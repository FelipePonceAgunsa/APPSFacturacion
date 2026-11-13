using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Satelite.Tests.Infraestructura;

namespace Satelite.Tests.Api;

/// <summary>Push de AEXSA SYS: <c>POST /api/aexsa/eventos</c> con autenticación Basic provisoria (PA-002 pendiente).</summary>
[Collection(PostgresCollection.Nombre)]
public class AexsaAutenticacionTests(PostgresFixture fixture) : ApiConOperaciones(fixture)
{
    private async Task<HttpResponseMessage> PostAsync(HttpClient cliente, AuthenticationHeaderValue? auth, string? cabeceraCruda = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/aexsa/eventos")
        {
            Content = new StringContent(Evento(98900).ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (cabeceraCruda is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", cabeceraCruda);
        }
        else
        {
            request.Headers.Authorization = auth;
        }

        return await cliente.SendAsync(request, Ct);
    }

    private static async Task AssertNoAutorizadoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains(respuesta.Headers.WwwAuthenticate, h => h.Scheme == "Basic");
        var cuerpo = JsonNode.Parse(await respuesta.Content.ReadAsStringAsync(Ct))!;
        Assert.Equal("NO_AUTORIZADO", cuerpo["codigo"]!.GetValue<string>());
    }

    [Fact]
    public async Task Sin_credencial_responde_401_aunque_el_modo_abierto_este_activo()
    {
        // La base de la clase corre en Development con modo abierto: el modo abierto no abre este endpoint.
        await AssertNoAutorizadoAsync(await PostAsync(Cliente, null));
    }

    [Fact]
    public async Task Clave_incorrecta_responde_401()
    {
        await AssertNoAutorizadoAsync(await PostAsync(Cliente, ApiFactory.CabeceraAexsa(clave: "otra-clave")));
    }

    [Fact]
    public async Task Usuario_incorrecto_responde_401()
    {
        await AssertNoAutorizadoAsync(await PostAsync(Cliente, ApiFactory.CabeceraAexsa(usuario: "otro")));
    }

    [Fact]
    public async Task Credencial_mal_formada_responde_401()
    {
        await AssertNoAutorizadoAsync(await PostAsync(Cliente, null, "Basic %%%no-es-base64%%%"));
        await AssertNoAutorizadoAsync(await PostAsync(
            Cliente, null, "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("sin-separador"))));
        await AssertNoAutorizadoAsync(await PostAsync(Cliente, null, "Bearer abc"));
    }

    [Fact]
    public async Task Credencial_correcta_ingresa_el_evento_con_201()
    {
        var respuesta = await EnviarEventoAsync(Evento(98901));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_credencial_configurada_rechaza_todo_aunque_se_envie_una_cabecera()
    {
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?>
        {
            ["Aexsa:Basic:Usuario"] = "",
            ["Aexsa:Basic:Clave"] = "",
        });
        var cliente = api.CreateClient();

        await AssertNoAutorizadoAsync(await PostAsync(cliente, ApiFactory.CabeceraAexsa(usuario: "", clave: "")));
        await AssertNoAutorizadoAsync(await PostAsync(cliente, ApiFactory.CabeceraAexsa()));
    }

    [Fact]
    public void Fuera_de_Development_una_clave_corta_impide_arrancar()
    {
        using var api = new ApiFactory(
            fixture.ConnectionString,
            new Dictionary<string, string?>
            {
                ["Aexsa:Basic:Clave"] = "corta",
                ["Auth:Authority"] = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_ejemplo",
                ["Auth:ClientId"] = "cliente-ejemplo",
            },
            "Production");

        var error = Assert.ThrowsAny<Exception>(() => api.CreateClient());
        Assert.Contains("Aexsa:Basic:Clave", error.ToString());
    }

    [Fact]
    public async Task La_credencial_de_AEXSA_no_abre_los_demas_endpoints()
    {
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["Auth:ModoAbierto"] = "false" });
        var cliente = api.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/operaciones");
        request.Headers.Authorization = ApiFactory.CabeceraAexsa();

        var respuesta = await cliente.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
