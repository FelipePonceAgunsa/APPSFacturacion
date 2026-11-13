using Satelite.Dominio.Motor.Reglas;

namespace Satelite.Dominio.Motor;

/// <summary>
/// Motor de tarifas determinístico (ADR-006, motor-tarifas-detalle.md). Recorre las reglas puras en el orden de §1:
/// R0 composición del directo → R1 tarifa → R2 cantidad → R3 moneda → R4 almacenaje → R5 recargos →
/// R6 reglas configurables (vacía en etapa 1) → R7 ajuste manual (aparte, con <see cref="AplicarAjuste"/>).
/// Sin reloj, azar, cultura ni I/O; solo <c>decimal</c> y sin redondeo (no hay regla documentada).
/// Mismo <see cref="ContextoCalculo"/>, mismo resultado.
/// </summary>
public sealed class MotorTarifas
{
    private readonly R1ResolucionTarifa _r1;

    /// <summary>Motor con las fuentes de tarifa de la etapa 1: convenio y pública.</summary>
    public MotorTarifas()
        : this([new FuenteConvenio(), new FuentePublica()])
    {
    }

    /// <summary>Motor con otras fuentes de tarifa, en orden de precedencia (Regla 1 enchufable, §5b).</summary>
    public MotorTarifas(IReadOnlyList<IFuenteTarifa> fuentesTarifa)
    {
        _r1 = new R1ResolucionTarifa(fuentesTarifa);
    }

    public ResultadoCalculo Calcular(ContextoCalculo contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        var (lineas, descartados) = R0ComposicionDirecto.Componer(contexto);
        var resultado = new List<ResultadoLinea>(lineas.Count);
        foreach (var e in lineas)
        {
            _r1.Aplicar(contexto, e);
            R2Cantidad.Aplicar(contexto, e);
            R3ConversionMoneda.Aplicar(contexto, e);
            R4Almacenaje.Aplicar(contexto, e);
            R5Recargos.Aplicar(contexto, e);
            R6ReglasConfigurables.Aplicar(contexto, e);
            resultado.Add(Cerrar(contexto, e));
        }

        return new ResultadoCalculo(resultado, descartados);
    }

    /// <summary>
    /// Regla 7: ajuste manual de monto con motivo obligatorio. Función pura: devuelve la línea con el monto nuevo y la
    /// memoria con el ajuste; la fecha la entrega quien llama (el motor no lee el reloj).
    /// </summary>
    public static ResultadoLinea AplicarAjuste(ResultadoLinea linea, string usuario, DateTimeOffset fecha, string motivo, decimal nuevoMonto)
    {
        ArgumentNullException.ThrowIfNull(linea);
        return R7AjusteManual.Aplicar(linea, usuario, fecha, motivo, nuevoMonto);
    }

    private static ResultadoLinea Cerrar(ContextoCalculo ctx, EstadoLinea e)
    {
        var memoria = new MemoriaLinea(
            e.Reglas.ToList(),
            ctx.Convenio?.VersionConvenioId,
            ctx.Convenio?.Numero,
            ctx.TarifaPublica.Version,
            e.TipoCambio,
            e.PisoActivado,
            Ajuste: null);

        return new ResultadoLinea(
            e.Servicio.TipoServicioId,
            e.Cantidad,
            e.Servicio.Unidad,
            e.Tarifa,
            e.Moneda,
            e.Monto,
            e.EsExcepcion,
            e.SinTarifa,
            e.SinTipoCambio,
            e.ArticuloSap,
            memoria);
    }
}
