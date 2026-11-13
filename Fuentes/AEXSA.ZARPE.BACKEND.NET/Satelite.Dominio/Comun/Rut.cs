namespace Satelite.Dominio.Comun;

/// <summary>
/// RUT chileno, identidad canónica del cliente (BR-020).
/// Solo normaliza el formato a <c>12.345.678-9</c> (puntos, guion y DV en mayúscula).
/// No rechaza un dígito verificador incorrecto: no hay regla documentada que lo pida.
/// </summary>
public static class Rut
{
    /// <summary>
    /// Devuelve el RUT con formato canónico, o <c>null</c> si el texto no tiene forma de RUT
    /// (cuerpo de 1 a 8 dígitos y un dígito verificador 0-9 o K).
    /// </summary>
    public static string? Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var limpio = new string(texto.Where(c => !char.IsWhiteSpace(c) && c != '.' && c != '-').ToArray())
            .ToUpperInvariant();
        if (limpio.Length < 2)
        {
            return null;
        }

        var cuerpo = limpio[..^1].TrimStart('0');
        var dv = limpio[^1];
        if (cuerpo.Length is 0 or > 8 || !cuerpo.All(char.IsAsciiDigit) || !(char.IsAsciiDigit(dv) || dv == 'K'))
        {
            return null;
        }

        var grupos = new List<string>();
        for (var fin = cuerpo.Length; fin > 0; fin -= 3)
        {
            var inicio = Math.Max(0, fin - 3);
            grupos.Insert(0, cuerpo[inicio..fin]);
        }

        return $"{string.Join('.', grupos)}-{dv}";
    }
}
