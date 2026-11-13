using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Comun;
using Satelite.Aplicacion.Configuracion;
using Satelite.Aplicacion.Convenios;
using Satelite.Aplicacion.Ofa;
using Satelite.Aplicacion.Operaciones;
using Satelite.Infraestructura.Archivos;
using Satelite.Infraestructura.Persistencia;
using Satelite.Infraestructura.Repositorios;

namespace Satelite.Infraestructura;

public static class DependencyInjection
{
    /// <summary>
    /// Registra la persistencia (EF Core + PostgreSQL) y los adaptadores de infraestructura.
    /// La cadena de conexión NO va en appsettings (regla 7 del steering): en local se carga con
    /// <c>dotnet user-secrets</c>; en AWS, por variable de entorno <c>ConnectionStrings__DefaultConnection</c>.
    /// </summary>
    public static IServiceCollection AddInfraestructura(this IServiceCollection services, IConfiguration configuration)
    {
        var conn = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException(
                "La cadena de conexión 'DefaultConnection' no está configurada. " +
                "En local: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"<cadena>\" --project Satelite.Api");
        }

        services.AddDbContext<PrincipalDbContext>(options => options.UseNpgsql(conn));

        services.AddHealthChecks()
            .AddDbContextCheck<PrincipalDbContext>("postgres");

        services.AddScoped<IUnidadTrabajo, UnidadTrabajo>();
        services.AddScoped<IAuditoria, Auditoria>();

        // Almacén de archivos: carpeta local (stub de S3). Por defecto <ContentRoot>/App_Data/archivos.
        services.AddSingleton<IAlmacenArchivos>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var ruta = config["Almacen:RutaLocal"];
            if (string.IsNullOrWhiteSpace(ruta))
            {
                var raiz = config["contentRoot"] ?? Directory.GetCurrentDirectory();
                ruta = Path.Combine(raiz, "App_Data", "archivos");
            }
            return new AlmacenArchivosLocal(ruta);
        });

        services.AddScoped<ICatalogoRepositorio, CatalogoRepositorio>();
        services.AddScoped<IConfiguracionRepositorio, ConfiguracionRepositorio>();
        services.AddScoped<IConveniosRepositorio, ConveniosRepositorio>();
        services.AddScoped<IOperacionesRepositorio, OperacionesRepositorio>();
        services.AddScoped<IOfaRepositorio, OfaRepositorio>();

        return services;
    }
}
