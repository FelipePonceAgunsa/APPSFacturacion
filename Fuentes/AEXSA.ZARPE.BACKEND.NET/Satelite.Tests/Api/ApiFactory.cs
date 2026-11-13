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
    private readonly string _almacen = Path.Combine(Path.GetTempPath(), "zarpe-pruebas", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(ambiente);
        // UseSetting entra como configuración del host: Program.cs la ve antes de builder.Build().
        builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
        builder.UseSetting("Almacen:RutaLocal", _almacen);
        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
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
