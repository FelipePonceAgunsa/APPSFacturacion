using System.Text.Json;
using System.Text.Json.Serialization;
namespace Satelite.Aplicacion.Operaciones;
/// <summary>Serialización JSON de las columnas jsonb y del mensaje de AEXSA (camelCase, enumeraciones como texto).</summary>
public static class JsonSatelite
{
    /// <summary>Para las columnas jsonb que escribe el Satélite.</summary>
    public static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
    /// <summary>
    /// Para leer el mensaje de AEXSA de forma estricta: campos `required` presentes, null solo donde el tipo lo admite,
    /// números como números y enumeraciones solo por nombre.
    /// </summary>
    public static readonly JsonSerializerOptions Estricto = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        NumberHandling = JsonNumberHandling.Strict,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };
    public static string Serializar<T>(T valor) => JsonSerializer.Serialize(valor, Opciones);
    public static T? Leer<T>(string? json) => string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, Opciones);
    /// <summary>Objeto JSON guardado en una columna jsonb (detalle o decisión de un pendiente), o null.</summary>
    public static Dictionary<string, JsonElement>? Objeto(string? json) => Leer<Dictionary<string, JsonElement>>(json);
}
