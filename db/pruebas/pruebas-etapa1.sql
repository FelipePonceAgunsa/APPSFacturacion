-- Pruebas de las reglas de la base (etapa 1). Todo corre en una transaccion que se revierte: no deja datos.
-- Uso (desde db/):  Get-Content .\pruebas\pruebas-etapa1.sql | docker exec -i zarpe-postgres psql -U postgres -d zarpe_facturacion -q
BEGIN;

CREATE TEMP TABLE resultado (n serial, prueba text, esperado text, obtenido text, detalle text);

CREATE FUNCTION pg_temp.debe_fallar(p_prueba text, p_sql text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    EXECUTE p_sql;
    INSERT INTO resultado (prueba, esperado, obtenido) VALUES (p_prueba, 'rechaza', 'acepta');
EXCEPTION WHEN others THEN
    INSERT INTO resultado (prueba, esperado, obtenido, detalle) VALUES (p_prueba, 'rechaza', 'rechaza', SQLERRM);
END $$;

CREATE FUNCTION pg_temp.debe_pasar(p_prueba text, p_sql text) RETURNS void LANGUAGE plpgsql AS $$
BEGIN
    EXECUTE p_sql;
    INSERT INTO resultado (prueba, esperado, obtenido) VALUES (p_prueba, 'acepta', 'acepta');
EXCEPTION WHEN others THEN
    INSERT INTO resultado (prueba, esperado, obtenido, detalle) VALUES (p_prueba, 'acepta', 'rechaza', SQLERRM);
END $$;

\o /dev/null
-- Datos base
INSERT INTO tipo_servicio VALUES (42, 'REC', 'Recepcion', 'CTR', 'Contenedor', true);
INSERT INTO operacion (operacion_id_aexsa, documento_carga_id, version_evento) VALUES ('OP-1', 'DOC-1', 3);
INSERT INTO ofa (id, operacion_id, estado) SELECT '10000000-0000-0000-0000-000000000001', id, 'BORRADOR' FROM operacion;

-- [E1-1] Ingesta de versiones
SELECT pg_temp.debe_fallar('E1-1 version_evento baja de 3 a 2', $q$UPDATE operacion SET version_evento = 2 WHERE operacion_id_aexsa = 'OP-1'$q$);
SELECT pg_temp.debe_pasar ('E1-1 version_evento sube de 3 a 4', $q$UPDATE operacion SET version_evento = 4 WHERE operacion_id_aexsa = 'OP-1'$q$);
SELECT pg_temp.debe_pasar ('E1-1 completar datos sin cambiar la version', $q$UPDATE operacion SET tipo_retiro = 'DIRECTO' WHERE operacion_id_aexsa = 'OP-1'$q$);
SELECT pg_temp.debe_pasar ('E1-1 primer APLICADO de la v4', $q$INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado) VALUES ('OP-1', 4, 'OPERACION_ACTUALIZADA', '{}', 'APLICADO')$q$);
SELECT pg_temp.debe_fallar('E1-1 segundo APLICADO de la misma version', $q$INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado) VALUES ('OP-1', 4, 'OPERACION_ACTUALIZADA', '{}', 'APLICADO')$q$);
SELECT pg_temp.debe_pasar ('E1-1 DUPLICADO de la misma version se registra', $q$INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado) VALUES ('OP-1', 4, 'OPERACION_ACTUALIZADA', '{}', 'DUPLICADO')$q$);
SELECT pg_temp.debe_fallar('E1-1 RECHAZADO sin codigo de error', $q$INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado) VALUES ('OP-1', 2, 'OPERACION_ACTUALIZADA', '{}', 'RECHAZADO')$q$);
SELECT pg_temp.debe_pasar ('E1-1 RECHAZADO con VERSION_SUPERADA', $q$INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado, codigo_error) VALUES ('OP-1', 2, 'OPERACION_ACTUALIZADA', '{}', 'RECHAZADO', 'VERSION_SUPERADA')$q$);
SELECT pg_temp.debe_fallar('DBA-7 segunda OFA principal', $q$INSERT INTO ofa (operacion_id, estado) SELECT id, 'BORRADOR' FROM operacion$q$);

