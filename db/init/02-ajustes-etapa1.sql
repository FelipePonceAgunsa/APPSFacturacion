-- Ajustes de la etapa 1 sobre el esquema de modelo-datos.md (01-esquema.sql, generado).
-- Alcance etapa 1: recepcion de AEXSA, calculo e item manual (incluye catalogo SAP y tipo de cambio).
-- Fuera: omisiones, alertas y reglas configurables.
--
-- Estado: PROPUESTA para el DBA. Cuando estos cambios entren a modelo-datos.md, este archivo se borra.
-- Es re-ejecutable (IF EXISTS / IF NOT EXISTS / OR REPLACE).
-- Marca de cada cambio: [E1-n].
--
-- Reconciliado con la segunda revision del DBA (commit c6e1d8c, marcas [DBA-nb]). Ya estan en el modelo y se
-- quitaron de aqui: trigger de version que no retrocede (DBA-1b), catalogo completo por version con base_version
-- y kits con FK al articulo de su version (DBA-3b), CHECK del item manual (DBA-6b) y CHECK de pendientes (DBA-8b).

-- ===== [E1-1] Ingesta de AEXSA: codigo del rechazo
-- El algoritmo y el trigger de version estan en modelo-datos.md §4b (DBA-1b). Con el contrato v3.0, la busqueda
-- con FOR UPDATE es por entidad + operacion_id_aexsa (ver [E1-6]).
-- El codigo de ResultadoIngesta (VERSION_SUPERADA, INVALIDO, ENTIDAD_NO_HABILITADA) queda guardado en el mensaje.
ALTER TABLE evento_aexsa ADD COLUMN IF NOT EXISTS codigo_error varchar(40);
ALTER TABLE evento_aexsa DROP CONSTRAINT IF EXISTS ck_evento_aexsa_rechazo_con_codigo;
ALTER TABLE evento_aexsa ADD CONSTRAINT ck_evento_aexsa_rechazo_con_codigo
    CHECK (resultado <> 'RECHAZADO' OR codigo_error IS NOT NULL);

-- ===== [E1-3] Tipo de cambio: un solo vocabulario y coherencia con la importacion
-- Mismo vocabulario que catalogo_importacion.origen (API / ARCHIVO) mas MANUAL.
ALTER TABLE tipo_cambio DROP CONSTRAINT IF EXISTS tipo_cambio_fuente_check;
ALTER TABLE tipo_cambio ADD CONSTRAINT tipo_cambio_fuente_check
    CHECK (fuente IN ('API','ARCHIVO','MANUAL'));
-- MANUAL nunca trae importacion; API y ARCHIVO siempre.
ALTER TABLE tipo_cambio DROP CONSTRAINT IF EXISTS ck_tipo_cambio_fuente_importacion;
ALTER TABLE tipo_cambio ADD CONSTRAINT ck_tipo_cambio_fuente_importacion
    CHECK ((fuente = 'MANUAL') = (importacion_id IS NULL));

-- Precedencia: gana el valor de SAP (la prefactura debe coincidir con la factura, ADR-017).
-- Un valor MANUAL solo se ingresa si falta el de SAP; si despues llega el de SAP, lo reemplaza.
-- La aplicacion registra el cambio en evento_auditoria. Los calculos anteriores no cambian: memoria_calculo guarda el valor usado.
CREATE OR REPLACE FUNCTION fn_tipo_cambio_sap_prevalece() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.fuente IN ('API','ARCHIVO') AND NEW.fuente = 'MANUAL' THEN
        RAISE EXCEPTION 'TIPO_CAMBIO_DE_SAP_EXISTE: % % ya tiene valor de SAP (%)', OLD.fecha, OLD.moneda, OLD.fuente
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS trg_tipo_cambio_sap_prevalece ON tipo_cambio;
CREATE TRIGGER trg_tipo_cambio_sap_prevalece
    BEFORE UPDATE ON tipo_cambio
    FOR EACH ROW EXECUTE FUNCTION fn_tipo_cambio_sap_prevalece();

-- ===== [E1-4] Item agregado a mano: cuando
-- Quien y por que ya los exige el modelo (ck_linea_agregada_manual, DBA-6b). Falta cuando: la tarea 2.2 pide usuario,
-- fecha y motivo, y el OpenAPI expone agregadaEn.
ALTER TABLE linea_ofa ADD COLUMN IF NOT EXISTS agregada_en timestamptz;
ALTER TABLE linea_ofa DROP CONSTRAINT IF EXISTS ck_linea_agregada_en;
ALTER TABLE linea_ofa ADD CONSTRAINT ck_linea_agregada_en
    CHECK ((origen = 'AGREGADA_MANUAL') = (agregada_en IS NOT NULL));

