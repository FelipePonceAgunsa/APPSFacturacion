-- Datos de prueba INVENTADOS: terminales de AEXSA y su grupo (RN-079, pantalla T4).
-- El 3 queda SIN_MAPEAR para probar el mapeo desde la pantalla.
INSERT INTO terminal_mapa (terminal_aexsa_id, nombre, grupo, actualizado_por) VALUES
    (1, 'STI DE EJEMPLO',           'STI',        'semilla'),
    (2, 'TERMINAL DE EJEMPLO',      'DP_WORLD',   'semilla'),
    (3, 'TERMINAL NUEVO DE EJEMPLO','SIN_MAPEAR', NULL)
ON CONFLICT (terminal_aexsa_id) DO NOTHING;