-- [E1-2] Catalogo: versiones completas
SELECT pg_temp.debe_fallar('E1-2 primera importacion solo de listas (sin base)', $q$INSERT INTO catalogo_importacion (origen, tipo, estado, cargado_por) VALUES ('ARCHIVO', 'LISTAS_PRECIOS', 'PENDIENTE_CONFIRMACION', 'p')$q$);
SELECT pg_temp.debe_pasar ('E1-2 version 1 completa (COMPLETO)', $q$
    INSERT INTO catalogo_importacion (id, origen, tipo, estado, cargado_por) VALUES ('20000000-0000-0000-0000-000000000001', 'ARCHIVO', 'COMPLETO', 'CONFIRMADA', 'p');
    INSERT INTO catalogo_snapshot (version, origen, importacion_id, confirmado_por, confirmado_en) VALUES (1, 'ARCHIVO', '20000000-0000-0000-0000-000000000001', 'p', now());
    INSERT INTO sap_articulo VALUES (1, 'SRV-001', 'Recepcion', 'CTR', true, NULL);
    INSERT INTO sap_lista_precios VALUES (1, 'LP-PUB', 'Publica', true);
    INSERT INTO tarifa_snapshot (version, articulo_id, lista_precios_id, precio, moneda, vigente_desde) VALUES (1, 'SRV-001', 'LP-PUB', 17000, 'CLP', '2026-01-01')$q$);
SELECT pg_temp.debe_pasar ('E1-2 version 2: importacion parcial de listas con base 1', $q$
    INSERT INTO catalogo_importacion (id, origen, tipo, estado, cargado_por, base_version) VALUES ('20000000-0000-0000-0000-000000000002', 'ARCHIVO', 'LISTAS_PRECIOS', 'CONFIRMADA', 'p', 1);
    INSERT INTO catalogo_snapshot (version, origen, importacion_id, confirmado_por, confirmado_en) VALUES (2, 'ARCHIVO', '20000000-0000-0000-0000-000000000002', 'p', now());
    INSERT INTO sap_lista_precios VALUES (2, 'LP-PUB', 'Publica', true)$q$);
SELECT pg_temp.debe_fallar('E1-2 precio en v2 sin copiar los articulos', $q$INSERT INTO tarifa_snapshot (version, articulo_id, lista_precios_id, precio, moneda, vigente_desde) VALUES (2, 'SRV-001', 'LP-PUB', 18000, 'CLP', '2026-06-01')$q$);
SELECT pg_temp.debe_pasar ('E1-2 precio en v2 despues de copiar los articulos de la base', $q$
    INSERT INTO sap_articulo SELECT 2, codigo, descripcion, unidad_medida, activo, grupo FROM sap_articulo WHERE snapshot_version = 1;
    INSERT INTO tarifa_snapshot (version, articulo_id, lista_precios_id, precio, moneda, vigente_desde) VALUES (2, 'SRV-001', 'LP-PUB', 18000, 'CLP', '2026-06-01')$q$);
SELECT pg_temp.debe_pasar ('E1-2 kit en v2', $q$INSERT INTO sap_kit (id, snapshot_version, kit_codigo, nombre) VALUES ('30000000-0000-0000-0000-000000000001', 2, 'SPOT', 'Spot')$q$);
SELECT pg_temp.debe_fallar('E1-2 kit con articulo inexistente', $q$INSERT INTO sap_kit_articulo (kit_id, snapshot_version, articulo_id) VALUES ('30000000-0000-0000-0000-000000000001', 2, 'NO-EXISTE')$q$);
SELECT pg_temp.debe_fallar('E1-2 kit con articulo de otra version', $q$INSERT INTO sap_kit_articulo (kit_id, snapshot_version, articulo_id) VALUES ('30000000-0000-0000-0000-000000000001', 1, 'SRV-001')$q$);
SELECT pg_temp.debe_pasar ('E1-2 kit con articulo de su version', $q$INSERT INTO sap_kit_articulo (kit_id, snapshot_version, articulo_id) VALUES ('30000000-0000-0000-0000-000000000001', 2, 'SRV-001')$q$);

-- [E1-3] Tipo de cambio
SELECT pg_temp.debe_fallar('E1-3 fuente API_SAP (nombre antiguo)', $q$INSERT INTO tipo_cambio VALUES ('2026-11-01', 'USD', 950, 'API_SAP', '20000000-0000-0000-0000-000000000001', 'p')$q$);
SELECT pg_temp.debe_fallar('E1-3 MANUAL con importacion', $q$INSERT INTO tipo_cambio VALUES ('2026-11-01', 'USD', 950, 'MANUAL', '20000000-0000-0000-0000-000000000001', 'p')$q$);
SELECT pg_temp.debe_fallar('E1-3 ARCHIVO sin importacion', $q$INSERT INTO tipo_cambio VALUES ('2026-11-01', 'USD', 950, 'ARCHIVO', NULL, 'p')$q$);
SELECT pg_temp.debe_pasar ('E1-3 MANUAL cuando falta el de SAP', $q$INSERT INTO tipo_cambio VALUES ('2026-11-02', 'USD', 951, 'MANUAL', NULL, 'facturador')$q$);
SELECT pg_temp.debe_pasar ('E1-3 SAP reemplaza al MANUAL', $q$UPDATE tipo_cambio SET valor = 952, fuente = 'ARCHIVO', importacion_id = '20000000-0000-0000-0000-000000000001', cargado_por = 'p' WHERE fecha = '2026-11-02' AND moneda = 'USD'$q$);
SELECT pg_temp.debe_fallar('E1-3 MANUAL no reemplaza al de SAP', $q$UPDATE tipo_cambio SET valor = 999, fuente = 'MANUAL', importacion_id = NULL WHERE fecha = '2026-11-02' AND moneda = 'USD'$q$);

