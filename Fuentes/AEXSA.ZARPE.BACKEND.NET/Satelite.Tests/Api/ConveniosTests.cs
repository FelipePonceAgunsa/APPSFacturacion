using System.Net;
using Microsoft.EntityFrameworkCore;
using Satelite.Api.Controllers;
using Satelite.Aplicacion.Convenios;
using Satelite.Dominio.Comun;
using Satelite.Tests.Infraestructura;

namespace Satelite.Tests.Api;

/// <summary>Tarea 1.2: convenios versionados (BORRADOR → VIGENTE con confirmación, nunca se edita una VIGENTE).</summary>
[Collection(PostgresCollection.Nombre)]
public class ConveniosTests(PostgresFixture fixture) : ApiConBaseNueva(fixture)
{
    private static object Convenio(string rut, string desde = "2026-01-01", string? hasta = "2026-12-31") => new
    {
        rut,
        razonSocial = "Cliente de ejemplo",
        comercial = "Comercial de ejemplo",
        vigenciaDesde = desde,
        vigenciaHasta = hasta,
        tcMinimo = 800m,
        diasLibres = 10,
        servicios = new object[]
        {
            new { tipoServicioId = 42, tarifa = 15000m, moneda = "CLP" },
            new { tipoServicioId = 45, tarifa = 20.5m, moneda = "USD" },
        },
    };

