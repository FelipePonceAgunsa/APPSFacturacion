using Microsoft.Extensions.DependencyInjection;

namespace Satelite.Aplicacion;

public static class DependencyInjection
{
    /// <summary>
    /// Registra los servicios de la capa de aplicación (casos de uso).
    /// Cada módulo nuevo agrega aquí sus servicios: <c>services.AddScoped&lt;IConvenioService, ConvenioService&gt;();</c>
    /// </summary>
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        return services;
    }
}
