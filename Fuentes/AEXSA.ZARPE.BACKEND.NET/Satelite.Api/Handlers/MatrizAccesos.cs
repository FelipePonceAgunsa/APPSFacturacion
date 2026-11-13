namespace Satelite.Api.Handlers;

/// <summary>
/// Matriz rol → accesos. Reemplaza a las tablas Role/Access/AccessRole del estándar BPT:
/// en el Satélite la identidad y los roles vienen de Cognito + Entra ID (ADR-016),
/// así que la asignación de permisos a cada rol vive en código y se revisa en cada cambio.
///
/// [SUPUESTO: matriz inicial a validar con negocio — PA-009 "Matriz de permisos por rol"]
/// </summary>
public static class MatrizAccesos
{
    private static readonly IReadOnlyDictionary<string, string[]> AccesosPorRol =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [RolesSatelite.Administrador] =
            [
                AccessNames.Convenios, AccessNames.Convenios_Edit, AccessNames.Convenios_Confirmar,
                AccessNames.CatalogoSap, AccessNames.CatalogoSap_Importar,
                AccessNames.Operaciones, AccessNames.Ofa, AccessNames.Ofa_AjusteManual,
            ],
            [RolesSatelite.Comercial] =
            [
                AccessNames.Convenios, AccessNames.Convenios_Edit, AccessNames.Convenios_Confirmar,
                AccessNames.CatalogoSap,
            ],
            [RolesSatelite.Facturacion] =
            [
                AccessNames.Convenios,
                AccessNames.CatalogoSap, AccessNames.CatalogoSap_Importar,
                AccessNames.Operaciones, AccessNames.Ofa, AccessNames.Ofa_AjusteManual,
            ],
            [RolesSatelite.Recaudacion] =
            [
                AccessNames.Operaciones, AccessNames.Ofa,
            ],
        };

    public static IReadOnlyList<string> AccesosDe(IEnumerable<string> roles) =>
        roles
            .SelectMany(r => AccesosPorRol.TryGetValue(r, out var accesos) ? accesos : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
}
