using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Satelite.Api.Extensions;
using Satelite.Aplicacion.Operaciones;
namespace Satelite.Api.Controllers;
/// <summary>
/// Vía de entrada del evento facturable que envía AEXSA SYS (push, ADR-005; contrato v3.0 en contratos-integracion.md §2).
/// Responde siempre con <c>ResultadoIngesta</c>. Autenticación: HTTP Basic provisorio (credencial de AEXSA en
/// <c>Aexsa:Basic</c>), independiente del login de usuarios [POR CONFIRMAR PA-002: mecanismo definitivo].
/// </summary>
[Route("api/aexsa/eventos")]
[Authorize(Policy = AexsaBasicAuthenticationHandler.Politica)]
[Tags("Callbacks")]
public class AexsaEventosController(IIngestaEventoFacturable ingesta) : SateliteControllerBase
{
    /// <summary>Recepción del evento facturable (idempotente por entidad + operacionIdAexsa + version).</summary>
    /// <param name="evento">EventoFacturable v3.0. Se recibe como JSON crudo para guardarlo tal como llegó, aunque sea inválido.</param>
    [HttpPost(Name = "AexsaEventosRecibir")]
    [Consumes("application/json")]
    [ProducesResponseType<ResultadoIngestaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ResultadoIngestaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ResultadoIngestaDto>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ResultadoIngestaDto>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Recibir([FromBody] JsonElement evento, CancellationToken ct)
    {
        var resultado = await ingesta.IngresarAsync(evento, ct);
        return StatusCode(resultado.StatusCode, resultado.Cuerpo);
    }
}
