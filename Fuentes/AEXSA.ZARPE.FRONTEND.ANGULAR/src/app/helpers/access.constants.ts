/**
 * Constantes de permisos de acceso.
 *
 * Deben coincidir con Satelite.Api/Handlers/AccessNames.cs. Los accesos del usuario los
 * calcula la API a partir de sus roles (GET /api/Session/Me); el frontend solo los consulta.
 * La validación usa `endsWith` (case-insensitive), igual que el estándar AGUNSA.
 */
export const Access = {
  // --- Convenios (E1) ---
  Convenios:            'convenios',
  Convenios_Edit:       'convenios_edit',
  Convenios_Confirmar:  'convenios_confirmar',

  // --- Catálogo SAP (E2) ---
  CatalogoSap:          'catalogoSap',
  CatalogoSap_Importar: 'catalogoSap_importar',

  // --- Operaciones y OFA (E2) ---
  Operaciones:          'operaciones',
  Ofa:                  'ofa',
  Ofa_AjusteManual:     'ofa_ajusteManual',
  Operaciones_Edit:     'operaciones_edit',

  // --- Configuración: terminales y tipos de inspección (Administrador) ---
  Configuracion:        'configuracion',
} as const;
