namespace Satelite.Api.Handlers;

/// <summary>
/// Configuración de autenticación con Cognito (sección <c>Auth</c> de appsettings).
/// Ningún valor es secreto: el app client del Satélite es público (Authorization Code + PKCE).
/// Los valores reales los entrega quien administra el user pool de la empresa (PA-009).
/// </summary>
public sealed class AuthOptions
{
    public const string Seccion = "Auth";

    /// <summary>Emisor del user pool: https://cognito-idp.{region}.amazonaws.com/{userPoolId}</summary>
    public string Authority { get; set; } = string.Empty;

    /// <summary>App client del Satélite. Se rechazan tokens emitidos para otros app clients del pool.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Claim que trae los grupos del usuario. Cognito: <c>cognito:groups</c>.</summary>
    public string RoleClaim { get; set; } = "cognito:groups";

    /// <summary>Claim con el nombre de usuario. Access token de Cognito: <c>username</c>.</summary>
    public string NameClaim { get; set; } = "username";

    /// <summary>
    /// Rol del Satélite → nombre del grupo en el pool. El pool es compartido con otras aplicaciones,
    /// por eso cada rol se mapea a un grupo explícito. [POR CONFIRMAR PA-009] nombres reales de los grupos.
    /// </summary>
    public Dictionary<string, string> GruposPorRol { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);
}
