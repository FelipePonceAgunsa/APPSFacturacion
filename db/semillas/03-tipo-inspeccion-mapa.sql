-- Datos de prueba INVENTADOS: tipos de inspección de AEXSA y su grupo ([E1-7], RN-052).
-- Los códigos reales del catálogo de AEXSA están [POR CONFIRMAR] (contratos-integracion.md §2 nota 15).
INSERT INTO tipo_inspeccion_mapa (codigo_aexsa, descripcion, grupo, actualizado_por) VALUES
    ('ADU-FIS', 'Aforo físico de aduana (ejemplo)', 'AFORO_ADUANA', 'semilla'),
    ('SAG',     'Inspección SAG (ejemplo)',         'SAG',          'semilla'),
    ('OTR',     'Otra inspección (ejemplo)',        'OTRA',         'semilla')
ON CONFLICT (codigo_aexsa) DO NOTHING;
