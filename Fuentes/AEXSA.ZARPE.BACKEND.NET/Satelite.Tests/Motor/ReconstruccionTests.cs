using System.Globalization;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Motor;
using static Satelite.Tests.Motor.MotorTarifasTests;

namespace Satelite.Tests.Motor;

/// <summary>
/// Tarea 1.6 (REQ-011, RNF-01): el monto de cada línea se reconstruye leyendo solo su memoria de cálculo.
/// </summary>
public class ReconstruccionTests
{
    /// <summary>Recalcula el monto con los datos que guardó cada regla en la memoria.</summary>
    private static decimal Reconstruir(MemoriaLinea memoria)
    {
        if (memoria.Ajuste is { } ajuste)
        {
            return ajuste.ValorNuevo;
        }

        static decimal? Num(string? texto) =>
            string.IsNullOrEmpty(texto) ? null : decimal.Parse(texto, CultureInfo.InvariantCulture);
        ReglaAplicada? Regla(string nombre) => memoria.Reglas.SingleOrDefault(r => r.Regla == nombre);

        var tarifa = Num(Regla("R1_RESOLUCION_TARIFA")!.Dato("tarifa"))!.Value;
        var cantidad = Num(Regla("R4_ALMACENAJE")?.Dato("diasCobrados")) ?? Num(Regla("R2_CANTIDAD")!.Dato("cantidad"))!.Value;
        var tc = Num(Regla("R3_CONVERSION_MONEDA")!.Dato("tipoCambioAplicado")) ?? 1m;
        Assert.Equal(memoria.TipoCambioAplicado ?? 1m, tc);

        var monto = cantidad * tarifa * tc;
        var recargos = memoria.Reglas.Where(r => r.Regla == "R5_RECARGO")
            .Sum(r => monto * Num(r.Dato("porcentaje"))!.Value / 100m);
        return monto + recargos;
    }

    private static ContextoCalculo ContextoCompleto() => Contexto(
            new ServicioACobrar(Almacenaje, 15m, UnidadCobro.CTR_DIA),
            new ServicioACobrar(Recepcion, 2m, UnidadCobro.CTR),
            new ServicioACobrar(Sag, 1m, UnidadCobro.CTR),
            new ServicioACobrar(ManipuleoTon, 3m, UnidadCobro.TON, MetrosCubicos: 4.25m, Toneladas: 3.5m)) with
    {
        Convenio = Convenio(tcMinimo: 800m, diasLibres: 5,
            tarifas: [(Almacenaje, 10m, Moneda.USD), (Recepcion, 15_000m, Moneda.CLP), (ManipuleoTon, 2.5m, Moneda.USD)]),
        TipoCambioUsd = 700m,
        Contenedores =
        [
            new CondicionAlmacenaje("ABCD1234567", DiasAlmacenaje: 12, DiasBajoAforo: 2),
            new CondicionAlmacenaje("EFGH7654321", DiasAlmacenaje: 3, DiasBajoAforo: null),
        ],
        Recargos = [new RecargoIndicado(Almacenaje, "FUERA_DE_HORARIO", 15m)],
    };

    [Fact]
    public void Cada_monto_se_reconstruye_desde_su_memoria()
    {
        var resultado = new MotorTarifas().Calcular(ContextoCompleto());

        Assert.Equal(4, resultado.Lineas.Count);
        foreach (var linea in resultado.Lineas)
        {
            Assert.Equal(linea.MontoClp, Reconstruir(linea.Memoria));
        }

        // Almacenaje: (12-5-2) + max(0, 3-5) = 5 días × 10 USD × piso 800 = 40.000, + 15 % = 46.000.
        Assert.Equal(46_000m, resultado.Lineas.Single(l => l.TipoServicioId == Almacenaje).MontoClp);
    }

    [Fact]
    public void Un_monto_ajustado_se_reconstruye_desde_su_memoria()
    {
        var linea = new MotorTarifas().Calcular(ContextoCompleto()).Lineas[0];
        var ajustada = MotorTarifas.AplicarAjuste(linea, "facturador@zarpe.local",
            new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero), "Corrección acordada con el cliente", 41_000m);

        Assert.Equal(41_000m, Reconstruir(ajustada.Memoria));
        Assert.Equal(linea.MontoClp, ajustada.Memoria.Ajuste!.ValorAnterior);
    }
}
