using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor;

/// <summary>Resultado del motor: las líneas valorizadas y los servicios que la Regla 0 dejó fuera.</summary>
public sealed record ResultadoCalculo(
    IReadOnlyList<ResultadoLinea> Lineas,
    IReadOnlyList<int> ServiciosDescartados);

/// <summary>
/// Línea valorizada con su memoria (motor-tarifas-detalle.md §3). Sin redondeo: no hay regla documentada.
/// </summary>
public sealed record ResultadoLinea(
    int TipoServicioId,
    decimal Cantidad,
    UnidadCobro Unidad,
    decimal Tarifa,
    Moneda Moneda,
    decimal MontoClp,
    bool EsExcepcion,
    bool SinTarifa,
    bool SinTipoCambio,
    string? ArticuloSap,
    MemoriaLinea Memoria);

/// <summary>Dato de una regla aplicada, como texto con cultura invariante (los decimales conservan su escala).</summary>
public sealed record DatoRegla(string Clave, string Valor);

/// <summary>Regla aplicada a una línea, con su versión y los datos que usó.</summary>
public sealed record ReglaAplicada(string Regla, int Version, IReadOnlyList<DatoRegla> Detalle)
{
    /// <summary>Valor de un dato del detalle, o null si no está.</summary>
    public string? Dato(string clave) => Detalle.FirstOrDefault(d => d.Clave == clave)?.Valor;
}

/// <summary>Ajuste manual de monto (nivel 3, REQ-006): lo fija una persona, queda como dato inmutable.</summary>
public sealed record AjusteManual(
    string Usuario,
    DateTimeOffset Fecha,
    string Motivo,
    decimal ValorAnterior,
    decimal ValorNuevo);

/// <summary>
/// Memoria de cálculo de una línea (REQ-011, RNF-01): permite reconstruir el monto.
/// Se guarda append-only en <c>memoria_calculo</c>.
/// </summary>
public sealed record MemoriaLinea(
    IReadOnlyList<ReglaAplicada> Reglas,
    Guid? VersionConvenioId,
    int? VersionConvenio,
    int? VersionTarifaPublica,
    decimal? TipoCambioAplicado,
    bool PisoActivado,
    AjusteManual? Ajuste);