    [Fact]
    public async Task Crear_guarda_un_convenio_en_borrador_que_se_lee_de_vuelta()
    {
        var respuesta = await PostJsonAsync("/api/convenios", Convenio("761111116"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var version = await LeerAsync<VersionConvenioDto>(respuesta);
        Assert.Equal(1, version.Numero);
        Assert.Equal(EstadoVersionConvenio.BORRADOR, version.Estado);
        Assert.Equal(OrigenVersionConvenio.MANUAL, version.Origen);

        var detalle = await LeerAsync<ConvenioDetalleDto>(await Cliente.GetAsync($"/api/convenios/{version.ConvenioId}", Ct));
        Assert.Equal("76.111.111-6", detalle.Rut);
        Assert.Equal("Cliente de ejemplo", detalle.RazonSocial);
        var leida = Assert.Single(detalle.Versiones);
        Assert.Equal(800m, leida.TcMinimo);
        Assert.Equal(10, leida.DiasLibres);
        Assert.Equal(new DateOnly(2026, 12, 31), leida.VigenciaHasta);
        Assert.Collection(leida.Servicios,
            s => { Assert.Equal(42, s.TipoServicioId); Assert.Equal(15000m, s.Tarifa); Assert.Equal(UnidadCobro.CTR, s.Unidad); },
            s => { Assert.Equal(45, s.TipoServicioId); Assert.Equal(Moneda.USD, s.Moneda); Assert.Equal(UnidadCobro.BL, s.Unidad); });

        var lista = await LeerAsync<List<ConvenioResumenDto>>(await Cliente.GetAsync("/api/convenios?rut=76.111.111-6", Ct));
        var resumen = Assert.Single(lista);
        Assert.Null(resumen.VersionVigente);
        Assert.Equal(EstadoVersionConvenio.BORRADOR, resumen.EstadoUltimaVersion);

        await using var db = Api.CrearContexto();
        Assert.True(await db.EventosAuditoria.AnyAsync(a => a.Entidad == "version_convenio" && a.EntidadId == version.VersionId, Ct));
    }

    [Fact]
    public async Task Confirmar_deja_la_version_vigente_y_una_nueva_version_reemplaza_a_la_anterior()
    {
        var v1 = await LeerAsync<VersionConvenioDto>(await PostJsonAsync("/api/convenios", Convenio("76.111.111-6")));

        var confirmada = await LeerAsync<VersionConvenioDto>(
            await Cliente.PostAsync($"/api/convenios/{v1.ConvenioId}/versiones/{v1.VersionId}/confirmar", null, Ct));
        Assert.Equal(EstadoVersionConvenio.VIGENTE, confirmada.Estado);
        Assert.Equal("admin.local@zarpe.local", confirmada.ConfirmadoPor);

        // Una VIGENTE no se vuelve a confirmar ni se edita.
        var otraVez = await Cliente.PostAsync($"/api/convenios/{v1.ConvenioId}/versiones/{v1.VersionId}/confirmar", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, otraVez.StatusCode);
        Assert.Equal(ConvenioService.VersionNoEditable, (await LeerAsync<ErrorRespuesta>(otraVez)).Codigo);

        var respuesta = await PostJsonAsync($"/api/convenios/{v1.ConvenioId}/versiones", new
        {
            vigenciaDesde = "2026-06-01",
            vigenciaHasta = (string?)null,
            tcMinimo = (decimal?)null,
            diasLibres = 5,
            origen = "COMPLEMENTO_CORREO",
            motivo = "Ajuste de tarifa por correo",
            servicios = new[] { new { tipoServicioId = 42, tarifa = 16000m, moneda = "CLP" } },
        });
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var v2 = await LeerAsync<VersionConvenioDto>(respuesta);
        Assert.Equal(2, v2.Numero);
        Assert.Equal(EstadoVersionConvenio.BORRADOR, v2.Estado);

        Assert.Equal(HttpStatusCode.OK,
            (await Cliente.PostAsync($"/api/convenios/{v1.ConvenioId}/versiones/{v2.VersionId}/confirmar", null, Ct)).StatusCode);

        var detalle = await LeerAsync<ConvenioDetalleDto>(await Cliente.GetAsync($"/api/convenios/{v1.ConvenioId}", Ct));
        Assert.Equal([2, 1], detalle.Versiones.Select(v => v.Numero));
        Assert.Equal(EstadoVersionConvenio.VIGENTE, detalle.Versiones[0].Estado);
        Assert.Equal(EstadoVersionConvenio.REEMPLAZADA, detalle.Versiones[1].Estado);
        Assert.Equal(15000m, detalle.Versiones[1].Servicios.Single(s => s.TipoServicioId == 42).Tarifa);

        var vigente = await LeerAsync<VersionConvenioVigenteDto>(
            await Cliente.GetAsync("/api/convenios/vigente?rut=76.111.111-6&fecha=2026-07-01", Ct));
        Assert.Equal(v2.VersionId, vigente.Version.VersionId);

        await using var db = Api.CrearContexto();
        var auditoria = await db.EventosAuditoria.SingleAsync(a => a.EntidadId == v2.VersionId && a.Accion == "CREAR_VERSION", Ct);
        Assert.Contains("Ajuste de tarifa por correo", auditoria.ValorNuevo);
    }

    [Fact]
    public async Task Vigente_fuera_del_rango_de_vigencia_responde_404()
    {
        var v1 = await LeerAsync<VersionConvenioDto>(await PostJsonAsync("/api/convenios", Convenio("77.222.222-K")));
        await Cliente.PostAsync($"/api/convenios/{v1.ConvenioId}/versiones/{v1.VersionId}/confirmar", null, Ct);

        Assert.Equal(HttpStatusCode.OK, (await Cliente.GetAsync("/api/convenios/vigente?rut=77222222-k&fecha=2026-12-31", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync("/api/convenios/vigente?rut=77.222.222-K&fecha=2027-01-01", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync("/api/convenios/vigente?rut=77.222.222-K&fecha=2025-12-31", Ct)).StatusCode);
    }

    [Fact]
    public async Task Con_dos_convenios_vigentes_se_elige_el_mas_antiguo_y_se_informan_los_candidatos()
    {
        var antiguo = await LeerAsync<VersionConvenioDto>(await PostJsonAsync("/api/convenios", Convenio("78.333.333-3")));
        var nuevo = await LeerAsync<VersionConvenioDto>(await PostJsonAsync("/api/convenios", Convenio("78.333.333-3", "2026-03-01", null)));
        await Cliente.PostAsync($"/api/convenios/{nuevo.ConvenioId}/versiones/{nuevo.VersionId}/confirmar", null, Ct);
        await Cliente.PostAsync($"/api/convenios/{antiguo.ConvenioId}/versiones/{antiguo.VersionId}/confirmar", null, Ct);

        var vigente = await LeerAsync<VersionConvenioVigenteDto>(
            await Cliente.GetAsync("/api/convenios/vigente?rut=78.333.333-3&fecha=2026-04-01", Ct));

        Assert.Equal(antiguo.VersionId, vigente.Version.VersionId);
        Assert.Equal(2, vigente.Candidatos.Count);
        Assert.Contains(nuevo.VersionId, vigente.Candidatos);

        // El cliente es uno solo: se busca por RUT normalizado.
        await using var db = Api.CrearContexto();
        Assert.Equal(1, await db.Clientes.CountAsync(c => c.Rut == "78.333.333-3", Ct));
    }

    [Theory]
    [InlineData(99, "CLP", "2026-01-01", "2026-12-31")]   // servicio inexistente
    [InlineData(42, "UF", "2026-01-01", "2026-12-31")]    // UF no aplica en convenios
    [InlineData(42, "CLP", "2026-12-31", "2026-01-01")]   // vigenciaHasta < vigenciaDesde
    public async Task Crear_rechaza_datos_invalidos_con_400(int tipoServicioId, string moneda, string desde, string hasta)
    {
        var respuesta = await PostJsonAsync("/api/convenios", new
        {
            rut = "76.111.111-6",
            vigenciaDesde = desde,
            vigenciaHasta = hasta,
            servicios = new[] { new { tipoServicioId, tarifa = 1m, moneda } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        await using var db = Api.CrearContexto();
        Assert.Equal(0, await db.Convenios.CountAsync(Ct));
    }

    [Fact]
    public async Task Crear_sin_servicios_o_con_rut_invalido_responde_400()
    {
        var sinServicios = await PostJsonAsync("/api/convenios", new { rut = "76.111.111-6", vigenciaDesde = "2026-01-01", servicios = Array.Empty<object>() });
        var rutInvalido = await PostJsonAsync("/api/convenios", new
        {
            rut = "no-es-rut",
            vigenciaDesde = "2026-01-01",
            servicios = new[] { new { tipoServicioId = 42, tarifa = 1m, moneda = "CLP" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, sinServicios.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rutInvalido.StatusCode);
    }
}