-- [E1-4] Item agregado a mano
SELECT pg_temp.debe_fallar('E1-4 manual sin quien', $q$INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, motivo_agregada, agregada_en) VALUES ('10000000-0000-0000-0000-000000000001', 42, 1, 'CTR', 1, 'CLP', 1, 1, 'AGREGADA_MANUAL', 'Pedido por correo', now())$q$);
SELECT pg_temp.debe_fallar('E1-4 manual sin motivo', $q$INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, agregada_por, agregada_en) VALUES ('10000000-0000-0000-0000-000000000001', 42, 1, 'CTR', 1, 'CLP', 1, 1, 'AGREGADA_MANUAL', 'facturador', now())$q$);
SELECT pg_temp.debe_fallar('E1-4 manual con motivo en blanco', $q$INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, agregada_por, motivo_agregada, agregada_en) VALUES ('10000000-0000-0000-0000-000000000001', 42, 1, 'CTR', 1, 'CLP', 1, 1, 'AGREGADA_MANUAL', 'facturador', '   ', now())$q$);
SELECT pg_temp.debe_pasar ('E1-4 manual completa', $q$INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, agregada_por, motivo_agregada, agregada_en) VALUES ('10000000-0000-0000-0000-000000000001', 42, 1, 'CTR', 1, 'CLP', 1, 1, 'AGREGADA_MANUAL', 'facturador', 'Pedido por correo', now())$q$);
SELECT pg_temp.debe_fallar('E1-4 calculada con datos de agregado manual', $q$INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, agregada_por) VALUES ('10000000-0000-0000-0000-000000000001', 42, 1, 'CTR', 1, 'CLP', 1, 1, 'CALCULADA', 'facturador')$q$);
SELECT pg_temp.debe_pasar ('E1-4 calculada limpia', $q$INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo) VALUES ('10000000-0000-0000-0000-000000000001', 42, 1, 'CTR', 1, 'CLP', 1, 1)$q$);

-- [E1-5] Pendientes
SELECT pg_temp.debe_pasar ('E1-5 pendiente abierto', $q$INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion) SELECT id, 'TIPO_RETIRO', true FROM operacion$q$);
SELECT pg_temp.debe_fallar('E1-5 DECISION sin motivo', $q$INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resuelto_por, resolucion, decision) SELECT id, 'SENTIDO', true, now(), 'f', 'DECISION', '{"sentido":"IMPORTACION"}' FROM operacion$q$);
SELECT pg_temp.debe_fallar('E1-5 DECISION con motivo en blanco', $q$INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resuelto_por, resolucion, decision, motivo) SELECT id, 'SENTIDO', true, now(), 'f', 'DECISION', '{"sentido":"IMPORTACION"}', ' ' FROM operacion$q$);
SELECT pg_temp.debe_fallar('E1-5 DECISION sin la decision', $q$INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resuelto_por, resolucion, motivo) SELECT id, 'SENTIDO', true, now(), 'f', 'DECISION', 'Segun DIN' FROM operacion$q$);
SELECT pg_temp.debe_fallar('E1-5 resuelto sin usuario', $q$INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resolucion) SELECT id, 'SENTIDO', true, now(), 'YA_NO_APLICA' FROM operacion$q$);
SELECT pg_temp.debe_pasar ('E1-5 DECISION completa', $q$INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resuelto_por, resolucion, decision, motivo) SELECT id, 'SENTIDO', true, now(), 'f', 'DECISION', '{"sentido":"IMPORTACION"}', 'Segun DIN' FROM operacion$q$);

\o
\pset footer off
SELECT n, CASE WHEN esperado = obtenido THEN 'OK' ELSE 'FALLA' END AS estado, prueba, left(coalesce(detalle, ''), 90) AS detalle FROM resultado ORDER BY n;
SELECT count(*) FILTER (WHERE esperado = obtenido) AS ok, count(*) FILTER (WHERE esperado <> obtenido) AS fallas FROM resultado;

ROLLBACK;
