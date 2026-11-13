using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Satelite.Api.Models;
using Satelite.Aplicacion.Comun;

namespace Satelite.Api.Controllers;

/// <summary>
/// Sesión del usuario. El inicio y cierre de sesión ocurren en Cognito (Authorization Code + PKCE);
/// la API solo informa quién es el usuario y qué puede hacer.
/// </summary>
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ApiController]
public class SessionController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("Me", Name = "SessionMe")]
    [ProducesResponseType(typeof(UsuarioActualResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public ActionResult<UsuarioActualResponse> Me()
    {
        return Ok(new UsuarioActualResponse(
            currentUser.Sub ?? string.Empty,
            currentUser.UserName,
            currentUser.Email,
            currentUser.Roles,
            currentUser.Access));
    }
}
