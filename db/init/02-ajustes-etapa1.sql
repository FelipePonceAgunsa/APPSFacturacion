-- Ajustes de la etapa 1 sobre el esquema de modelo-datos.md v1.1 (01-esquema.sql).
-- Alcance etapa 1: recepcion de AEXSA, calculo e item manual (incluye catalogo SAP y tipo de cambio).
-- Fuera: omisiones, alertas y reglas configurables.
--
-- Estado: PROPUESTA para el DBA. Cuando estos cambios entren a modelo-datos.md, este archivo se borra.
-- Es re-ejecutable (IF EXISTS / IF NOT EXISTS / OR REPLACE), por si el modelo ya trae parte de ellos.
-- Marca de cada cambio: [E1-n].

-- ===== [E1-1] Ingesta de AEXSA: una version vieja no pisa a una nueva
-- Algoritmo (en la aplicacion, una transaccion por mensaje):
--   1. SELECT ... FROM operacion WHERE entidad = $1 AND operacion_id_aexsa = $2 FOR UPDATE   (serializa mensajes de la misma operacion; clave en [E1-6])
--   2. Si no existe -> INSERT operacion (el UNIQUE de entidad + operacion_id_aexsa resuelve la carrera) -> APLICADO (201).
--   3. version  > version_evento -> UPDATE operacion -> APLICADO (200).
--      version  = version_evento -> DUPLICADO (409, AEXSA lo da por enviado).
--      version  < version_evento -> RECHAZADO, codigo_error = 'VERSION_SUPERADA' (409, sin reintento).
--   4. Siempre INSERT en evento_aexsa con el resultado.
-- Cada mensaje trae el estado completo de la operacion: perder una version intermedia no importa.
ALTER TABLE evento_aexsa ADD COLUMN IF NOT EXISTS codigo_error varchar(40);   -- p.ej. VERSION_SUPERADA, VALIDACION
ALTER TABLE evento_aexsa DROP CONSTRAINT IF EXISTS ck_evento_aexsa_rechazo_con_codigo;
ALTER TABLE evento_aexsa ADD CONSTRAINT ck_evento_aexsa_rechazo_con_codigo
    CHECK (resultado <> 'RECHAZADO' OR codigo_error IS NOT NULL);

-- Defensa en la base: version_evento nunca baja (puede quedar igual al completar datos a mano).
CREATE OR REPLACE FUNCTION fn_operacion_version_no_baja() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.version_evento < OLD.version_evento THEN
        RAISE EXCEPTION 'VERSION_SUPERADA: operacion % tiene version % y se intento bajar a %',
            OLD.operacion_id_aexsa, OLD.version_evento, NEW.version_evento
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS trg_operacion_version_no_baja ON operacion;
CREATE TRIGGER trg_operacion_version_no_baja
    BEFORE UPDATE OF version_evento ON operacion
    FOR EACH ROW EXECUTE FUNCTION fn_operacion_version_no_baja();

-- ===== [E1-2] Catalogo SAP: cada version queda completa
-- Una importacion parcial (LISTAS_PRECIOS, KITS, TIPO_CAMBIO) parte de la version vigente (base_version).
-- Al confirmar, la aplicacion crea la version nueva y copia desde base_version lo que no vino:
--   INSERT INTO sap_articulo      SELECT :nueva, codigo, descripcion, unidad_medida, activo, grupo FROM sap_articulo WHERE snapshot_version = :base;
--   INSERT INTO sap_lista_precios SELECT :nueva, codigo, nombre, es_publica FROM sap_lista_precios WHERE snapshot_version = :base;
--   INSERT INTO tarifa_snapshot (version, articulo_id, lista_precios_id, precio, moneda, vigente_desde, vigente_hasta)
--        SELECT :nueva, articulo_id, lista_precios_id, precio, moneda, vigente_desde, vigente_hasta FROM tarifa_snapshot WHERE version = :base;
--   (kits igual, generando id nuevo para sap_kit y repitiendo su composicion)
-- COMPLETO trae todo y no copia nada. Las FK por version impiden una version con precios o kits sin sus articulos.
ALTER TABLE catalogo_importacion ADD COLUMN IF NOT EXISTS base_version int REFERENCES catalogo_snapshot(version);
ALTER TABLE catalogo_importacion DROP CONSTRAINT IF EXISTS ck_importacion_parcial_con_base;
ALTER TABLE catalogo_importacion ADD CONSTRAINT ck_importacion_parcial_con_base
    CHECK (tipo NOT IN ('LISTAS_PRECIOS','KITS') OR base_version IS NOT NULL);  -- la primera version debe traer articulos

-- Kits: cada articulo del kit debe existir en la misma version del catalogo.
ALTER TABLE sap_kit DROP CONSTRAINT IF EXISTS ux_sap_kit_id_version;
ALTER TABLE sap_kit ADD CONSTRAINT ux_sap_kit_id_version UNIQUE (id, snapshot_version);
ALTER TABLE sap_kit_articulo ADD COLUMN IF NOT EXISTS snapshot_version int NOT NULL;
ALTER TABLE sap_kit_articulo DROP CONSTRAINT IF EXISTS fk_kit_articulo_kit_version;
ALTER TABLE sap_kit_articulo ADD CONSTRAINT fk_kit_articulo_kit_version
    FOREIGN KEY (kit_id, snapshot_version) REFERENCES sap_kit(id, snapshot_version);
ALTER TABLE sap_kit_articulo DROP CONSTRAINT IF EXISTS fk_kit_articulo_articulo;
ALTER TABLE sap_kit_articulo ADD CONSTRAINT fk_kit_articulo_articulo
    FOREIGN KEY (snapshot_version, articulo_id) REFERENCES sap_articulo(snapshot_version, codigo);

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

-- ===== [E1-4] Item agregado a mano: quien, cuando y por que
ALTER TABLE linea_ofa ADD COLUMN IF NOT EXISTS motivo_agregada text;
ALTER TABLE linea_ofa ADD COLUMN IF NOT EXISTS agregada_en timestamptz;
ALTER TABLE linea_ofa DROP CONSTRAINT IF EXISTS ck_linea_ofa_agregada_manual;
ALTER TABLE linea_ofa ADD CONSTRAINT ck_linea_ofa_agregada_manual CHECK (
    CASE WHEN origen = 'AGREGADA_MANUAL'
         THEN agregada_por IS NOT NULL AND btrim(agregada_por) <> ''
          AND motivo_agregada IS NOT NULL AND btrim(motivo_agregada) <> ''
          AND agregada_en IS NOT NULL
         ELSE agregada_por IS NULL AND motivo_agregada IS NULL AND agregada_en IS NULL
    END);

-- ===== [E1-5] Pendientes: una resolucion completa y una decision con motivo
ALTER TABLE operacion_pendiente DROP CONSTRAINT IF EXISTS ck_pendiente_resolucion_completa;
ALTER TABLE operacion_pendiente ADD CONSTRAINT ck_pendiente_resolucion_completa CHECK (
    (resuelto_en IS NULL) = (resuelto_por IS NULL) AND (resuelto_en IS NULL) = (resolucion IS NULL));
ALTER TABLE operacion_pendiente DROP CONSTRAINT IF EXISTS ck_pendiente_decision_con_motivo;
ALTER TABLE operacion_pendiente ADD CONSTRAINT ck_pendiente_decision_con_motivo CHECK (
    resolucion IS DISTINCT FROM 'DECISION'
    OR (motivo IS NOT NULL AND btrim(motivo) <> '' AND decision IS NOT NULL));

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
