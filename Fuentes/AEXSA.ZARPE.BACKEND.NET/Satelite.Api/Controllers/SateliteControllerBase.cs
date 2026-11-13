using Microsoft.AspNetCore.Mvc;
using Satelite.Aplicacion.Comun;

namespace Satelite.Api.Controllers;

/// <summary>Error de la API según el esquema <c>Error</c> del OpenAPI.</summary>
public sealed record ErrorRespuesta(string Codigo, string Mensaje, string? Detalle);

/// <summary>Base de los controllers del Satélite: traduce <see cref="ServiceResult{T}"/> a la respuesta HTTP.</summary>
[ApiController]
[Produces("application/json")]
public abstract class SateliteControllerBase : ControllerBase
{
    protected ActionResult Responder<T>(ServiceResult<T> result)
    {
        if (result.Success)
        {
            return result.Data is null ? StatusCode(result.StatusCode) : StatusCode(result.StatusCode, result.Data);
        }

        var codigo = result.Codigo ?? result.StatusCode switch
        {
            400 => "INVALIDO",
            404 => "NO_ENCONTRADO",
            409 => "CONFLICTO",
            _ => "ERROR",
        };
        return StatusCode(result.StatusCode, new ErrorRespuesta(codigo, result.Message ?? codigo, result.Detalle));
    }
}
