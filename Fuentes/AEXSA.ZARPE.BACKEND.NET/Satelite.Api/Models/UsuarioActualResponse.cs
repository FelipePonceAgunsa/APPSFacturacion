namespace Satelite.Api.Models;

/// <summary>
/// Datos del usuario autenticado, con sus roles y accesos del Satélite.
/// El frontend lo usa para el menú, las rutas y la directiva *hasAccess.
/// </summary>
public sealed record UsuarioActualResponse(
    string Sub,
    string? UserName,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Access);
