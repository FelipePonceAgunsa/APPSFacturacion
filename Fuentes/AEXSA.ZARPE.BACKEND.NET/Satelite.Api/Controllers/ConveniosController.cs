using Microsoft.AspNetCore.Mvc;
using Satelite.Api.Handlers;
using Satelite.Aplicacion.Convenios;
using Satelite.Dominio.Comun;

namespace Satelite.Api.Controllers;

/// <summary>Convenios versionados por formulario manual (tarea 1.2). Pantallas C1 a C4.</summary>
[Route("api/convenios")]
[Tags("Convenios")]
public class ConveniosController(IConvenioService convenios) : SateliteControllerBase
{
    /// <summary>Listar convenios (por RUT del cliente y estado de sus versiones).</summary>
    [HttpGet(Name = "ConveniosListar")]
    [AccessAuthorize(AccessNames.Convenios)]
    [ProducesResponseType<List<ConvenioResumenDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Listar([FromQuery] string? rut, [FromQuery] EstadoVersionConvenio? estado, CancellationToken ct) =>
        Responder(await convenios.ListarAsync(rut, estado, ct));

    /// <summary>Versión de convenio en vigor para un RUT a una fecha (RN-018: con varios, el más antiguo).</summary>
    [HttpGet("vigente", Name = "ConveniosVigente")]
    [AccessAuthorize(AccessNames.Convenios, AccessNames.Operaciones)]
    [ProducesResponseType<VersionConvenioVigenteDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Vigente([FromQuery] string rut, [FromQuery] DateOnly fecha, CancellationToken ct) =>
        Responder(await convenios.ObtenerVigenteAsync(rut, fecha, ct));

    /// <summary>Convenio con todas sus versiones y servicios.</summary>
    [HttpGet("{convenioId:guid}", Name = "ConveniosObtener")]
    [AccessAuthorize(AccessNames.Convenios)]
    [ProducesResponseType<ConvenioDetalleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Obtener(Guid convenioId, CancellationToken ct) =>
        Responder(await convenios.ObtenerAsync(convenioId, ct));

    /// <summary>Crear un convenio con su primera versión en BORRADOR (formulario manual, ADR-012).</summary>
    [HttpPost(Name = "ConveniosCrear")]
    [AccessAuthorize(AccessNames.Convenios_Edit)]
    [ProducesResponseType<VersionConvenioDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Crear([FromBody] ConvenioManualRequest request, CancellationToken ct) =>
        Responder(await convenios.CrearAsync(request, ct));

    /// <summary>Crear una versión nueva en BORRADOR (ajuste, renovación o complemento por correo).</summary>
    [HttpPost("{convenioId:guid}/versiones", Name = "ConveniosCrearVersion")]
    [AccessAuthorize(AccessNames.Convenios_Edit)]
    [ProducesResponseType<VersionConvenioDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CrearVersion(Guid convenioId, [FromBody] NuevaVersionConvenioRequest request, CancellationToken ct) =>
        Responder(await convenios.CrearVersionAsync(convenioId, request, ct));

    /// <summary>Confirmar una versión en BORRADOR (humano): pasa a VIGENTE y la anterior a REEMPLAZADA.</summary>
    [HttpPost("{convenioId:guid}/versiones/{versionId:guid}/confirmar", Name = "ConveniosConfirmarVersion")]
    [AccessAuthorize(AccessNames.Convenios_Confirmar)]
    [ProducesResponseType<VersionConvenioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Confirmar(Guid convenioId, Guid versionId, CancellationToken ct) =>
        Responder(await convenios.ConfirmarVersionAsync(convenioId, versionId, ct));
}
