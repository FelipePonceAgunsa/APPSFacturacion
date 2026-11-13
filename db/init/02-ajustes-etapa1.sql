-- Ajustes de la etapa 1 sobre el esquema de modelo-datos.md v1.1 (01-esquema.sql).
-- Alcance etapa 1: recepcion de AEXSA, calculo e item manual (incluye catalogo SAP y tipo de cambio).
-- Fuera: omisiones, alertas y reglas configurables.
--
-- Estado: PROPUESTA para el DBA. Cuando estos cambios entren a modelo-datos.md, este archivo se borra.
-- Es re-ejecutable (IF EXISTS / IF NOT EXISTS / OR REPLACE), por si el modelo ya trae parte de ellos.
-- Marca de cada cambio: [E1-n].

-- ===== [E1-1] Ingesta de AEXSA: una version vieja no pisa a una nueva
-- Algoritmo (en la aplicacion, una transaccion por mensaje):
--   1. SELECT ... FROM operacion WHERE operacion_id_aexsa = $1 FOR UPDATE   (serializa mensajes de la misma operacion)
--   2. Si no existe -> INSERT operacion (el UNIQUE de operacion_id_aexsa resuelve la carrera) -> APLICADO (201).
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
