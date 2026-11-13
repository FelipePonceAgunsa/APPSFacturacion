using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Satelite.Api.Handlers;

namespace Satelite.Api.Extensions;

/// <summary>
/// Modo abierto de desarrollo (<c>Auth:ModoAbierto</c>): autentica cada request como el usuario local
/// <see cref="Email"/> con el rol Administrador. Ese usuario queda en la auditoría.
/// <see cref="SeguridadExtensions.AddCognitoAuthentication"/> impide registrarlo fuera de Development.
/// </summary>
public sealed class ModoAbiertoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AuthOptions> auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "ModoAbierto";
    public const string Sub = "modo-abierto";
    public const string UserName = "admin.local";
    public const string Email = "admin.local@zarpe.local";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var config = auth.Value;
        // El rol se entrega igual que en un token de Cognito: como el grupo mapeado al rol Administrador.
        var grupo = config.GruposPorRol.TryGetValue(RolesSatelite.Administrador, out var g) && !string.IsNullOrWhiteSpace(g)
            ? g
            : RolesSatelite.Administrador;
        var claims = new[]
        {
            new Claim("sub", Sub),
            new Claim(config.NameClaim, UserName),
            new Claim("email", Email),
            new Claim(config.RoleClaim, grupo),
        };
        var identidad = new ClaimsIdentity(claims, Esquema, config.NameClaim, config.RoleClaim);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
