-- Datos de prueba INVENTADOS: catálogo de servicios (tipo_servicio, tarea 1.1).
-- Los IDs imitan la clave de MasterData.TipoServicio de AEXSA, pero no son los reales.
-- Motor:ConceptosDirecto de appsettings.Development.json apunta a 42 a 47 [SUPUESTO: IDs de datos de prueba].
INSERT INTO tipo_servicio (tipo_servicio_id, codigo, descripcion, unidad_normalizada, unidad_origen, habilitado) VALUES
    (42, 'REC',     'Recepción',                'CTR',     'Contenedor', true),
    (43, 'DES',     'Despacho',                 'CTR',     'Contenedor', true),
    (44, 'MOV',     'Movilización',             'CTR',     'Contenedor', true),
    (45, 'VIS',     'Visación documental',      'BL',      'BL',         true),
    (46, 'SECFEE',  'Security fee',             'CTR',     'Contenedor', true),
    (47, 'REFACT',  'Refacturación terminal',   'CTR',     'Contenedor', true),
    (48, 'ALM',     'Almacenaje',               'CTR_DIA', 'Día',        true),
    (49, 'SAG',     'Inspección SAG',           'CTR',     'Contenedor', true),
    (50, 'USOAREA', 'Uso de área',              'CTR',     'Contenedor', true),
    (51, 'DOCEXP',  'Documentación exportación','DUS',     'DUS',        true),
    (52, 'MANTON',  'Manipuleo por tonelada',   'TON',     'Tonelada',   true)
ON CONFLICT (tipo_servicio_id) DO NOTHING;
