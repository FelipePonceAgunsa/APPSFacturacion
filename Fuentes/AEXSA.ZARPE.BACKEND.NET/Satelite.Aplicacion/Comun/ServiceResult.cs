namespace Satelite.Aplicacion.Comun;

/// <summary>
/// Resultado estándar de los servicios (estándar AGUNSA). Los controllers lo traducen con
/// <c>SateliteControllerBase.Responder</c>: si falla, el cuerpo sigue el esquema <c>Error</c> del OpenAPI
/// (<c>{codigo, mensaje, detalle}</c>).
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public int StatusCode { get; set; }

    /// <summary>Código de error del contrato (p. ej. <c>VERSION_NO_EDITABLE</c>).</summary>
    public string? Codigo { get; set; }
    public string? Detalle { get; set; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data, StatusCode = 200 };
    public static ServiceResult<T> Ok() => new() { Success = true, StatusCode = 200 };
    public static ServiceResult<T> Created(T data) => new() { Success = true, Data = data, StatusCode = 201 };
    public static ServiceResult<T> Error(int statusCode, string? message) => new() { Success = false, StatusCode = statusCode, Message = message };

    public static ServiceResult<T> Error(int statusCode, string codigo, string mensaje, string? detalle = null) =>
        new() { Success = false, StatusCode = statusCode, Codigo = codigo, Message = mensaje, Detalle = detalle };

    public static ServiceResult<T> NoEncontrado(string mensaje) => Error(404, "NO_ENCONTRADO", mensaje);
    public static ServiceResult<T> Invalido(string mensaje, string? detalle = null) => Error(400, "INVALIDO", mensaje, detalle);
}
