namespace Satelite.Aplicacion.Comun;

/// <summary>
/// Usuario autenticado del request actual (identidad emitida por Cognito federado con Entra ID, ADR-016).
/// Los casos de uso lo usan para la auditoría: <see cref="Sub"/> y <see cref="Email"/> se guardan como
/// <c>usuario</c> en <c>evento_auditoria</c> y en los ajustes manuales.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Identificador estable del usuario en el pool (claim <c>sub</c>).</summary>
    string? Sub { get; }

    /// <summary>Nombre de usuario en el pool (claim configurado en <c>Auth:NameClaim</c>).</summary>
    string? UserName { get; }

    /// <summary>Correo, si el token lo trae. [POR CONFIRMAR PA-009] el access token de Cognito no lo incluye por defecto.</summary>
    string? Email { get; }

    /// <summary>Roles del Satélite (Comercial, Facturacion, Recaudacion, Administrador).</summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>Accesos (permisos) que resultan de los roles.</summary>
    IReadOnlyList<string> Access { get; }

    /// <summary>Identificador a registrar en auditoría: el correo si existe; si no, el <c>sub</c>.</summary>
    string UsuarioAuditoria { get; }
}
