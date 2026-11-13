using System.Globalization;

namespace Satelite.Dominio.Motor.Reglas;

/// <summary>
/// Regla 7 — Ajuste manual (ADR-008, nivel 3; REQ-006). Va al final y tiene la última palabra.
/// El monto lo fija una persona (nunca la IA); queda registrado {usuario, fecha, motivo, valorAnterior, valorNuevo}.
/// </summary>
internal static class R7AjusteManual
{
    public const string Regla = "R7_AJUSTE_MANUAL";
    public const int Version = 1;

    public static ResultadoLinea Aplicar(ResultadoLinea linea, string usuario, DateTimeOffset fecha, string motivo, decimal nuevoMonto)
    {
        if (string.IsNullOrWhiteSpace(usuario))
        {
            throw new ArgumentException("El ajuste manual exige el usuario que lo hace.", nameof(usuario));
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("El ajuste manual exige un motivo.", nameof(motivo));
        }

        var ajuste = new AjusteManual(usuario, fecha, motivo.Trim(), linea.MontoClp, nuevoMonto);
        var regla = new ReglaAplicada(Regla, Version,
        [
            new DatoRegla("usuario", usuario),
            new DatoRegla("fecha", fecha.ToString("O", CultureInfo.InvariantCulture)),
            new DatoRegla("motivo", ajuste.Motivo),
            new DatoRegla("valorAnterior", EstadoLinea.Texto(linea.MontoClp)),
            new DatoRegla("valorNuevo", EstadoLinea.Texto(nuevoMonto)),
        ]);

        var memoria = linea.Memoria with
        {
            Reglas = [.. linea.Memoria.Reglas, regla],
            Ajuste = ajuste,
        };
        return linea with { MontoClp = nuevoMonto, Memoria = memoria };
    }
}
