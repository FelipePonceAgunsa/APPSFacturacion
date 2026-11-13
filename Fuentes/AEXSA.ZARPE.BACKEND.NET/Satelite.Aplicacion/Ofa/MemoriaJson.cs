using Satelite.Aplicacion.Operaciones;
using Satelite.Dominio.Motor;
namespace Satelite.Aplicacion.Ofa;
/// <summary>
/// Formato de <c>memoria_calculo.reglas_aplicadas</c>: <c>[{regla, version, detalle: {clave: valor}}]</c>, en el orden
/// en que se aplicaron. Con esto se reconstruye el monto (REQ-011).
/// </summary>
public static class MemoriaJson
{
    /// <summary>Regla que marca una línea agregada a mano (tarea 2.2).</summary>
    public const string AgregadoManual = "AGREGADO_MANUAL";
    /// <summary>Regla que marca una línea agregada a mano copiada a una versión nueva de cálculo (DBA-7).</summary>
    public const string CopiaRecalculo = "COPIA_RECALCULO";
    public const string ReglaTarifa = "R1_RESOLUCION_TARIFA";
    public const string ReglaMoneda = "R3_CONVERSION_MONEDA";
    public const string ReglaAjuste = "R7_AJUSTE_MANUAL";
    private sealed record ReglaJson(string Regla, int Version, Dictionary<string, string> Detalle);
    public static string Serializar(IEnumerable<ReglaAplicada> reglas) =>
        JsonSatelite.Serializar(reglas.Select(r => new ReglaJson(
            r.Regla, r.Version, r.Detalle.GroupBy(d => d.Clave).ToDictionary(g => g.Key, g => g.Last().Valor))).ToList());
    public static List<ReglaAplicada> Leer(string? json) =>
        (JsonSatelite.Leer<List<ReglaJson>>(json) ?? [])
            .Select(r => new ReglaAplicada(r.Regla, r.Version, r.Detalle.Select(d => new DatoRegla(d.Key, d.Value)).ToList()))
            .ToList();
    public static ReglaAplicada Regla(string regla, params (string Clave, string? Valor)[] datos) =>
        new(regla, 1, datos.Select(d => new DatoRegla(d.Clave, d.Valor ?? string.Empty)).ToList());
    /// <summary>Fuente de la tarifa según la Regla 1 (CONVENIO, PUBLICA, MANUAL, SIN_TARIFA, REGLA_0_DP_WORLD).</summary>
    public static string? Fuente(IEnumerable<ReglaAplicada> reglas) => reglas.LastOrDefault(r => r.Regla == ReglaTarifa)?.Dato("fuente");
    public static string? Lista(IEnumerable<ReglaAplicada> reglas)
    {
        var lista = reglas.LastOrDefault(r => r.Regla == ReglaTarifa)?.Dato("lista");
        return string.IsNullOrEmpty(lista) ? null : lista;
    }
    public static bool SinTarifa(IEnumerable<ReglaAplicada> reglas) => Fuente(reglas) == "SIN_TARIFA";
    public static bool SinTipoCambio(IEnumerable<ReglaAplicada> reglas) =>
        reglas.LastOrDefault(r => r.Regla == ReglaMoneda)?.Dato("sinTipoCambio") == "true";
    public static bool ConAjuste(IEnumerable<ReglaAplicada> reglas) => reglas.Any(r => r.Regla == ReglaAjuste);
}
