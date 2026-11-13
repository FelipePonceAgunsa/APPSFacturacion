using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Satelite.Aplicacion.Operaciones;
using Satelite.Dominio.Comun;
using Satelite.Tests.Infraestructura;
namespace Satelite.Tests.Api;
/// <summary>Tarea 3.1a: ingesta del evento de AEXSA v3.0 (contratos-integracion.md §2) y pendientes de la operación.</summary>
[Collection(PostgresCollection.Nombre)]
public class IngestaTests(PostgresFixture fixture) : ApiConOperaciones(fixture)
{
    private async Task<(HttpStatusCode Codigo, ResultadoIngestaDto Cuerpo)> EnviarAsync(JsonNode evento)
    {
        var respuesta = await EnviarEventoAsync(evento);
        return (respuesta.StatusCode, await LeerAsync<ResultadoIngestaDto>(respuesta));
    }
    private async Task<OperacionDto> OperacionAsync(Guid id) =>
        await LeerAsync<OperacionDto>(await Cliente.GetAsync($"/api/operaciones/{id}", Ct));
    [Fact]
    public async Task Mensaje_valido_crea_la_operacion_y_el_repetido_responde_409_YA_RECIBIDO()
    {
        var (codigo, cuerpo) = await EnviarAsync(Evento(98933));
        Assert.Equal(HttpStatusCode.Created, codigo);
        Assert.Equal(ResultadoEvento.APLICADO, cuerpo.Resultado);
        Assert.Equal(1, cuerpo.VersionAplicada);
        var op = await OperacionAsync(cuerpo.OperacionId!.Value);
        Assert.Equal([98933L], op.OperacionIdAexsa);
        Assert.Equal(RutSinConvenio, op.Rut);
        Assert.False(op.ConConvenio);
        Assert.Equal("CONTENEDOR-INDIRECTO-SIN_CONVENIO", op.RutaClasificada);
        Assert.Equal(MomentoFacturacion.EN_VISACION, op.MomentoFacturacion);
        Assert.Equal(GrupoTerminal.STI, op.GrupoTerminal);
        Assert.Equal("BL-98933", Assert.Single(op.Bls).Numero);
        Assert.Empty(op.Pendientes);
        // Días bajo aforo desde la inspección ADU-FIS (21 → 23 = 2) [SUPUESTO nota 15].
        Assert.Equal(2, Assert.Single(op.Contenedores).DiasBajoAforo);
        Assert.Equal(UnidadCobro.CTR_DIA, op.Servicios.Single(s => s.TipoServicioId == 48).Unidad);
        var (repetido, cuerpoRepetido) = await EnviarAsync(Evento(98933));
        Assert.Equal(HttpStatusCode.Conflict, repetido);
        Assert.Equal(ResultadoEvento.DUPLICADO, cuerpoRepetido.Resultado);
        Assert.Equal(IngestaEventoFacturable.YaRecibido, cuerpoRepetido.Codigo);
        await using var db = Api.CrearContexto();
        var eventos = await db.EventosAexsa.Where(e => e.OperacionIdAexsa == "98933").OrderBy(e => e.RecibidoEn).ToListAsync(Ct);
        Assert.Equal([ResultadoEvento.APLICADO, ResultadoEvento.DUPLICADO], eventos.Select(e => e.Resultado));
        Assert.Contains("98933", eventos[0].Carga);
    }
    [Fact]
    public async Task Version_mayor_actualiza_y_las_versiones_atrasadas_se_rechazan_con_VERSION_SUPERADA()
    {
        Assert.Equal(HttpStatusCode.Created, (await EnviarAsync(Evento(500))).Codigo);
        // v3 llega antes que v2: se aplica v3 y v2 queda superada.
        var v3 = Evento(500, version: 3);
        v3["tipoEvento"] = "OPERACION_ACTUALIZADA";
        v3["servicios"]!.AsArray().RemoveAt(4);
        var (codigo3, cuerpo3) = await EnviarAsync(v3);
        Assert.Equal(HttpStatusCode.OK, codigo3);
        Assert.Equal(3, cuerpo3.VersionAplicada);
        var (codigo2, cuerpo2) = await EnviarAsync(Evento(500, version: 2));
        Assert.Equal(HttpStatusCode.Conflict, codigo2);
        Assert.Equal(ResultadoEvento.RECHAZADO, cuerpo2.Resultado);
        Assert.Equal(IngestaEventoFacturable.VersionSuperada, cuerpo2.Codigo);
        Assert.Equal(3, cuerpo2.VersionAplicada);
        var (codigo1, cuerpo1) = await EnviarAsync(Evento(500, version: 1));
        Assert.Equal(HttpStatusCode.Conflict, codigo1);
        Assert.Equal(IngestaEventoFacturable.VersionSuperada, cuerpo1.Codigo);
        var op = await OperacionAsync(cuerpo3.OperacionId!.Value);
        Assert.Equal(3, op.VersionEvento);
        Assert.DoesNotContain(op.Servicios, s => s.TipoServicioId == 49);
    }
    [Fact]
    public async Task Visado_presencial_y_visado_virtual_con_dos_ids_se_rechazan_con_400()
    {
        var presencial = Evento(700);
        presencial["entidad"] = "VISADO_PRESENCIAL";
        presencial["operacionIdAexsa"] = new JsonArray(700, 701);
        var (codigo, cuerpo) = await EnviarAsync(presencial);
        Assert.Equal(HttpStatusCode.BadRequest, codigo);
        Assert.Equal(ResultadoEvento.RECHAZADO, cuerpo.Resultado);
        Assert.Equal(IngestaEventoFacturable.EntidadNoHabilitada, cuerpo.Codigo);
        var dosIds = Evento(800);
        dosIds["operacionIdAexsa"] = new JsonArray(800, 801);
        var (codigoDos, cuerpoDos) = await EnviarAsync(dosIds);
        Assert.Equal(HttpStatusCode.BadRequest, codigoDos);
        Assert.Equal(IngestaEventoFacturable.Invalido, cuerpoDos.Codigo);
        Assert.Contains(cuerpoDos.Errores, e => e.Contains("exactamente un ID"));
        var sinSentido = Evento(900);
        sinSentido.Remove("sentido");
        var (codigoSin, cuerpoSin) = await EnviarAsync(sinSentido);
        Assert.Equal(HttpStatusCode.BadRequest, codigoSin);
        Assert.Contains(cuerpoSin.Errores, e => e.Contains("sentido"));
        var resultado = Evento(901);
        resultado["contenedores"]![0]!["inspecciones"]![0]!["resultado"] = "OTRO";
        Assert.Equal(HttpStatusCode.BadRequest, (await EnviarAsync(resultado)).Codigo);
        await using var db = Api.CrearContexto();
        var rechazados = await db.EventosAexsa.Where(e => e.Resultado == ResultadoEvento.RECHAZADO).ToListAsync(Ct);
        Assert.Equal(4, rechazados.Count);
        Assert.Contains(rechazados, e => e.CodigoError == IngestaEventoFacturable.EntidadNoHabilitada && e.Entidad == EntidadAexsa.VISADO_PRESENCIAL);
        Assert.False(await db.Operaciones.AnyAsync(Ct));
    }
    [Fact]
    public async Task Dos_operaciones_que_comparten_un_documento_quedan_ambas_con_el_pendiente()
    {
        var primera = (await EnviarAsync(Evento(1001))).Cuerpo.OperacionId!.Value;
        var segunda = Evento(1002);
        segunda["bls"] = JsonNode.Parse("""[{ "id": 10010, "numero": "BL-1001" }, { "id": 555, "numero": "BL-OTRO" }]""");
        var idSegunda = (await EnviarAsync(segunda)).Cuerpo.OperacionId!.Value;
        Assert.Contains(TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES, (await OperacionAsync(primera)).Pendientes);
        var op2 = await OperacionAsync(idSegunda);
        var pendiente = op2.PendientesDetalle.Single(p => p.Tipo == TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES);
        Assert.True(pendiente.BloqueaValidacion);
        Assert.True(pendiente.ExigeDecision);
        Assert.Contains(primera.ToString(), pendiente.Detalle!["operaciones"].GetRawText());
    }
    [Fact]
    public async Task Campos_B_vacios_terminal_nulo_e_inspecciones_dejan_sus_pendientes()
    {
        var e = Evento(1100);
        e["tipoRetiro"] = null;
        e["sentido"] = null;
        e["terminal"] = null;
        e["servicios"]![0]!["cantidad"] = null;
        var inspecciones = e["contenedores"]![0]!["inspecciones"]!.AsArray();
        inspecciones[0]!["finalizadaEn"] = null;
        inspecciones[0]!["resultado"] = null;
        inspecciones[1]!["resultado"] = "RECHAZADA_TOTAL";
        inspecciones.Add(new JsonObject { ["tipo"] = "NUEVO-COD", ["asignadaEn"] = null, ["finalizadaEn"] = null, ["resultado"] = null });
        var (codigo, cuerpo) = await EnviarAsync(e);
        Assert.Equal(HttpStatusCode.Created, codigo);
        var op = await OperacionAsync(cuerpo.OperacionId!.Value);
        Assert.Null(op.RutaClasificada);
        Assert.Equal(GrupoTerminal.SIN_MAPEAR, op.GrupoTerminal);
        Assert.Equal(
            [TipoPendiente.TIPO_RETIRO, TipoPendiente.SENTIDO, TipoPendiente.TERMINAL_SIN_MAPEAR, TipoPendiente.CANTIDAD_SERVICIO,
             TipoPendiente.DIAS_AFORO, TipoPendiente.INSPECCION_NO_FINALIZADA, TipoPendiente.INSPECCION_RECHAZADA],
            op.Pendientes);
        Assert.False(op.PendientesDetalle.Single(p => p.Tipo == TipoPendiente.TERMINAL_SIN_MAPEAR).BloqueaValidacion);
        await using var db = Api.CrearContexto();
        Assert.Equal(GrupoInspeccion.SIN_MAPEAR, (await db.TiposInspeccionMapa.SingleAsync(t => t.CodigoAexsa == "NUEVO-COD", Ct)).Grupo);
        // Un terminal nuevo se agrega al mapa como SIN_MAPEAR.
        var otro = Evento(1101);
        otro["terminal"] = new JsonObject { ["id"] = 9, ["nombre"] = "TERMINAL NUEVO" };
        await EnviarAsync(otro);
        Assert.Equal(GrupoTerminal.SIN_MAPEAR, (await db.TerminalesMapa.SingleAsync(t => t.TerminalAexsaId == 9, Ct)).Grupo);
    }
    [Fact]
    public async Task El_convenio_se_busca_por_RUT_y_fecha_y_con_dos_vigentes_se_usa_el_mas_antiguo()
    {
        var antiguo = await CrearConvenioAsync("convenio-76111111-6.json");
        var conConvenio = await OperacionAsync((await EnviarAsync(Evento(1200, RutConvenio))).Cuerpo.OperacionId!.Value);
        Assert.True(conConvenio.ConConvenio);
        Assert.Equal(antiguo.VersionId, conConvenio.Convenio!.VersionConvenioId);
        Assert.Equal(MomentoFacturacion.POST_DESPACHO, conConvenio.MomentoFacturacion);
        Assert.Equal("CONTENEDOR-INDIRECTO-CON_CONVENIO", conConvenio.RutaClasificada);
        // Visado fuera de la vigencia (2026): sin convenio.
        var vencido = Evento(1201, RutConvenio);
        vencido["visadoAprobadoEn"] = "2027-02-01T10:00:00Z";
        Assert.False((await OperacionAsync((await EnviarAsync(vencido)).Cuerpo.OperacionId!.Value)).ConConvenio);
        // Un segundo convenio vigente para el mismo RUT: gana el más antiguo y queda la decisión pendiente.
        await CrearConvenioAsync("convenio-76111111-6.json");
        var dos = await OperacionAsync((await EnviarAsync(Evento(1202, RutConvenio))).Cuerpo.OperacionId!.Value);
        Assert.Equal(antiguo.VersionId, dos.Convenio!.VersionConvenioId);
        Assert.Contains(TipoPendiente.VARIOS_CONVENIOS_VIGENTES, dos.Pendientes);
    }
    [Fact]
    public async Task Bulto_se_guarda_fuera_de_alcance_y_anulada_marca_la_operacion()
    {
        var bulto = Evento(1300);
        bulto["tipoCarga"] = "BULTO";
        var op = await OperacionAsync((await EnviarAsync(bulto)).Cuerpo.OperacionId!.Value);
        Assert.False(op.Facturable);
        Assert.Empty(op.Pendientes);
        var anulada = Evento(1301);
        await EnviarAsync(anulada);
        var v2 = Evento(1301, version: 2);
        v2["estadoOperacion"] = "ANULADA";
        var opAnulada = await OperacionAsync((await EnviarAsync(v2)).Cuerpo.OperacionId!.Value);
        Assert.Equal(EstadoOperacion.ANULADA, opAnulada.EstadoOperacion);
    }
    [Fact]
    public async Task Alta_manual_usa_el_mismo_esquema_e_idempotencia()
    {
        var creada = await PostRawAsync("/api/operaciones", Evento(1400).ToJsonString());
        Assert.Equal(HttpStatusCode.Created, creada.StatusCode);
        var op = await LeerAsync<OperacionDto>(creada);
        var repetida = await PostRawAsync("/api/operaciones", Evento(1400).ToJsonString());
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        Assert.Equal(op.OperacionId, (await LeerAsync<OperacionDto>(repetida)).OperacionId);
        var presencial = Evento(1401);
        presencial["entidad"] = "VISADO_PRESENCIAL";
        var rechazada = await PostRawAsync("/api/operaciones", presencial.ToJsonString());
        Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);
        Assert.Equal(IngestaEventoFacturable.EntidadNoHabilitada, (await LeerAsync<Satelite.Api.Controllers.ErrorRespuesta>(rechazada)).Codigo);
        var lista = await LeerAsync<List<OperacionResumenDto>>(await Cliente.GetAsync($"/api/operaciones?rut={RutSinConvenio}", Ct));
        Assert.Equal(op.OperacionId, Assert.Single(lista).OperacionId);
    }
}
