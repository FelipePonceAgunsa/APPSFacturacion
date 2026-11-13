using Microsoft.AspNetCore.Authorization;

namespace Satelite.Api.Handlers;

/// <summary>
/// Verifica que el usuario tenga al menos uno de los accesos requeridos.
/// Los accesos se derivan de los roles del Satélite (grupos de Cognito) con <see cref="MatrizAccesos"/>.
/// </summary>
public class AccessAuthorizationHandler(ResolutorRoles resolutor) : AuthorizationHandler<AccessRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AccessRequirement requirement)
    {
        var userAccess = resolutor.AccesosDe(context.User);
        if (userAccess.Count == 0)
        {
            return Task.CompletedTask;
        }

        bool hasAccess = requirement.AccessNames.Any(required =>
            userAccess.Any(ua => string.Equals(ua, required, StringComparison.OrdinalIgnoreCase)));

        if (hasAccess)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
