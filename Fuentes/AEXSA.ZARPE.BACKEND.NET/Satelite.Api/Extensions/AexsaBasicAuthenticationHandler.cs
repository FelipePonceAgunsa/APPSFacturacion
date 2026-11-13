using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Satelite.Api.Handlers;

namespace Satelite.Api.Extensions;

/// <summary>
/// Credencial de AEXSA SYS para el push de eventos (sección <c>Aexsa:Basic</c>). Provisoria hasta que PA-002
/// defina el mecanismo definitivo. Los valores reales van por variable de entorno
/// (<c>Aexsa__Basic__Usuario</c>, <c>Aexsa__Basic__Clave</c>) o user-secrets; nunca en appsettings.
/// </summary>
public sealed class AexsaBasicOptions
{
    public const string Seccion = "Aexsa:Basic";
    public const int LargoMinimoClaveFueraDeDesarrollo = 16;

    public string Usuario { get; set; } = "";
    public string Clave { get; set; } = "";

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(Usuario) && !string.IsNullOrEmpty(Clave);
}

/// <summary>
/// Autenticación HTTP Basic del endpoint que invoca AEXSA SYS (<c>POST /api/aexsa/eventos</c>).
/// Falla cerrado: sin credencial configurada rechaza todo. Compara en tiempo constante.
/// Solo es válida sobre HTTPS / red privada.
/// </summary>
public sealed class AexsaBasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<AexsaBasicOptions> aexsa,
    IOptions<AuthOptions> auth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Esquema = "AexsaBasic";
    public const string Politica = "AexsaIngesta";
    public const string Sub = "aexsa-sys";
    public const string ClaimSistema = "sistema";
    public const string ValorSistema = "aexsa";
    public const string Realm = "ZARPE AEXSA";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var config = aexsa.Value;
        if (!config.EstaConfigurado)
        {
            return Task.FromResult(AuthenticateResult.Fail("Credencial de AEXSA sin configurar."));
        }

        var cabecera = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(cabecera))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        const string prefijo = "Basic ";
        if (!cabecera.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Se requiere autenticación Basic."));
        }

        string usuario, clave;
        try
        {
            var texto = Encoding.UTF8.GetString(Convert.FromBase64String(cabecera[prefijo.Length..].Trim()));
            var separador = texto.IndexOf(':');
            if (separador < 0)
            {
                return Task.FromResult(AuthenticateResult.Fail("Credencial mal formada."));
            }

            usuario = texto[..separador];
            clave = texto[(separador + 1)..];
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Credencial mal formada."));
        }

        // Ambas comparaciones se evalúan siempre (sin cortocircuito) y en tiempo constante.
        var usuarioOk = IgualesEnTiempoConstante(usuario, config.Usuario);
        var claveOk = IgualesEnTiempoConstante(clave, config.Clave);
        if (!(usuarioOk & claveOk))
        {
            return Task.FromResult(AuthenticateResult.Fail("Credencial inválida."));
        }

        var claims = new[]
        {
            new Claim("sub", Sub),
            new Claim(auth.Value.NameClaim, Sub),
            new Claim(ClaimSistema, ValorSistema),
        };
        var identidad = new ClaimsIdentity(claims, Esquema, auth.Value.NameClaim, auth.Value.RoleClaim);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema)));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties) => Rechazar(StatusCodes.Status401Unauthorized);

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) => Rechazar(StatusCodes.Status403Forbidden);

    private Task Rechazar(int status)
    {
        Response.StatusCode = status;
        Response.Headers.WWWAuthenticate = $"Basic realm=\"{Realm}\", charset=\"UTF-8\"";
        Response.ContentType = "application/json";
        var cuerpo = new
        {
            codigo = "NO_AUTORIZADO",
            mensaje = "Credencial de AEXSA ausente o inválida.",
            detalle = "Enviar 'Authorization: Basic <usuario:clave en base64>'.",
        };
        return Response.WriteAsync(JsonSerializer.Serialize(cuerpo));
    }

    private static bool IgualesEnTiempoConstante(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
