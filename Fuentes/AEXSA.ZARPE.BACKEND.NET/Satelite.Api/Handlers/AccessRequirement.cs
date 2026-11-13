using Microsoft.AspNetCore.Authorization;

namespace Satelite.Api.Handlers;

public class AccessRequirement : IAuthorizationRequirement
{
    public string[] AccessNames { get; }

    public AccessRequirement(string[] accessNames) => AccessNames = accessNames;
}
