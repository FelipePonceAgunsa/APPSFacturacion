using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Satelite.Infraestructura.Persistencia;

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

        return services;
    }
}
