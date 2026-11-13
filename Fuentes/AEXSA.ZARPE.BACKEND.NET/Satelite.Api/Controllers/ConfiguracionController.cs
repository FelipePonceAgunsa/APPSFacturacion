using Microsoft.AspNetCore.Mvc;
using Satelite.Api.Handlers;
using Satelite.Aplicacion.Configuracion;

namespace Satelite.Api.Controllers;

/// <summary>Parámetros que mantiene el Administrador: terminales (T4) y tipos de inspección.</summary>
[Route("api/configuracion")]
[Tags("Configuracion")]
[AccessAuthorize(AccessNames.Configuracion)]
public class ConfiguracionController(IConfiguracionService configuracion) : SateliteControllerBase
{
    /// <summary>Mapeo de terminales de AEXSA a STI, DP World u otro (RN-079).</summary>
    [HttpGet("terminales", Name = "ConfiguracionTerminales")]
    [ProducesResponseType<List<TerminalMapaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Terminales(CancellationToken ct) => Responder(await configuracion.ListarTerminalesAsync(ct));

    /// <summary>Asignar el grupo de un terminal.</summary>
    [HttpPut("terminales/{terminalId:int}", Name = "ConfiguracionAsignarTerminal")]
    [ProducesResponseType<TerminalMapaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AsignarTerminal(int terminalId, [FromBody] GrupoTerminalRequest request, CancellationToken ct) =>
        Responder(await configuracion.AsignarGrupoTerminalAsync(terminalId, request, ct));

    /// <summary>Mapeo de los tipos de inspección de AEXSA a AFORO_ADUANA, SAG u OTRA (RN-052).</summary>
    [HttpGet("tipos-inspeccion", Name = "ConfiguracionTiposInspeccion")]
    [ProducesResponseType<List<TipoInspeccionMapaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> TiposInspeccion(CancellationToken ct) => Responder(await configuracion.ListarTiposInspeccionAsync(ct));

    /// <summary>Asignar el grupo de un tipo de inspección.</summary>
    [HttpPut("tipos-inspeccion/{codigo}", Name = "ConfiguracionAsignarTipoInspeccion")]
    [ProducesResponseType<TipoInspeccionMapaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> AsignarTipoInspeccion(string codigo, [FromBody] GrupoInspeccionRequest request, CancellationToken ct) =>
        Responder(await configuracion.AsignarGrupoInspeccionAsync(codigo, request, ct));
}
