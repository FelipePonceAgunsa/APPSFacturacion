using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Tests.Infraestructura;

namespace Satelite.Tests.Api;

/// <summary>
/// Base de las pruebas de API: cada prueba usa una base nueva (misma instancia de PostgreSQL) con los tipos de
/// servicio 42 a 52 de los datos de prueba y la API en modo abierto (Administrador).
/// </summary>
public abstract class ApiConBaseNueva(PostgresFixture fixture) : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    protected ApiFactory Api { get; private set; } = null!;
    protected HttpClient Cliente { get; private set; } = null!;
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    public virtual async ValueTask InitializeAsync()
    {
        Api = new ApiFactory(await fixture.CrearBaseNuevaAsync());
        Cliente = Api.CreateClient();

        await using var db = Api.CrearContexto();
        db.TiposServicio.AddRange(
            Tipo(42, "REC", "Recepción", UnidadCobro.CTR),
            Tipo(43, "DES", "Despacho", UnidadCobro.CTR),
            Tipo(44, "MOV", "Movilización", UnidadCobro.CTR),
            Tipo(45, "VIS", "Visación documental", UnidadCobro.BL),
            Tipo(46, "SECFEE", "Security fee", UnidadCobro.CTR),
            Tipo(47, "REFACT", "Refacturación terminal", UnidadCobro.CTR),
            Tipo(48, "ALM", "Almacenaje", UnidadCobro.CTR_DIA));
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        Cliente.Dispose();
        await Api.DisposeAsync();
    }

    private static TipoServicio Tipo(int id, string codigo, string descripcion, UnidadCobro unidad) =>
        new() { TipoServicioId = id, Codigo = codigo, Descripcion = descripcion, UnidadNormalizada = unidad };

    protected async Task<T> LeerAsync<T>(HttpResponseMessage respuesta) =>
        (await respuesta.Content.ReadFromJsonAsync<T>(Json, Ct))!;

    protected Task<HttpResponseMessage> PostJsonAsync(string url, object cuerpo) =>
        Cliente.PostAsync(url, new StringContent(JsonSerializer.Serialize(cuerpo, Json), Encoding.UTF8, "application/json"), Ct);

    protected Task<HttpResponseMessage> PutJsonAsync(string url, object cuerpo) =>
        Cliente.PutAsync(url, new StringContent(JsonSerializer.Serialize(cuerpo, Json), Encoding.UTF8, "application/json"), Ct);
}
