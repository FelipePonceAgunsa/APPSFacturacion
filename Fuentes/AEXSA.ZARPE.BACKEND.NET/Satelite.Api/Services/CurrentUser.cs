using System.Security.Claims;
using Microsoft.Extensions.Options;
using Satelite.Api.Handlers;
using Satelite.Aplicacion.Comun;

namespace Satelite.Api.Services;

/// <summary>
/// Implementación de <see cref="ICurrentUser"/> a partir de los claims del access token de Cognito.
/// </summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor, ResolutorRoles resolutor, IOptions<AuthOptions> options)
    : ICurrentUser
{
    private readonly AuthOptions _options = options.Value;
    private IReadOnlyList<string>? _roles;
    private IReadOnlyList<string>? _access;

    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? Sub => Principal?.FindFirst("sub")?.Value;

    public string? UserName => Principal?.FindFirst(_options.NameClaim)?.Value;

    public string? Email => Principal?.FindFirst("email")?.Value;

    public IReadOnlyList<string> Roles => _roles ??= Principal is null ? [] : resolutor.RolesDe(Principal);

    public IReadOnlyList<string> Access => _access ??= MatrizAccesos.AccesosDe(Roles);

    public string UsuarioAuditoria => Email ?? Sub ?? "desconocido";
}
