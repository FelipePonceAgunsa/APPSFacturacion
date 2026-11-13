using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor.Reglas;

/// <summary>
/// Regla 3 — Conversión de moneda (RN-022). CLP: sin conversión. USD: <c>max(TC del día, piso del convenio)</c>.
/// UF: valor de la UF del día. Sin tipo de cambio cargado: monto 0 y marca SIN_TIPO_CAMBIO.
/// [SUPUESTO: el piso (tc_minimo) aplica solo a tarifas del convenio; una tarifa pública de excepción en USD
/// usa el dólar del día, igual que SAP. POR CONFIRMAR con facturación].
/// </summary>
internal static class R3ConversionMoneda
{
    public const string Regla = "R3_CONVERSION_MONEDA";
    public const int Version = 1;

    public static void Aplicar(ContextoCalculo ctx, EstadoLinea e)
    {
        decimal? tcDia = null;
        decimal? piso = null;
        switch (e.Moneda)
        {
            case Moneda.CLP:
                e.TipoCambio = null;
                break;
            case Moneda.USD:
                tcDia = ctx.TipoCambioUsd;
                piso = e.TarifaDeConvenio ? ctx.Convenio?.TcMinimo : null;
                if (tcDia is { } usd)
                {
                    e.PisoActivado = piso is { } p && usd < p;
                    e.TipoCambio = e.PisoActivado ? piso : usd;
                }
                else
                {
                    e.SinTipoCambio = true;
                }

                break;
            case Moneda.UF:
                tcDia = ctx.TipoCambioUf;
                if (tcDia is { } uf)
                {
                    e.TipoCambio = uf;
                }
                else
                {
                    e.SinTipoCambio = true;
                }

                break;
        }

        e.RecalcularMonto();
        e.Registrar(Regla, Version,
            ("moneda", e.Moneda),
            ("tipoCambioDia", tcDia),
            ("tcMinimo", piso),
            ("pisoActivado", e.PisoActivado),
            ("tipoCambioAplicado", e.TipoCambio),
            ("sinTipoCambio", e.SinTipoCambio),
            ("monto", e.Monto));
    }
}
