using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace Satelite.Api.Handlers;

/// <summary>
/// Traduce los grupos del token de Cognito a roles del Satélite y a accesos.
/// Un grupo del pool que no esté mapeado en <c>Auth:GruposPorRol</c> se ignora.
/// </summary>
public sealed class ResolutorRoles(IOptions<AuthOptions> options)
{
    private readonly AuthOptions _options = options.Value;

    public IReadOnlyList<string> RolesDe(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return [];

        var grupos = user.FindAll(_options.RoleClaim)
            .Select(c => c.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return RolesSatelite.Todos
            .Where(rol => _options.GruposPorRol.TryGetValue(rol, out var grupo)
                          && !string.IsNullOrWhiteSpace(grupo)
                          && grupos.Contains(grupo))
            .ToList();
    }

    public IReadOnlyList<string> AccesosDe(ClaimsPrincipal user) => MatrizAccesos.AccesosDe(RolesDe(user));
}
