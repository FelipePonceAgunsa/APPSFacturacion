using Microsoft.Extensions.DependencyInjection;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Configuracion;
using Satelite.Aplicacion.Convenios;
using Satelite.Aplicacion.Ofa;
using Satelite.Aplicacion.Operaciones;
namespace Satelite.Aplicacion;
public static class DependencyInjection
{
    /// <summary>
    /// Registra los servicios de la capa de aplicación (casos de uso).
    /// Cada módulo nuevo agrega aquí sus servicios: <c>services.AddScoped&lt;IConvenioService, ConvenioService&gt;();</c>
    /// Las opciones (<see cref="CatalogoOptions"/>, <see cref="Motor.MotorOptions"/>, <see cref="OfaOptions"/>) se enlazan en la API.
    /// </summary>
    public static IServiceCollection AddAplicacion(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ICatalogoService, CatalogoService>();
        services.AddScoped<IImportacionCatalogoService, ImportacionCatalogoService>();
        services.AddScoped<IConfiguracionService, ConfiguracionService>();
        services.AddScoped<IConvenioService, ConvenioService>();
        services.AddScoped<ResolutorOperacion>();
        services.AddScoped<IIngestaEventoFacturable, IngestaEventoFacturable>();
        services.AddScoped<IOperacionService, OperacionService>();
        services.AddScoped<ConstructorContexto>();
        services.AddScoped<IOfaService, OfaService>();
        return services;
    }
}
