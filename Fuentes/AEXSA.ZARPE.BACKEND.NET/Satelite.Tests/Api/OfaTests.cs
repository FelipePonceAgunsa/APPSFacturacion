using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Satelite.Api.Controllers;
using Satelite.Aplicacion.Ofa;
using Satelite.Aplicacion.Operaciones;
using Satelite.Dominio.Comun;
using Satelite.Tests.Infraestructura;
namespace Satelite.Tests.Api;
/// <summary>Tareas 2.2, 2.3 y 1.6: cálculo de la OFA con memoria, ítem manual, ajuste de monto, validación y envío.</summary>
[Collection(PostgresCollection.Nombre)]
public class OfaTests(PostgresFixture fixture) : ApiConOperaciones(fixture)
{
    private async Task<Guid> IngresarAsync(JsonNode evento)
    {
        var respuesta = await EnviarEventoAsync(evento);
        Assert.True(respuesta.IsSuccessStatusCode, await respuesta.Content.ReadAsStringAsync(Ct));
        return (await LeerAsync<ResultadoIngestaDto>(respuesta)).OperacionId!.Value;
    }
    private async Task<OfaDto> CalcularAsync(Guid operacionId)
    {
        var respuesta = await Cliente.PostAsync($"/api/operaciones/{operacionId}/ofa", null, Ct);
        Assert.True(respuesta.StatusCode == HttpStatusCode.OK, await respuesta.Content.ReadAsStringAsync(Ct));
        return await LeerAsync<OfaDto>(respuesta);
    }
    private static decimal Dato(LineaOfaDto l, string regla, string clave) =>
        decimal.Parse(l.Memoria.ReglasAplicadas.Last(r => r.Regla == regla).Detalle[clave], CultureInfo.InvariantCulture);
    [Fact]
    public async Task Calcular_con_convenio_deja_lineas_con_memoria_de_convenio_tipo_de_cambio_y_reglas()
    {
        var convenio = await CrearConvenioAsync("convenio-76111111-6.json");
        var ofa = await CalcularAsync(await IngresarAsync(Evento(3000, RutConvenio)));
        Assert.Equal(EstadoOfa.BORRADOR, ofa.Estado);
        Assert.Equal(1, ofa.VersionCalculo);
        var asignacion = Assert.Single(ofa.Asignaciones);
        Assert.Equal(RutConvenio, asignacion.DestinatarioRut);
        Assert.Equal(RolCobro.CONSIGNATARIO, asignacion.Rol);
        var lineas = ofa.Lineas.ToDictionary(l => l.TipoServicioId);
        Assert.Equal(15000m, lineas[42].MontoClp);
        Assert.Equal(13000m, lineas[43].MontoClp);
        // T3: 20 USD con el dólar del 2026-10-26 (700) bajo el piso del convenio (800).
        Assert.Equal(16000m, lineas[45].MontoClp);
        Assert.Equal(800m, lineas[45].Memoria.TipoCambioAplicado);
        Assert.Equal("true", lineas[45].Memoria.ReglasAplicadas.Single(r => r.Regla == "R3_CONVERSION_MONEDA").Detalle["pisoActivado"]);
        // Almacenaje: 15 días − 10 libres − 2 bajo aforo = 3 × 8.000.
        Assert.Equal(3m, lineas[48].Cantidad);
        Assert.Equal(24000m, lineas[48].MontoClp);
        Assert.Contains(lineas[48].Memoria.ReglasAplicadas, r => r.Regla == "R4_DIAS_AFORO");
        // SAG no está en el convenio: tarifa pública como excepción.
        Assert.True(lineas[49].EsExcepcion);
        Assert.Equal("PUBLICA", lineas[49].FuenteTarifa);
        Assert.Equal("LP-PUBLICA", lineas[49].ListaPrecios);
        Assert.Equal(25000m, lineas[49].MontoClp);
        Assert.All(ofa.Lineas, l =>
        {
            Assert.Equal(convenio.VersionId, l.Memoria.VersionConvenioId);
            Assert.Equal(1, l.Memoria.VersionConvenio);
            Assert.Equal(1, l.Memoria.VersionTarifaPublica);
            Assert.NotNull(l.ArticuloSap);
            Assert.Contains(l.Memoria.ReglasAplicadas, r => r.Regla == "R1_RESOLUCION_TARIFA");
        });
        Assert.Equal(93000m, ofa.TotalClp);
        await using var db = Api.CrearContexto();
        Assert.Equal(5, await db.MemoriasCalculo.CountAsync(Ct));
    }
    [Fact]
    public async Task Directo_sin_convenio_lleva_tres_conceptos_y_la_refacturacion_va_en_cero_en_DP_World()
    {
        var sti = await CalcularAsync(await IngresarAsync(EventoDirecto(3100, terminal: 1)));
        var dpw = await CalcularAsync(await IngresarAsync(EventoDirecto(3101, terminal: 2)));
        Assert.Equal([45, 46, 47], sti.Lineas.Select(l => l.TipoServicioId).Order());
        Assert.Equal([45, 46, 47], dpw.Lineas.Select(l => l.TipoServicioId).Order());
        Assert.Equal(30000m, sti.Lineas.Single(l => l.TipoServicioId == 47).MontoClp);
        Assert.Equal(0m, dpw.Lineas.Single(l => l.TipoServicioId == 47).MontoClp);
        // Visación pública 23 USD con el dólar del día (700): el piso es solo del convenio.
        Assert.Equal(16100m, sti.Lineas.Single(l => l.TipoServicioId == 45).MontoClp);
        Assert.Equal("SPOT-DIRECTO", sti.KitSugerido!.CodigoKit);
        Assert.All(sti.Lineas, l => Assert.True(l.EnKitSap));
    }
    [Fact]
    public async Task Corregir_DIRECTO_a_INDIRECTO_recalcula_la_OFA_con_otros_conceptos_T10c()
    {
        var id = await IngresarAsync(EventoDirecto(2100, terminal: 1));
        var calculo = await Cliente.PostAsync($"/api/operaciones/{id}/ofa", null, Ct);
        Assert.Equal(HttpStatusCode.OK, calculo.StatusCode);
        var directo = await LeerAsync<OfaDto>(calculo);
        Assert.Equal([45, 46, 47], directo.Lineas.Select(l => l.TipoServicioId).Order());
        var respuesta = await PatchJsonAsync($"/api/operaciones/{id}", new { motivo = "El retiro era indirecto", tipoRetiro = "INDIRECTO" });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var indirecto = await LeerAsync<OfaDto>(await Cliente.GetAsync($"/api/ofa/{directo.OfaId}", Ct));
        Assert.Equal(2, indirecto.VersionCalculo);
        Assert.Equal([42, 43, 45], indirecto.Lineas.Select(l => l.TipoServicioId).Order());
        await using var db = Api.CrearContexto();
        var auditoria = await db.EventosAuditoria.SingleAsync(a => a.EntidadId == id && a.Accion == OperacionService.AccionCompletar, Ct);
        Assert.Contains("DIRECTO", auditoria.ValorAnterior);
        Assert.Contains("INDIRECTO", auditoria.ValorNuevo);
        // Las líneas de la versión 1 se conservan en el histórico.
        Assert.Equal(3, await db.LineasOfa.CountAsync(l => l.OfaId == directo.OfaId && l.VersionCalculo == 1, Ct));
    }
    [Fact]
    public async Task Terminal_sin_mapear_en_directo_sin_convenio_no_se_calcula()
    {
        var e = EventoDirecto(3200, terminal: 1);
        e["terminal"] = null;
        var respuesta = await Cliente.PostAsync($"/api/operaciones/{await IngresarAsync(e)}/ofa", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Equal(ConstructorContexto.TerminalSinMapear, (await LeerAsync<ErrorRespuesta>(respuesta)).Codigo);
        // Un terminal nuevo queda SIN_MAPEAR; al mapearlo en T4 la operación se calcula sin volver a ingresarla.
        var nuevo = EventoDirecto(3201, terminal: 1);
        nuevo["terminal"] = new JsonObject { ["id"] = 9, ["nombre"] = "TERMINAL NUEVO" };
        var id = await IngresarAsync(nuevo);
        Assert.Equal(HttpStatusCode.Conflict, (await Cliente.PostAsync($"/api/operaciones/{id}/ofa", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await PutJsonAsync("/api/configuracion/terminales/9", new { grupo = "DP_WORLD" })).StatusCode);
        var ofa = await CalcularAsync(id);
        Assert.Equal(0m, ofa.Lineas.Single(l => l.TipoServicioId == 47).MontoClp);
        var op = await LeerAsync<OperacionDto>(await Cliente.GetAsync($"/api/operaciones/{id}", Ct));
        Assert.DoesNotContain(TipoPendiente.TERMINAL_SIN_MAPEAR, op.Pendientes);
    }
    [Fact]
    public async Task Item_manual_ajuste_validacion_y_envio()
    {
        var ofa = await CalcularAsync(await IngresarAsync(Evento(3300)));
        var total = ofa.TotalClp;
        // Ítem agregado a mano con tarifa de la lista pública (uso de área va en UF y no hay UF ese día: se informa la tarifa).
        var sinMotivo = await PostJsonAsync($"/api/ofa/{ofa.OfaId}/lineas", new { tipoServicioId = 44, cantidad = 2m, motivo = " " });
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);
        var agregada = await PostJsonAsync($"/api/ofa/{ofa.OfaId}/lineas", new { tipoServicioId = 44, cantidad = 2m, motivo = "Movilización extra pedida por el cliente" });
        Assert.Equal(HttpStatusCode.Created, agregada.StatusCode);
        var manual = await LeerAsync<LineaOfaDto>(agregada);
        Assert.Equal(OrigenLinea.AGREGADA_MANUAL, manual.Origen);
        Assert.Equal("admin.local@zarpe.local", manual.AgregadaPor);
        Assert.Equal(24000m, manual.MontoClp);
        Assert.Equal("AGREGADO_MANUAL", manual.Memoria.ReglasAplicadas[0].Regla);
        // La memoria reconstruye el monto: cantidad × tarifa (CLP).
        Assert.Equal(Dato(manual, "R2_CANTIDAD", "cantidad") * Dato(manual, "R1_RESOLUCION_TARIFA", "tarifa"), Dato(manual, "R3_CONVERSION_MONEDA", "monto"));
        var conItem = await LeerAsync<OfaDto>(await Cliente.GetAsync($"/api/ofa/{ofa.OfaId}", Ct));
        Assert.Equal(total + 24000m, conItem.TotalClp);
        // Ajuste de monto: motivo obligatorio; queda una memoria nueva (append-only) con autor y valores.
        var linea = conItem.Lineas.Single(l => l.TipoServicioId == 42);
        var ajusteSinMotivo = await PostJsonAsync($"/api/ofa/{ofa.OfaId}/lineas/{linea.LineaId}/ajuste", new { nuevoMonto = 10000m, motivo = "" });
        Assert.Equal(HttpStatusCode.BadRequest, ajusteSinMotivo.StatusCode);
        var ajuste = await PostJsonAsync($"/api/ofa/{ofa.OfaId}/lineas/{linea.LineaId}/ajuste", new { nuevoMonto = 10000m, motivo = "Descuento comercial acordado" });
        Assert.Equal(HttpStatusCode.OK, ajuste.StatusCode);
        var ajustada = await LeerAsync<LineaOfaDto>(ajuste);
        Assert.Equal(10000m, ajustada.MontoClp);
        Assert.Equal(2, ajustada.CantidadMemorias);
        Assert.Equal("admin.local@zarpe.local", ajustada.Memoria.AjusteManual!.Usuario);
        Assert.Equal(17000m, ajustada.Memoria.AjusteManual.ValorAnterior);
        Assert.Equal("Descuento comercial acordado", ajustada.Memoria.AjusteManual.Motivo);
        // Validación: completa → VALIDADA. Enviar a SAP está deshabilitado.
        var validacion = await LeerAsync<ResultadoValidacionDto>(await Cliente.PostAsync($"/api/ofa/{ofa.OfaId}/validar", null, Ct));
        Assert.True(validacion.Valida, string.Join(" | ", validacion.Faltantes));
        Assert.Equal(EstadoOfa.VALIDADA, validacion.Estado);
        var envio = await Cliente.PostAsync($"/api/ofa/{ofa.OfaId}/enviar", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, envio.StatusCode);
        Assert.Equal(OfaService.EnvioSapDeshabilitado, (await LeerAsync<ErrorRespuesta>(envio)).Codigo);
        await using var db = Api.CrearContexto();
        Assert.True(await db.EventosAuditoria.AnyAsync(a => a.EntidadId == linea.LineaId && a.Accion == "AJUSTE_MANUAL", Ct));
        Assert.Equal(2, await db.MemoriasCalculo.CountAsync(m => m.LineaOfaId == linea.LineaId, Ct));
    }
    [Fact]
    public async Task Recalcular_mantiene_la_linea_manual_y_no_copia_los_ajustes()
    {
        var ofa = await CalcularAsync(await IngresarAsync(Evento(3400)));
        var manual = await LeerAsync<LineaOfaDto>(await PostJsonAsync($"/api/ofa/{ofa.OfaId}/lineas",
            new { tipoServicioId = 44, cantidad = 1m, tarifa = 5000m, motivo = "Ítem que el sistema no conoce" }));
        Assert.Equal(5000m, manual.MontoClp);
        Assert.Equal("MANUAL", manual.FuenteTarifa);
        await PostJsonAsync($"/api/ofa/{ofa.OfaId}/lineas/{manual.LineaId}/ajuste", new { nuevoMonto = 4000m, motivo = "Rebaja" });
        var calculada = ofa.Lineas.Single(l => l.TipoServicioId == 42);
        await PostJsonAsync($"/api/ofa/{ofa.OfaId}/lineas/{calculada.LineaId}/ajuste", new { nuevoMonto = 1m, motivo = "Prueba" });
        var recalculada = await CalcularAsync(ofa.OperacionId);
        Assert.Equal(2, recalculada.VersionCalculo);
        var copia = recalculada.Lineas.Single(l => l.Origen == OrigenLinea.AGREGADA_MANUAL);
        Assert.Equal(manual.LineaId, copia.LineaOrigenId);
        Assert.Equal(5000m, copia.MontoClp);
        Assert.Null(copia.Memoria.AjusteManual);
        Assert.Contains(copia.Memoria.ReglasAplicadas, r => r.Regla == "COPIA_RECALCULO");
        Assert.Equal(17000m, recalculada.Lineas.Single(l => l.TipoServicioId == 42).MontoClp);
    }
    [Fact]
    public async Task Validar_con_pendientes_lista_lo_que_falta()
    {
        var e = Evento(3500);
        e["contenedores"]![0]!["inspecciones"]![1]!["resultado"] = "RECHAZADA_PARCIAL";
        var id = await IngresarAsync(e);
        var ofa = await CalcularAsync(id);
        var validacion = await LeerAsync<ResultadoValidacionDto>(await Cliente.PostAsync($"/api/ofa/{ofa.OfaId}/validar", null, Ct));
        Assert.False(validacion.Valida);
        Assert.Equal(EstadoOfa.BORRADOR, validacion.Estado);
        Assert.Contains(validacion.Faltantes, f => f.Contains("INSPECCION_RECHAZADA"));
        // Con la decisión registrada, la OFA se recalcula y valida.
        var patch = await PatchJsonAsync($"/api/operaciones/{id}", new
        {
            motivo = "El rechazo parcial no cambia el cobro",
            decisiones = new[] { new { tipo = "INSPECCION_RECHAZADA", valor = ValoresDecision.Continuar } },
        });
        Assert.Equal(HttpStatusCode.OK, patch.StatusCode);
        var segunda = await LeerAsync<ResultadoValidacionDto>(await Cliente.PostAsync($"/api/ofa/{ofa.OfaId}/validar", null, Ct));
        Assert.True(segunda.Valida, string.Join(" | ", segunda.Faltantes));
    }
}
