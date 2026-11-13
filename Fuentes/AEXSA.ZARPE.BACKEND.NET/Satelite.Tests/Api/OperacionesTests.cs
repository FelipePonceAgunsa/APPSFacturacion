using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Satelite.Aplicacion.Operaciones;
using Satelite.Dominio.Comun;
using Satelite.Tests.Infraestructura;
namespace Satelite.Tests.Api;
/// <summary>Tareas 3.1a y 2.2: completar datos con motivo (PATCH), decisiones y corrección del tipo de retiro.</summary>
[Collection(PostgresCollection.Nombre)]
public class OperacionesTests(PostgresFixture fixture) : ApiConOperaciones(fixture)
{
    private async Task<Guid> IngresarAsync(JsonNode evento)
    {
        var respuesta = await EnviarEventoAsync(evento);
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync(Ct));
        return (await LeerAsync<ResultadoIngestaDto>(respuesta)).OperacionId!.Value;
    }
    [Fact]
    public async Task Completar_sin_motivo_responde_400_y_con_motivo_resuelve_el_pendiente_con_auditoria()
    {
        var e = Evento(2000);
        e["tipoRetiro"] = null;
        var id = await IngresarAsync(e);
        var sinMotivo = await PatchJsonAsync($"/api/operaciones/{id}", new { motivo = "  ", tipoRetiro = "INDIRECTO" });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        var respuesta = await PatchJsonAsync($"/api/operaciones/{id}", new { motivo = "Confirmado con el agente", tipoRetiro = "INDIRECTO" });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var op = await LeerAsync<OperacionDto>(respuesta);
        Assert.Equal(TipoRetiro.INDIRECTO, op.TipoRetiro);
        Assert.DoesNotContain(TipoPendiente.TIPO_RETIRO, op.Pendientes);
        var resuelto = op.PendientesDetalle.Single(p => p.Tipo == TipoPendiente.TIPO_RETIRO);
        Assert.Equal(ResolucionPendiente.DATO_COMPLETADO, resuelto.Resolucion);
        Assert.Equal("admin.local@zarpe.local", resuelto.ResueltoPor);
        Assert.Equal("CONTENEDOR-INDIRECTO-SIN_CONVENIO", op.RutaClasificada);
        await using var db = Api.CrearContexto();
        var auditoria = await db.EventosAuditoria.SingleAsync(a => a.EntidadId == id && a.Accion == OperacionService.AccionCompletar, Ct);
        Assert.Contains("Confirmado con el agente", auditoria.ValorNuevo);
        Assert.Contains("tipoRetiro", auditoria.ValorAnterior);
        // Un dato completado a mano no se pisa con vacío en la versión siguiente de AEXSA.
        var v2 = Evento(2000, version: 2);
        v2["tipoRetiro"] = null;
        await IngresarAsync(v2);
        var despues = await LeerAsync<OperacionDto>(await Cliente.GetAsync($"/api/operaciones/{id}", Ct));
        Assert.Equal(TipoRetiro.INDIRECTO, despues.TipoRetiro);
        Assert.DoesNotContain(TipoPendiente.TIPO_RETIRO, despues.Pendientes);
    }
    [Fact]
    public async Task Con_dos_convenios_vigentes_el_facturador_decide_cual_usar()
    {
        var antiguo = await CrearConvenioAsync("convenio-76111111-6.json");
        var nuevo = await CrearConvenioAsync("convenio-76111111-6.json");
        var id = await IngresarAsync(Evento(2200, RutConvenio));
        var sinValor = await PatchJsonAsync($"/api/operaciones/{id}", new
        {
            motivo = "Correo del comercial",
            decisiones = new[] { new { tipo = "VARIOS_CONVENIOS_VIGENTES", valor = Guid.NewGuid().ToString() } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, sinValor.StatusCode);
        var respuesta = await PatchJsonAsync($"/api/operaciones/{id}", new
        {
            motivo = "Correo del comercial",
            decisiones = new[] { new { tipo = "VARIOS_CONVENIOS_VIGENTES", valor = nuevo.ConvenioId.ToString() } },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var op = await LeerAsync<OperacionDto>(respuesta);
        Assert.NotEqual(antiguo.VersionId, op.Convenio!.VersionConvenioId);
        Assert.Equal(nuevo.VersionId, op.Convenio.VersionConvenioId);
        Assert.DoesNotContain(TipoPendiente.VARIOS_CONVENIOS_VIGENTES, op.Pendientes);
        var decidido = op.PendientesDetalle.Single(p => p.Tipo == TipoPendiente.VARIOS_CONVENIOS_VIGENTES);
        Assert.Equal(ResolucionPendiente.DECISION, decidido.Resolucion);
        Assert.Equal("Correo del comercial", decidido.Motivo);
    }
    [Fact]
    public async Task Completar_cantidad_y_dias_de_aforo_por_contenedor()
    {
        var e = Evento(2300);
        e["servicios"]![3]!["cantidad"] = null;
        e["contenedores"]![0]!["inspecciones"]![0]!["finalizadaEn"] = null;
        var id = await IngresarAsync(e);
        var respuesta = await PatchJsonAsync($"/api/operaciones/{id}", new
        {
            motivo = "Datos del terminal",
            contenedor = "ABCD1234567",
            diasAforo = 3,
            cantidadesServicio = new[] { new { tipoServicioId = 48, cantidad = 15m } },
        });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var op = await LeerAsync<OperacionDto>(respuesta);
        Assert.Equal(3, op.Contenedores.Single().DiasBajoAforo);
        Assert.Equal(15m, op.Servicios.Single(s => s.TipoServicioId == 48).Cantidad);
        Assert.DoesNotContain(TipoPendiente.CANTIDAD_SERVICIO, op.Pendientes);
        Assert.DoesNotContain(TipoPendiente.DIAS_AFORO, op.Pendientes);
        // La inspección sigue sin terminar: ese pendiente lo cierra AEXSA, no el facturador.
        Assert.Contains(TipoPendiente.INSPECCION_NO_FINALIZADA, op.Pendientes);
    }
}
