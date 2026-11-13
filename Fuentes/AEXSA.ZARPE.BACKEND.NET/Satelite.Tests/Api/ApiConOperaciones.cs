using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Convenios;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Tests.Infraestructura;
namespace Satelite.Tests.Api;
/// <summary>
/// Base de las pruebas de operaciones y OFA: tipos de servicio 42 a 52, terminales 1 (STI) y 2 (DP World), tipos de
/// inspección ADU-FIS (aforo) y SAG, el catálogo SAP de ejemplo de datos-prueba/catalogo confirmado y los vínculos 42 a 52.
/// Todo inventado.
/// </summary>
public abstract class ApiConOperaciones(PostgresFixture fixture) : ApiConBaseNueva(fixture)
{
    protected const string RutConvenio = "76.111.111-6";
    protected const string RutSinConvenio = "78.333.333-3";
    protected static readonly string DatosPrueba = BuscarDatosPrueba();
    private static readonly Dictionary<int, string> Vinculos = new()
    {
        [42] = "SRV-REC-001", [43] = "SRV-DES-001", [44] = "SRV-MOV-001", [45] = "SRV-VIS-001", [46] = "SRV-SEC-001",
        [47] = "SRV-REF-001", [48] = "SRV-ALM-001", [49] = "SRV-SAG-001", [50] = "SRV-USO-001", [51] = "SRV-DOC-001",
        [52] = "SRV-MAN-001",
    };
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await using (var db = Api.CrearContexto())
        {
            db.TiposServicio.AddRange(
                new TipoServicio { TipoServicioId = 49, Codigo = "SAG", Descripcion = "Inspección SAG", UnidadNormalizada = UnidadCobro.CTR },
                new TipoServicio { TipoServicioId = 50, Codigo = "USOAREA", Descripcion = "Uso de área", UnidadNormalizada = UnidadCobro.CTR },
                new TipoServicio { TipoServicioId = 51, Codigo = "DOCEXP", Descripcion = "Documentación expo", UnidadNormalizada = UnidadCobro.DUS },
                new TipoServicio { TipoServicioId = 52, Codigo = "MANTON", Descripcion = "Manipuleo", UnidadNormalizada = UnidadCobro.TON });
            db.TerminalesMapa.AddRange(
                new TerminalMapa { TerminalAexsaId = 1, Nombre = "STI DE EJEMPLO", Grupo = GrupoTerminal.STI, ActualizadoEn = DateTimeOffset.UtcNow },
                new TerminalMapa { TerminalAexsaId = 2, Nombre = "TERMINAL DE EJEMPLO", Grupo = GrupoTerminal.DP_WORLD, ActualizadoEn = DateTimeOffset.UtcNow });
            db.TiposInspeccionMapa.AddRange(
                new TipoInspeccionMapa { CodigoAexsa = "ADU-FIS", Grupo = GrupoInspeccion.AFORO_ADUANA, ActualizadoEn = DateTimeOffset.UtcNow },
                new TipoInspeccionMapa { CodigoAexsa = "SAG", Grupo = GrupoInspeccion.SAG, ActualizadoEn = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(Ct);
        }
        var catalogo = Path.Combine(DatosPrueba, "catalogo");
        var formulario = new MultipartFormDataContent { { new StringContent("COMPLETO"), "tipo" } };
        foreach (var (parte, archivo) in new[]
                 {
                     ("articulos", "A1_articulos_20261101.csv"), ("listasPrecios", "A2_listas_precios_20261101.csv"),
                     ("kits", "A3_kits_20261101.csv"), ("tipoCambio", "A4_tipo_cambio_20261101.csv"),
                 })
        {
            var contenido = new ByteArrayContent(await File.ReadAllBytesAsync(Path.Combine(catalogo, archivo), Ct));
            contenido.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            formulario.Add(contenido, parte, archivo);
        }
        var importacion = await Cliente.PostAsync("/api/catalogo/importaciones", formulario, Ct);
        Assert.Equal(HttpStatusCode.Created, importacion.StatusCode);
        var resultado = await LeerAsync<ResultadoImportacionDto>(importacion);
        var confirmar = await Cliente.PostAsync($"/api/catalogo/importaciones/{resultado.ImportacionId}/confirmar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, confirmar.StatusCode);
        foreach (var (id, articulo) in Vinculos)
        {
            Assert.Equal(HttpStatusCode.OK, (await PutJsonAsync($"/api/catalogo/vinculos/{id}", new { articuloSap = articulo })).StatusCode);
        }
    }
    /// <summary>Crea y confirma un convenio desde un JSON de datos-prueba/convenios (o con otra vigencia).</summary>
    protected async Task<VersionConvenioDto> CrearConvenioAsync(string archivo, string? desde = null, string? hasta = null)
    {
        var json = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(DatosPrueba, "convenios", archivo), Ct))!.AsObject();
        if (desde is not null)
        {
            json["vigenciaDesde"] = desde;
            json["vigenciaHasta"] = hasta;
        }
        var creado = await PostRawAsync("/api/convenios", json.ToJsonString());
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var borrador = await LeerAsync<VersionConvenioDto>(creado);
        var confirmado = await Cliente.PostAsync($"/api/convenios/{borrador.ConvenioId}/versiones/{borrador.VersionId}/confirmar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, confirmado.StatusCode);
        return await LeerAsync<VersionConvenioDto>(confirmado);
    }
    /// <summary>
    /// Evento v3.0 de importación indirecta: 1 contenedor con 15 días de almacenaje, aforo ADU-FIS del 21 al 23 y SAG
    /// aprobada; servicios 42, 43, 45, 48 (15 días) y 49. Visado el 2026-10-26 (TC 700 en el catálogo de ejemplo).
    /// </summary>
    protected static JsonObject Evento(long id, string rut = RutSinConvenio, int version = 1)
    {
        var texto = """
            {
              "entidad": "VISADO_VIRTUAL", "tipoEvento": "VISADO_APROBADO", "version": __VERSION__,
              "operacionIdAexsa": [__ID__], "visadoAprobadoEn": "2026-10-26T14:30:00Z", "estadoOperacion": "ACTIVA",
              "tipoCarga": "CONTENEDOR", "sentido": "IMPORTACION", "tipoRetiro": "INDIRECTO",
              "terminal": { "id": 1, "nombre": "STI DE EJEMPLO" },
              "din": [6100000001], "bls": [{ "id": __BL__, "numero": "BL-__ID__" }], "papeletas": [5512],
              "consignatario": { "rut": "__RUT__", "razonSocial": "Cliente de ejemplo" },
              "clienteFacturador": null,
              "contenedores": [{
                "numero": "ABCD1234567", "fechaRecepcion": "2026-10-20T08:00:00Z", "fechaRetiro": "2026-10-27T10:00:00Z",
                "fechaDespacho": null, "diasAlmacenaje": 15, "esReefer": false,
                "inspecciones": [
                  { "tipo": "ADU-FIS", "asignadaEn": "2026-10-21T09:00:00Z", "finalizadaEn": "2026-10-23T11:00:00Z", "resultado": "APROBADA" },
                  { "tipo": "SAG", "asignadaEn": "2026-10-21T09:00:00Z", "finalizadaEn": "2026-10-22T11:00:00Z", "resultado": "APROBADA" }
                ]
              }],
              "servicios": [
                { "tipoServicioId": 42, "cantidad": 1, "unidad": "Contenedor", "contenedor": null, "finalizadoEn": "2026-10-24T10:00:00Z", "origen": "AEXSA" },
                { "tipoServicioId": 43, "cantidad": 1, "unidad": "Contenedor", "contenedor": null, "finalizadoEn": null, "origen": "AEXSA" },
                { "tipoServicioId": 45, "cantidad": 1, "unidad": "Por B/L", "contenedor": null, "finalizadoEn": null, "origen": "AEXSA" },
                { "tipoServicioId": 48, "cantidad": 15, "unidad": "Contenedor / Día", "contenedor": "ABCD1234567", "finalizadoEn": null, "origen": "AEXSA" },
                { "tipoServicioId": 49, "cantidad": 1, "unidad": "Contenedor", "contenedor": null, "finalizadoEn": null, "origen": "AEXSA" }
              ]
            }
            """;
        return JsonNode.Parse(texto
            .Replace("__VERSION__", version.ToString(CultureInfo.InvariantCulture))
            .Replace("__ID__", id.ToString(CultureInfo.InvariantCulture))
            .Replace("__BL__", (id * 10).ToString(CultureInfo.InvariantCulture))
            .Replace("__RUT__", rut))!.AsObject();
    }
    /// <summary>Directo sin convenio en el terminal indicado (1 = STI, 2 = DP World), con servicios que la Regla 0 descarta.</summary>
    protected static JsonObject EventoDirecto(long id, int terminal)
    {
        var e = Evento(id);
        e["tipoRetiro"] = "DIRECTO";
        e["terminal"] = new JsonObject { ["id"] = terminal, ["nombre"] = terminal == 1 ? "STI DE EJEMPLO" : "TERMINAL DE EJEMPLO" };
        e["contenedores"]![0]!["inspecciones"] = new JsonArray();
        e["servicios"] = JsonNode.Parse("""
            [
              { "tipoServicioId": 42, "cantidad": 1, "unidad": null, "contenedor": null, "finalizadoEn": null, "origen": "AEXSA" },
              { "tipoServicioId": 43, "cantidad": 1, "unidad": null, "contenedor": null, "finalizadoEn": null, "origen": "AEXSA" },
              { "tipoServicioId": 45, "cantidad": 1, "unidad": null, "contenedor": null, "finalizadoEn": null, "origen": "AEXSA" }
            ]
            """);
        return e;
    }
    /// <summary>Envía el evento como lo hace AEXSA SYS: con la credencial Basic de pruebas.</summary>
    protected Task<HttpResponseMessage> EnviarEventoAsync(JsonNode evento)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/aexsa/eventos")
        {
            Content = new StringContent(evento.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = ApiFactory.CabeceraAexsa();
        return Cliente.SendAsync(request, Ct);
    }
    protected Task<HttpResponseMessage> PostRawAsync(string url, string json) =>
        Cliente.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), Ct);
    protected Task<HttpResponseMessage> PatchJsonAsync(string url, object cuerpo) =>
        Cliente.PatchAsync(url, new StringContent(System.Text.Json.JsonSerializer.Serialize(cuerpo, Json), Encoding.UTF8, "application/json"), Ct);
    private static string BuscarDatosPrueba()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidato = Path.Combine(dir.FullName, "datos-prueba");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }
        }
        throw new DirectoryNotFoundException("No se encontró la carpeta datos-prueba del repositorio.");
    }
}
