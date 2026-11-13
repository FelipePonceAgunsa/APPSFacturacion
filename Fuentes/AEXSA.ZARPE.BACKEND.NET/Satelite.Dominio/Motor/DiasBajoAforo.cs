namespace Satelite.Dominio.Motor;

/// <summary>
/// Conteo de los días bajo aforo de un contenedor a partir de su inspección de aforo (RN-052).
/// [SUPUESTO: conteo por fecha calendario UTC, POR CONFIRMAR nota 15 de contratos-integracion.md §2]:
/// no hay regla documentada; se cuenta la diferencia de días calendario entre las fechas UTC de
/// <c>finalizadaEn</c> y <c>asignadaEn</c> (por ejemplo, del 21 al 23 = 2 días).
/// El facturador siempre puede corregir el valor (PATCH diasAforo). En la memoria queda como R4_DIAS_AFORO v1.
/// </summary>
public static class DiasBajoAforo
{
    public const string Regla = "R4_DIAS_AFORO";
    public const int Version = 1;

    /// <summary>Días bajo aforo, o null si la inspección no ha finalizado (queda como pendiente DIAS_AFORO).</summary>
    public static int? Calcular(DateTimeOffset asignadaEn, DateTimeOffset? finalizadaEn)
    {
        if (finalizadaEn is null)
        {
            return null;
        }

        var desde = DateOnly.FromDateTime(asignadaEn.UtcDateTime);
        var hasta = DateOnly.FromDateTime(finalizadaEn.Value.UtcDateTime);
        return Math.Max(0, hasta.DayNumber - desde.DayNumber);
    }
}
