using Microsoft.AspNetCore.Authorization;

namespace Satelite.Api.Handlers;

/// <summary>
/// Protege un endpoint con uno o más accesos (basta con tener uno). Estándar AGUNSA:
/// <c>[AccessAuthorize(AccessNames.Convenios_Edit)]</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class AccessAuthorizeAttribute : AuthorizeAttribute, IAuthorizationRequirementData
{
    public string[] AccessNames { get; }

    public AccessAuthorizeAttribute(params string[] accessNames) => AccessNames = accessNames;

    public IEnumerable<IAuthorizationRequirement> GetRequirements()
    {
        yield return new AccessRequirement(AccessNames);
    }
}
