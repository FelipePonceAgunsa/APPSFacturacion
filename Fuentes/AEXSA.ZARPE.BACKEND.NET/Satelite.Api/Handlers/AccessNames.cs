namespace Satelite.Api.Handlers;

/// <summary>
/// Constantes de permisos de acceso (estándar AGUNSA).
/// Cada módulo nuevo agrega aquí sus permisos y los asigna a los roles en <see cref="MatrizAccesos"/>.
/// Se usan const para que funcionen en atributos [AccessAuthorize].
/// El frontend replica los mismos nombres en src/app/helpers/access.constants.ts.
/// </summary>
public static class AccessNames
{
    // --- Convenios (E1) ---
    public const string Convenios            = "convenios";
    public const string Convenios_Edit       = "convenios_edit";
    public const string Convenios_Confirmar  = "convenios_confirmar";

    // --- Catálogo SAP (E2) ---
    public const string CatalogoSap          = "catalogoSap";
    public const string CatalogoSap_Importar = "catalogoSap_importar";

    // --- Operaciones y OFA (E2) ---
    public const string Operaciones          = "operaciones";
    public const string Operaciones_Edit     = "operaciones_edit";
    public const string Ofa                  = "ofa";
    public const string Ofa_AjusteManual     = "ofa_ajusteManual";

    // --- Configuración: mapeo de terminales y tipos de inspección (rol Administrador) ---
    public const string Configuracion        = "configuracion";
}
