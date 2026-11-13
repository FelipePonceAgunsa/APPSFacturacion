using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor.Reglas;

/// <summary>Tarifa encontrada por una fuente.</summary>
public sealed record TarifaResuelta(decimal Tarifa, Moneda Moneda, string Fuente, bool DeConvenio, string? Lista);

/// <summary>
/// Fuente de tarifa enchufable de la Regla 1. Hoy: convenio y pública. La letra tarifaria de carga suelta
/// (2ª etapa, ADR-013) entra como una fuente más (motor-tarifas-detalle.md §5b).
/// </summary>
public interface IFuenteTarifa
{
    TarifaResuelta? Resolver(ContextoCalculo ctx, int tipoServicioId);
}

/// <summary>Tarifa del convenio vigente (precedencia del convenio, RN-045).</summary>
public sealed class FuenteConvenio : IFuenteTarifa
{
    public TarifaResuelta? Resolver(ContextoCalculo ctx, int tipoServicioId) =>
        ctx.Convenio is { } c && c.Tarifas.TryGetValue(tipoServicioId, out var t)
            ? new TarifaResuelta(t.Tarifa, t.Moneda, "CONVENIO", DeConvenio: true, Lista: null)
            : null;
}

/// <summary>Tarifa pública / lista spot de SAP (RN-014).</summary>
public sealed class FuentePublica : IFuenteTarifa
{
    public TarifaResuelta? Resolver(ContextoCalculo ctx, int tipoServicioId) =>
        ctx.TarifaPublica.Tarifas.TryGetValue(tipoServicioId, out var t)
            ? new TarifaResuelta(t.Precio, t.Moneda, "PUBLICA", DeConvenio: false, Lista: t.Lista)
            : null;
}

/// <summary>
/// Regla 1 — Resolución de tarifa (RN-006, RN-014, RN-045): convenio → pública. Un servicio fuera del convenio
/// usa la pública y queda marcado como excepción. Sin tarifa en ninguna fuente: tarifa 0 y <c>SinTarifa</c>.
/// </summary>
internal sealed class R1ResolucionTarifa
{
    public const string Regla = "R1_RESOLUCION_TARIFA";
    public const int Version = 1;

    private readonly IReadOnlyList<IFuenteTarifa> _fuentes;

    public R1ResolucionTarifa(IReadOnlyList<IFuenteTarifa> fuentes) => _fuentes = fuentes;

    public void Aplicar(ContextoCalculo ctx, EstadoLinea e)
    {
        var id = e.Servicio.TipoServicioId;
        e.ArticuloSap = ctx.ArticulosSap.TryGetValue(id, out var articulo)
            ? articulo
            : ctx.TarifaPublica.Tarifas.TryGetValue(id, out var pub) ? pub.ArticuloSap : null;

        if (e.TarifaCeroPorTerminal)
        {
            e.Tarifa = 0m;
            e.Moneda = Moneda.CLP;
            e.Registrar(Regla, Version, ("fuente", "REGLA_0_DP_WORLD"), ("tarifa", 0m), ("moneda", Moneda.CLP),
                ("esExcepcion", false), ("versionTarifaPublica", ctx.TarifaPublica.Version));
            return;
        }

        var resuelta = _fuentes.Select(f => f.Resolver(ctx, id)).FirstOrDefault(t => t is not null);
        if (resuelta is null)
        {
            e.SinTarifa = true;
            e.Tarifa = 0m;
            e.Moneda = Moneda.CLP;
            e.Registrar(Regla, Version, ("fuente", "SIN_TARIFA"), ("tarifa", 0m), ("moneda", Moneda.CLP),
                ("esExcepcion", false), ("versionTarifaPublica", ctx.TarifaPublica.Version));
            return;
        }

        e.Tarifa = resuelta.Tarifa;
        e.Moneda = resuelta.Moneda;
        e.TarifaDeConvenio = resuelta.DeConvenio;
        e.EsExcepcion = ctx.ConConvenio && !resuelta.DeConvenio;
        e.Registrar(Regla, Version,
            ("fuente", resuelta.Fuente),
            ("tarifa", resuelta.Tarifa),
            ("moneda", resuelta.Moneda),
            ("esExcepcion", e.EsExcepcion),
            ("versionConvenio", resuelta.DeConvenio ? (object?)ctx.Convenio!.Numero : null),
            ("lista", resuelta.Lista),
            ("versionTarifaPublica", resuelta.DeConvenio ? null : (object?)ctx.TarifaPublica.Version));
    }
}
