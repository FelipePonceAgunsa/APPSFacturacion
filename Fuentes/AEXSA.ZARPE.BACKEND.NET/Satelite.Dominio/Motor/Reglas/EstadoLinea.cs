using System.Globalization;
using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor.Reglas;

/// <summary>Estado parcial de una línea mientras recorre el pipeline. Solo vive dentro de una llamada a Calcular.</summary>
internal sealed class EstadoLinea
{
    public EstadoLinea(ServicioACobrar servicio)
    {
        Servicio = servicio;
        Cantidad = servicio.Cantidad;
    }

    public ServicioACobrar Servicio { get; }

    /// <summary>Regla 0: refacturación del directo en DP World (RN-079).</summary>
    public bool TarifaCeroPorTerminal { get; set; }

    public decimal Cantidad { get; set; }
    public decimal Tarifa { get; set; }
    public Moneda Moneda { get; set; } = Moneda.CLP;
    public bool TarifaDeConvenio { get; set; }
    public bool EsExcepcion { get; set; }
    public bool SinTarifa { get; set; }
    public bool SinTipoCambio { get; set; }
    public bool PisoActivado { get; set; }
    public string? ArticuloSap { get; set; }

    /// <summary>Tipo de cambio aplicado; null si la tarifa está en CLP.</summary>
    public decimal? TipoCambio { get; set; }

    public decimal Monto { get; set; }
    public List<ReglaAplicada> Reglas { get; } = [];

    /// <summary>Monto en CLP = cantidad × tarifa × tipo de cambio. Cero si falta tarifa o tipo de cambio.</summary>
    public void RecalcularMonto()
    {
        Monto = SinTarifa || SinTipoCambio ? 0m : Cantidad * Tarifa * (TipoCambio ?? 1m);
    }

    public void Registrar(string regla, int version, params (string Clave, object? Valor)[] datos)
    {
        Reglas.Add(new ReglaAplicada(regla, version, datos.Select(d => new DatoRegla(d.Clave, Texto(d.Valor))).ToList()));
    }

    internal static string Texto(object? valor) => valor switch
    {
        null => string.Empty,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