-- ===== [E1-6] Contrato de entrada de AEXSA v3.0 (2026-11-13, contratos-integracion.md §2 notas 12 a 16)
-- Origen de la operacion: VISADO_VIRTUAL (etapa 1) o VISADO_PRESENCIAL (despues).
ALTER TABLE operacion ADD COLUMN IF NOT EXISTS entidad text NOT NULL DEFAULT 'VISADO_VIRTUAL';
ALTER TABLE operacion DROP CONSTRAINT IF EXISTS ck_operacion_entidad;
ALTER TABLE operacion ADD CONSTRAINT ck_operacion_entidad CHECK (entidad IN ('VISADO_VIRTUAL','VISADO_PRESENCIAL'));

-- operacionIdAexsa es un arreglo de IDs. operacion_id_aexsa queda como CLAVE de la operacion:
-- en visado virtual es el unico ID, como texto. En visado presencial, como se arma la clave esta [POR CONFIRMAR].
ALTER TABLE operacion ADD COLUMN IF NOT EXISTS ids_aexsa bigint[] NOT NULL;
ALTER TABLE operacion DROP CONSTRAINT IF EXISTS ck_operacion_ids_aexsa;
ALTER TABLE operacion ADD CONSTRAINT ck_operacion_ids_aexsa CHECK (
    cardinality(ids_aexsa) >= 1
    AND (entidad <> 'VISADO_VIRTUAL' OR (cardinality(ids_aexsa) = 1 AND operacion_id_aexsa = ids_aexsa[1]::text)));
-- La clave es unica por entidad: los IDs de dos origenes distintos pueden repetirse.
ALTER TABLE operacion DROP CONSTRAINT IF EXISTS operacion_operacion_id_aexsa_key;
ALTER TABLE operacion DROP CONSTRAINT IF EXISTS ux_operacion_entidad_clave;
ALTER TABLE operacion ADD CONSTRAINT ux_operacion_entidad_clave UNIQUE (entidad, operacion_id_aexsa);

-- Registro de mensajes: la entidad entra en la clave de idempotencia (entidad + operacionIdAexsa + version).
-- Puede faltar o ser invalida solo en un mensaje RECHAZADO (se guarda tal como llego).
ALTER TABLE evento_aexsa ADD COLUMN IF NOT EXISTS entidad text;
ALTER TABLE evento_aexsa DROP CONSTRAINT IF EXISTS ck_evento_aexsa_entidad;
ALTER TABLE evento_aexsa ADD CONSTRAINT ck_evento_aexsa_entidad
    CHECK (resultado = 'RECHAZADO' OR (entidad IS NOT NULL AND entidad IN ('VISADO_VIRTUAL','VISADO_PRESENCIAL')));  -- IS NOT NULL: un NULL IN (...) pasaria el CHECK
DROP INDEX IF EXISTS ux_evento_aexsa_aplicado;
CREATE UNIQUE INDEX ux_evento_aexsa_aplicado
    ON evento_aexsa (entidad, operacion_id_aexsa, version) WHERE resultado = 'APLICADO';
DROP INDEX IF EXISTS ix_evento_aexsa_operacion;
CREATE INDEX ix_evento_aexsa_operacion ON evento_aexsa (entidad, operacion_id_aexsa, recibido_en);

-- documentoCargaId se elimino: los documentos de carga llegan en bls (id y numero).
-- El documento con varias solicitudes se detecta por bl_id compartido entre operaciones activas.
DROP INDEX IF EXISTS ix_operacion_documento;
ALTER TABLE operacion DROP COLUMN IF EXISTS documento_carga_id;
ALTER TABLE operacion DROP COLUMN IF EXISTS bls;
CREATE TABLE IF NOT EXISTS operacion_bl (
    operacion_id  uuid NOT NULL REFERENCES operacion(id),
    bl_id         bigint NOT NULL,                     -- bls[].id: ID del documento de carga en AEXSA
    numero        varchar(40) NOT NULL,                -- bls[].numero: numero del BL
    PRIMARY KEY (operacion_id, bl_id)
);
CREATE INDEX IF NOT EXISTS ix_operacion_bl_documento ON operacion_bl (bl_id);  -- DOCUMENTO_CON_VARIAS_SOLICITUDES

