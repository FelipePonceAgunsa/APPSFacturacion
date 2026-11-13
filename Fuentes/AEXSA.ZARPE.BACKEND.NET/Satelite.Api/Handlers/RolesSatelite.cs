namespace Satelite.Api.Handlers;

/// <summary>
/// Roles del Satélite (ADR-016). Llegan desde los grupos del user pool de Cognito
/// según el mapeo de <c>Auth:GruposPorRol</c> en la configuración.
/// </summary>
public static class RolesSatelite
{
    public const string Comercial     = "Comercial";
    public const string Facturacion   = "Facturacion";
    public const string Recaudacion   = "Recaudacion";
    public const string Administrador = "Administrador";

    public static readonly IReadOnlyList<string> Todos = [Comercial, Facturacion, Recaudacion, Administrador];
}
