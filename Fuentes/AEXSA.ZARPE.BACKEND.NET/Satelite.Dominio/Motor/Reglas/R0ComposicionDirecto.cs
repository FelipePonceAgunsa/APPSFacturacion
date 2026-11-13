using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor.Reglas;

/// <summary>
/// Regla 0 — Composición del contenedor directo sin convenio (RN-078, RN-079; ADR-015).
/// La OFA lleva exactamente tres conceptos: visación, security fee y refacturación, con tarifa pública.
/// No lleva recepción, despacho ni movilización. La refacturación tiene valor en STI y va en cero en DP World.
/// Con convenio vigente manda el convenio (RN-045) y esta regla no aplica.
/// </summary>
internal static class R0ComposicionDirecto
{
    public const string Regla = "R0_COMPOSICION_DIRECTO";
    public const int Version = 1;

    public static bool Aplica(ContextoCalculo ctx) => ctx.Retiro == TipoRetiro.DIRECTO && !ctx.ConConvenio;

    /// <summary>Arma las líneas iniciales. Sin la regla, una línea por servicio prestado.</summary>
    public static (List<EstadoLinea> Lineas, List<int> Descartados) Componer(ContextoCalculo ctx)
    {
        if (!Aplica(ctx))
        {
            return (ctx.Servicios.Select(s => new EstadoLinea(s)).ToList(), []);
        }

        var conceptos = ctx.ConceptosDirecto
            ?? throw new InvalidOperationException(
                "Directo sin convenio: faltan los servicios de la Regla 0 (configuración Motor:ConceptosDirecto).");

        var tres = new[] { conceptos.Visacion, conceptos.SecurityFee, conceptos.Refacturacion };
        var idsTres = tres.Select(c => c.TipoServicioId).ToHashSet();

        // Todo servicio prestado que no sea uno de los tres queda fuera (también un segundo registro del mismo concepto).
        var usados = new HashSet<int>();
        var descartados = new List<int>();
        foreach (var s in ctx.Servicios)
        {
            if (!idsTres.Contains(s.TipoServicioId) || !usados.Add(s.TipoServicioId))
            {
                descartados.Add(s.TipoServicioId);
            }
        }

        var textoDescartados = string.Join(",", descartados);
        var lineas = new List<EstadoLinea>();
        foreach (var concepto in tres)
        {
            var prestado = ctx.Servicios.FirstOrDefault(s => s.TipoServicioId == concepto.TipoServicioId);
            var origen = "PRESTADO";
            if (prestado is null)
            {
                origen = "AGREGADO_REGLA_0";
                prestado = new ServicioACobrar(concepto.TipoServicioId, CantidadPorUnidad(ctx, concepto.Unidad), concepto.Unidad);
            }

            var linea = new EstadoLinea(prestado);
            var esRefacturacion = concepto.TipoServicioId == conceptos.Refacturacion.TipoServicioId;

            // [SUPUESTO: un terminal OTRO cobra la refacturación con tarifa pública, igual que STI; RN-079 solo define
            // STI y DP World. POR CONFIRMAR con facturación]. SIN_MAPEAR no llega aquí: la aplicación deja la operación pendiente.
            linea.TarifaCeroPorTerminal = esRefacturacion && ctx.Terminal == GrupoTerminal.DP_WORLD;
            linea.Registrar(Regla, Version,
                ("concepto", esRefacturacion ? "REFACTURACION" : concepto == conceptos.Visacion ? "VISACION" : "SECURITY_FEE"),
                ("terminal", ctx.Terminal),
                ("origenCantidad", origen),
                ("refacturacionEnCero", linea.TarifaCeroPorTerminal),
                ("serviciosDescartados", textoDescartados));
            lineas.Add(linea);
        }

        return (lineas, descartados);
    }

    /// <summary>Cantidad de un concepto agregado por la regla: BL → cantidad de BL; CTR → contenedores; si no, 1.</summary>
    private static decimal CantidadPorUnidad(ContextoCalculo ctx, UnidadCobro unidad) => unidad switch
    {
        UnidadCobro.BL => ctx.CantidadBls,
        UnidadCobro.CTR => ctx.CantidadContenedores,
        _ => 1m,
    };
}
