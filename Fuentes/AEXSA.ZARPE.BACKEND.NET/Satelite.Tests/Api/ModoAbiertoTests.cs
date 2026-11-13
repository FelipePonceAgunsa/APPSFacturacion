using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Satelite.Api.Extensions;
using Satelite.Api.Models;
using Satelite.Infraestructura.Persistencia;
using Satelite.Tests.Infraestructura;

namespace Satelite.Tests.Api;

/// <summary>Tarea 0.4: modo abierto de desarrollo (Auth:ModoAbierto), secure by default.</summary>
[Collection(PostgresCollection.Nombre)]
public class ModoAbiertoTests(PostgresFixture fixture)
{
    [Fact]
    public async Task En_Development_con_modo_abierto_el_usuario_es_Administrador_local()
    {
        await using var api = new ApiFactory(fixture.ConnectionString);
        var cliente = api.CreateClient();

        var respuesta = await cliente.GetAsync("/api/Session/Me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var usuario = await respuesta.Content.ReadFromJsonAsync<UsuarioActualResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(usuario);
        Assert.Equal(ModoAbiertoAuthenticationHandler.Email, usuario.Email);
        Assert.Equal(["Administrador"], usuario.Roles);
        Assert.Contains("convenios_confirmar", usuario.Access);
        Assert.Contains("configuracion", usuario.Access);
    }

    [Fact]
    public async Task La_api_de_pruebas_usa_la_base_del_contenedor()
    {
        await using var api = new ApiFactory(fixture.ConnectionString);
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PrincipalDbContext>();

        Assert.Equal(
            new Npgsql.NpgsqlConnectionStringBuilder(fixture.ConnectionString).Port,
            new Npgsql.NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Port);
    }

    [Fact]
    public async Task Sin_modo_abierto_un_request_sin_token_recibe_401()
    {
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["Auth:ModoAbierto"] = "false" });
        var cliente = api.CreateClient();

        var respuesta = await cliente.GetAsync("/api/Session/Me", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Fuera_de_Development_el_modo_abierto_impide_arrancar(string ambiente)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:ModoAbierto"] = "true",
                ["Auth:Authority"] = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_ejemplo",
                ["Auth:ClientId"] = "cliente-ejemplo",
            })
            .Build();

        var error = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddCognitoAuthentication(configuracion, new Ambiente(ambiente)));
        Assert.Contains("ModoAbierto", error.Message);
    }

    [Fact]
    public async Task Fuera_de_Development_el_host_no_arranca_con_modo_abierto()
    {
        await using var api = new ApiFactory(fixture.ConnectionString, new Dictionary<string, string?> { ["Auth:ModoAbierto"] = "true" }, "Production");

        Assert.ThrowsAny<Exception>(() => api.CreateClient());
    }

    private sealed class Ambiente(string nombre) : IHostEnvironment, IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;
        public string ApplicationName { get; set; } = "Satelite.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
