namespace Satelite.Aplicacion.Comun;

/// <summary>
/// Resultado estándar de los servicios (estándar AGUNSA). El controller lo traduce con
/// <c>StatusCode(result.StatusCode, result.Success ? result.Data : new { message = result.Message })</c>.
/// </summary>
public class ServiceResult<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public int StatusCode { get; set; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data, StatusCode = 200 };
    public static ServiceResult<T> Ok() => new() { Success = true, StatusCode = 200 };
    public static ServiceResult<T> Error(int statusCode, string? message) => new() { Success = false, StatusCode = statusCode, Message = message };
}
