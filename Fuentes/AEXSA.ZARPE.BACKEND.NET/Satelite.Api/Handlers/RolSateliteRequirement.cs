using Microsoft.AspNetCore.Authorization;

namespace Satelite.Api.Handlers;

/// <summary>
/// Exige que el usuario tenga al menos un rol del Satélite. El user pool es compartido:
/// un usuario válido del pool sin rol del Satélite recibe 403 (ADR-016).
/// Forma parte de la política por defecto, así que aplica a todo [Authorize] y [AccessAuthorize].
/// </summary>
public sealed class RolSateliteRequirement : IAuthorizationRequirement;

public sealed class RolSateliteHandler(ResolutorRoles resolutor) : AuthorizationHandler<RolSateliteRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RolSateliteRequirement requirement)
    {
        if (resolutor.RolesDe(context.User).Count > 0)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
