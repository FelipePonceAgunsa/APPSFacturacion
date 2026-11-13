using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Satelite.Api.Handlers;
using Satelite.Aplicacion.Operaciones;
namespace Satelite.Api.Controllers;
/// <summary>Operaciones facturables (tarea 3.1a). Pantallas F1 Bandeja y F2 Detalle.</summary>
[Route("api/operaciones")]
[Tags("Operaciones")]
public class OperacionesController(IOperacionService operaciones) : SateliteControllerBase
{
    /// <summary>Listar operaciones (por facturable y RUT del cliente facturador o consignatario).</summary>
    [HttpGet(Name = "OperacionesListar")]
    [AccessAuthorize(AccessNames.Operaciones)]
    [ProducesResponseType<List<OperacionResumenDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Listar([FromQuery] bool? facturable, [FromQuery] string? rut, CancellationToken ct) =>
        Responder(await operaciones.ListarAsync(facturable, rut, ct));
    /// <summary>Detalle de una operación: ruta, convenio encontrado, contenedores, servicios y pendientes.</summary>
    [HttpGet("{operacionId:guid}", Name = "OperacionesObtener")]
    [AccessAuthorize(AccessNames.Operaciones)]
    [ProducesResponseType<OperacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Obtener(Guid operacionId, CancellationToken ct) =>
        Responder(await operaciones.ObtenerAsync(operacionId, ct));
    /// <summary>Registrar una operación a mano con el esquema EventoFacturable (puente de la Fase A).</summary>
    [HttpPost(Name = "OperacionesRegistrar")]
    [AccessAuthorize(AccessNames.Operaciones_Edit)]
    [Consumes("application/json")]
    [ProducesResponseType<OperacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<OperacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Registrar([FromBody] JsonElement evento, CancellationToken ct) =>
        Responder(await operaciones.RegistrarAsync(evento, ct));
    /// <summary>Completar o corregir datos de la operación (motivo obligatorio, auditable) y registrar decisiones.</summary>
    [HttpPatch("{operacionId:guid}", Name = "OperacionesCompletar")]
    [AccessAuthorize(AccessNames.Operaciones_Edit)]
    [ProducesResponseType<OperacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Completar(Guid operacionId, [FromBody] CompletarOperacionRequest request, CancellationToken ct) =>
        Responder(await operaciones.CompletarAsync(operacionId, request, ct));
}
