using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Satelite.Api.Handlers;

namespace Satelite.Api.Extensions;

public static class SeguridadExtensions
{
    /// <summary>
    /// Autenticación con el JWT de Cognito federado con Entra ID (ADR-016, tarea 0.4).
    /// La API solo valida el token: firma (JWKS del pool), emisor, expiración, <c>token_use = access</c>
    /// y <c>client_id</c> del app client del Satélite. No emite tokens ni guarda credenciales.
    /// Con <c>Auth:ModoAbierto</c> (solo Development) registra en su lugar el esquema de modo abierto.
    /// Devuelve el nombre del esquema activo, que usan las políticas de autorización.
    /// </summary>
    public static string AddCognitoAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var seccion = configuration.GetSection(AuthOptions.Seccion);
        services.Configure<AuthOptions>(seccion);
        var auth = seccion.Get<AuthOptions>() ?? new AuthOptions();

        if (auth.ModoAbierto)
        {
            // Secure by default: el modo abierto nunca se activa fuera de Development.
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    $"'Auth:ModoAbierto' solo se permite en el ambiente Development (ambiente actual: {environment.EnvironmentName}).");
            }

            services.AddAuthentication(ModoAbiertoAuthenticationHandler.Esquema)
                .AddScheme<AuthenticationSchemeOptions, ModoAbiertoAuthenticationHandler>(
                    ModoAbiertoAuthenticationHandler.Esquema, _ => { });
            return ModoAbiertoAuthenticationHandler.Esquema;
        }

        if (!auth.EstaConfigurado && !environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Falta configurar 'Auth:Authority' y 'Auth:ClientId' (user pool de Cognito, PA-009).");
        }

        var issuer = auth.Authority.TrimEnd('/');

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Mantener los nombres de claim tal como vienen en el token (sub, cognito:groups, client_id...).
                options.MapInboundClaims = false;

                if (auth.EstaConfigurado)
                {
                    options.Authority = issuer;
                }

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    // El access token de Cognito no trae 'aud': la audiencia se valida con 'client_id' (OnTokenValidated).
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = auth.NameClaim,
                    RoleClaimType = auth.RoleClaim,
                    ClockSkew = TimeSpan.Zero,
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var principal = context.Principal;
                        var tokenUse = principal?.FindFirst("token_use")?.Value;
                        var clientId = principal?.FindFirst("client_id")?.Value;

                        if (!string.Equals(tokenUse, "access", StringComparison.Ordinal))
                        {
                            context.Fail("Se requiere un access token.");
                        }
                        else if (!string.Equals(clientId, auth.ClientId, StringComparison.Ordinal))
                        {
                            context.Fail("El token fue emitido para otra aplicación del pool.");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        return JwtBearerDefaults.AuthenticationScheme;
    }

    /// <summary>
    /// Autorización: la política por defecto (y la de respaldo) exige usuario autenticado con al menos
    /// un rol del Satélite. Los endpoints públicos se marcan explícitamente con AllowAnonymous.
    /// <paramref name="esquema"/> es el esquema de autenticación activo (JWT de Cognito o modo abierto).
    /// </summary>
    public static IServiceCollection AddAutorizacionSatelite(this IServiceCollection services, string esquema)
    {
        services.AddSingleton<ResolutorRoles>();
        services.AddSingleton<IAuthorizationHandler, AccessAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, RolSateliteHandler>();

        services.AddAuthorization(options =>
        {
            var politica = new AuthorizationPolicyBuilder(esquema)
                .RequireAuthenticatedUser()
                .AddRequirements(new RolSateliteRequirement())
                .Build();

            options.DefaultPolicy = politica;
            options.FallbackPolicy = politica;
        });

        return services;
    }
}
