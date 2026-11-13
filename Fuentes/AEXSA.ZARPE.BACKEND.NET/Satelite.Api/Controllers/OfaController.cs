using Microsoft.AspNetCore.Mvc;
using Satelite.Api.Handlers;
using Satelite.Aplicacion.Ofa;
namespace Satelite.Api.Controllers;
/// <summary>Prefactura (OFA): cálculo, ítem manual, ajuste de monto, validación y envío (tareas 2.2 y 2.3). Pantallas F3, F4 y F6.</summary>
[Tags("Ofa")]
public class OfaController(IOfaService ofas) : SateliteControllerBase
{
    /// <summary>Calcular la OFA de una operación (motor determinístico). Si ya existe en BORRADOR o VALIDADA, se recalcula.</summary>
    [HttpPost("api/operaciones/{operacionId:guid}/ofa", Name = "OfaCalcular")]
    [AccessAuthorize(AccessNames.Ofa)]
    [ProducesResponseType<OfaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Calcular(Guid operacionId, CancellationToken ct) =>
        Responder(await ofas.CalcularAsync(operacionId, ct));
    /// <summary>OFA con sus líneas vigentes, destinatarios, memoria de cálculo y kit sugerido.</summary>
    [HttpGet("api/ofa/{ofaId:guid}", Name = "OfaObtener")]
    [AccessAuthorize(AccessNames.Ofa)]
    [ProducesResponseType<OfaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Obtener(Guid ofaId, CancellationToken ct) =>
        Responder(await ofas.ObtenerAsync(ofaId, ct));
    /// <summary>Agregar a mano un servicio que el cálculo no incluyó (motivo obligatorio).</summary>
    [HttpPost("api/ofa/{ofaId:guid}/lineas", Name = "OfaAgregarLinea")]
    [AccessAuthorize(AccessNames.Ofa_AjusteManual)]
    [ProducesResponseType<LineaOfaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AgregarLinea(Guid ofaId, [FromBody] AgregarLineaRequest request, CancellationToken ct) =>
        Responder(await ofas.AgregarLineaAsync(ofaId, request, ct));
    /// <summary>Ajuste manual auditable del monto de una línea (REQ-006, nivel 3). La IA no fija el monto.</summary>
    [HttpPost("api/ofa/{ofaId:guid}/lineas/{lineaId:guid}/ajuste", Name = "OfaAjustarLinea")]
    [AccessAuthorize(AccessNames.Ofa_AjusteManual)]
    [ProducesResponseType<LineaOfaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AjustarLinea(Guid ofaId, Guid lineaId, [FromBody] AjusteManualRequest request, CancellationToken ct) =>
        Responder(await ofas.AjustarLineaAsync(ofaId, lineaId, request, ct));
    /// <summary>Validar la completitud de la OFA (REQ-008). Si está completa pasa a VALIDADA: lista para digitar en SAP.</summary>
    [HttpPost("api/ofa/{ofaId:guid}/validar", Name = "OfaValidar")]
    [AccessAuthorize(AccessNames.Ofa)]
    [ProducesResponseType<ResultadoValidacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Validar(Guid ofaId, CancellationToken ct) =>
        Responder(await ofas.ValidarAsync(ofaId, ct));
    /// <summary>Enviar la OFA a SAP. Deshabilitado en la etapa 1: responde 409 ENVIO_SAP_DESHABILITADO.</summary>
    [HttpPost("api/ofa/{ofaId:guid}/enviar", Name = "OfaEnviar")]
    [AccessAuthorize(AccessNames.Ofa)]
    [ProducesResponseType<EnvioOfaDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Enviar(Guid ofaId, CancellationToken ct) =>
        Responder(await ofas.EnviarAsync(ofaId, ct));
}
