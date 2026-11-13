namespace Satelite.Dominio.Motor.Reglas;

/// <summary>
/// Regla 5 — Recargos (RN-024): 15 % refacturación a terceros, 90 % falso flete, sobrepeso, fuera de horario.
/// Solo se aplica un recargo indicado (hoy llega por instrucción comercial, PQ-020).
/// Cada recargo se calcula sobre el monto antes de recargos (no se componen entre sí).
/// </summary>
internal static class R5Recargos
{
    public const string Regla = "R5_RECARGO";
    public const int Version = 1;

    public static void Aplicar(ContextoCalculo ctx, EstadoLinea e)
    {
        var baseRecargo = e.Monto;
        foreach (var r in ctx.Recargos.Where(r => r.TipoServicioId == e.Servicio.TipoServicioId))
        {
            var recargo = baseRecargo * r.Porcentaje / 100m;
            e.Monto += recargo;
            e.Registrar(Regla, Version,
                ("tipo", r.Tipo),
                ("base", baseRecargo),
                ("porcentaje", r.Porcentaje),
                ("recargo", recargo),
                ("monto", e.Monto));
        }
    }
}
