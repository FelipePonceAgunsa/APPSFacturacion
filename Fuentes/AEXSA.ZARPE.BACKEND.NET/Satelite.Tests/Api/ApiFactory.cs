using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Satelite.Infraestructura.Persistencia;

namespace Satelite.Tests.Api;

/// <summary>
/// API en memoria (TestServer) contra una base de Testcontainers. Ambiente Development, así que el modo abierto
/// de appsettings.Development.json queda activo salvo que <paramref name="ajustes"/> diga otra cosa.
/// </summary>
public sealed class ApiFactory(string connectionString, IDictionary<string, string?>? ajustes = null, string ambiente = "Development")
    : WebApplicationFactory<Program>
{
    /// <summary>Credencial Basic de AEXSA que usan las pruebas (valores inventados; nunca reales).</summary>
    public const string UsuarioAexsa = "aexsa-pruebas";
    public const string ClaveAexsa = "clave-de-pruebas-aexsa-0001";

    public static System.Net.Http.Headers.AuthenticationHeaderValue CabeceraAexsa(string usuario = UsuarioAexsa, string clave = ClaveAexsa) =>
        new("Basic", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{usuario}:{clave}")));

    private readonly string _almacen = Path.Combine(Path.GetTempPath(), "zarpe-pruebas", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ambiente);
        // UseSetting entra como configuración del host: Program.cs la ve antes de builder.Build().
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Almacen:RutaLocal", _almacen);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        builder.UseSetting("Aexsa:Basic:Usuario", UsuarioAexsa);
        builder.UseSetting("Aexsa:Basic:Clave", ClaveAexsa);
        foreach (var (clave, valor) in ajustes ?? new Dictionary<string, string?>())
        {
            builder.UseSetting(clave, valor);
        }
    }

    public PrincipalDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<PrincipalDbContext>().UseNpgsql(connectionString).Options);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(_almacen))
        {
            Directory.Delete(_almacen, recursive: true);
        }
    }
}
