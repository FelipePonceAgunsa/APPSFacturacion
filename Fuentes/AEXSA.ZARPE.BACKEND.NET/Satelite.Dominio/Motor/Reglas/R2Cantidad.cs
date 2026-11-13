using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor.Reglas;

/// <summary>
/// Regla 2 — Cantidad por unidad (RN-047, RN-048). Servicio por DUS → <c>CantidadDus</c> (captura manual);
/// por TON o M3 con ambos datos → el mayor entre m³ y toneladas ("el mayor paga"); si no, la cantidad del servicio.
/// Los días de almacenaje (CTR_DIA) los calcula la Regla 4.
/// </summary>
internal static class R2Cantidad
{
    public const string Regla = "R2_CANTIDAD";
    public const int Version = 1;

    public static void Aplicar(ContextoCalculo ctx, EstadoLinea e)
    {
        var s = e.Servicio;
        string driver;
        if (s.Unidad == UnidadCobro.DUS && ctx.CantidadDus is { } dus)
        {
            e.Cantidad = dus;
            driver = "CANTIDAD_DUS";
        }
        else if (s.Unidad is UnidadCobro.TON or UnidadCobro.M3 && s.MetrosCubicos is { } m3 && s.Toneladas is { } ton)
        {
            e.Cantidad = Math.Max(m3, ton);
            driver = "MAYOR_M3_TON";
        }
        else
        {
            e.Cantidad = s.Cantidad;
            driver = "CANTIDAD_SERVICIO";
        }

        e.Registrar(Regla, Version,
            ("driver", driver),
            ("unidad", s.Unidad),
            ("cantidadServicio", s.Cantidad),
            ("metrosCubicos", s.MetrosCubicos),
            ("toneladas", s.Toneladas),
            ("cantidadDus", ctx.CantidadDus),
            ("cantidad", e.Cantidad));
    }
}