-- din pasa a arreglo de numeros.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'operacion' AND column_name = 'din' AND data_type <> 'ARRAY') THEN
        ALTER TABLE operacion ALTER COLUMN din TYPE bigint[]
            USING CASE WHEN din IS NULL THEN NULL ELSE ARRAY[din::bigint] END;
    END IF;
END $$;

-- papeleta (numero y RUT) pasa a papeletas: arreglo de IDs. El contraste DIN<->papeleta (RN-001) lo hace AEXSA [POR CONFIRMAR].
ALTER TABLE operacion ADD COLUMN IF NOT EXISTS papeletas bigint[];
ALTER TABLE operacion DROP COLUMN IF EXISTS papeleta_numero;
ALTER TABLE operacion DROP COLUMN IF EXISTS papeleta_rut;
-- dus y cantidad_dus se mantienen: no vienen de AEXSA, el facturador completa la cantidad de DUS en exportacion (RN-047).

-- bajoAforo se elimino: el aforo llega en inspecciones. dias_bajo_aforo se mantiene (calculado desde la
-- inspeccion de aforo o completado por el facturador, RN-052).
ALTER TABLE contenedor_operacion DROP COLUMN IF EXISTS aforo_asignado_en;
ALTER TABLE contenedor_operacion DROP COLUMN IF EXISTS aforo_finalizado_en;
-- inspecciones: [{tipo, asignadaEn, finalizadaEn, resultado}]. tipo obligatorio; resultado null o uno de los 4 valores.
ALTER TABLE contenedor_operacion DROP CONSTRAINT IF EXISTS ck_contenedor_inspecciones;
ALTER TABLE contenedor_operacion ADD CONSTRAINT ck_contenedor_inspecciones CHECK (
    inspecciones IS NULL OR (
        jsonb_typeof(inspecciones) = 'array'
        AND NOT jsonb_path_exists(inspecciones,
            '$[*] ? (!exists(@.tipo) || (exists(@.resultado) && @.resultado != null
                     && !(@.resultado == "APROBADA" || @.resultado == "RECHAZADA_PARCIAL"
                          || @.resultado == "RECHAZADA_TOTAL" || @.resultado == "VISADO_LIBERADO")))')));

-- ===== [E1-7] Inspecciones: tipo mapeado y control antes de validar (contratos-integracion.md §2 nota 15)
-- AEXSA envia inspecciones[].tipo como codigo de su catalogo. El Administrador asigna cada codigo a un grupo
-- (mismo patron que terminal_mapa). Un codigo nuevo se inserta como SIN_MAPEAR; mientras tanto, los dias bajo
-- aforo de ese contenedor quedan como pendiente DIAS_AFORO y los completa el facturador.
CREATE TABLE IF NOT EXISTS tipo_inspeccion_mapa (
    codigo_aexsa    varchar(40) PRIMARY KEY,             -- inspecciones[].tipo tal como lo envia AEXSA
    descripcion     text,
    grupo           text NOT NULL DEFAULT 'SIN_MAPEAR'
                    CHECK (grupo IN ('AFORO_ADUANA','SAG','OTRA','SIN_MAPEAR')),  -- AFORO_ADUANA: sus dias no se cobran (RN-052)
    actualizado_por varchar(80),
    actualizado_en  timestamptz NOT NULL DEFAULT now()
);

-- Pendientes nuevos (RN-058): una inspeccion debe estar finalizada para validar la OFA; un rechazo lo decide el facturador.
--   INSPECCION_NO_FINALIZADA: sin finalizadaEn o con resultado null. Bloquea la validacion.
--   INSPECCION_RECHAZADA:     resultado RECHAZADA_PARCIAL o RECHAZADA_TOTAL. Exige una DECISION con motivo.
--   APROBADA o VISADO_LIBERADO, con finalizadaEn: no genera pendiente. El resultado no cambia el precio.
ALTER TABLE operacion_pendiente DROP CONSTRAINT IF EXISTS operacion_pendiente_tipo_check;
ALTER TABLE operacion_pendiente ADD CONSTRAINT operacion_pendiente_tipo_check CHECK (tipo IN (
    'TIPO_RETIRO','SENTIDO','TERMINAL_SIN_MAPEAR','CANTIDAD_DUS','CANTIDAD_SERVICIO','DIAS_AFORO','FECHA_DESPACHO',
    'VARIOS_CONVENIOS_VIGENTES','DOCUMENTO_CON_VARIAS_SOLICITUDES',
    'INSPECCION_NO_FINALIZADA','INSPECCION_RECHAZADA'));          -- mismos valores que el OpenAPI
