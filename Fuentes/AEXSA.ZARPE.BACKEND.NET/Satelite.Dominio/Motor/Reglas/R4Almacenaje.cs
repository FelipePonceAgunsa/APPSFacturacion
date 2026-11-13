using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor.Reglas;

/// <summary>
/// Regla 4 — Almacenaje (RN-023, RN-051, RN-052). Solo servicios por CTR_DIA. Por contenedor:
/// <c>max(0, días calculados − días libres del convenio − días bajo aforo)</c>, sumado sobre los contenedores
/// (o solo el contenedor del servicio). Sin datos de contenedor, la cantidad del servicio se toma como días calculados.
/// </summary>
internal static class R4Almacenaje
{
    public const string Regla = "R4_ALMACENAJE";
    public const int Version = 1;

    public static void Aplicar(ContextoCalculo ctx, EstadoLinea e)
    {
        if (e.Servicio.Unidad != UnidadCobro.CTR_DIA)
        {
            return;
        }

        var diasLibres = ctx.Convenio?.DiasLibres ?? 0;
        var contenedores = ctx.Contenedores
            .Where(c => c.DiasAlmacenaje is not null)
            .Where(c => e.Servicio.Contenedor is null || c.Numero == e.Servicio.Contenedor)
            .ToList();

        if (contenedores.Count == 0)
        {
            var calculados = e.Servicio.Cantidad;
            e.Cantidad = Math.Max(0m, calculados - diasLibres);
            e.RecalcularMonto();
            e.Registrar(Regla, Version,
                ("origenDias", "CANTIDAD_SERVICIO"),
                ("diasCalculados", calculados),
                ("diasLibres", diasLibres),
                ("diasBajoAforo", 0),
                ("diasCobrados", e.Cantidad),
                ("monto", e.Monto));
            return;
        }

        var total = 0;
        var detalle = new List<string>();
        foreach (var c in contenedores)
        {
            var aforo = c.DiasBajoAforo ?? 0;
            var netos = Math.Max(0, c.DiasAlmacenaje!.Value - diasLibres - aforo);
            total += netos;
            detalle.Add($"{c.Numero}:{c.DiasAlmacenaje.Value}-{diasLibres}-{aforo}={netos}");
        }

        e.Cantidad = total;
        e.RecalcularMonto();
        e.Registrar(Regla, Version,
            ("origenDias", "CONTENEDORES"),
            ("diasCalculados", contenedores.Sum(c => c.DiasAlmacenaje!.Value)),
            ("diasLibres", diasLibres),
            ("diasBajoAforo", contenedores.Sum(c => c.DiasBajoAforo ?? 0)),
            ("porContenedor", string.Join(";", detalle)),
            ("diasCobrados", e.Cantidad),
            ("monto", e.Monto));

        if (contenedores.Any(c => c.DiasAforoCalculados))
        {
            e.Registrar(DiasBajoAforo.Regla, DiasBajoAforo.Version,
                ("conteo", "DIAS_CALENDARIO_UTC_FINALIZADA_MENOS_ASIGNADA"),
                ("supuesto", "POR_CONFIRMAR_NOTA_15"));
        }
    }
}
