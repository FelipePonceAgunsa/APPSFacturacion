using System.Text.Json;
using System.Text.Json.Serialization;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Motor;

namespace Satelite.Tests.Motor;

/// <summary>
/// Casos T1 a T12 de docs/arquitectura/motor-tarifas-detalle.md §5 (tareas 1.3, 1.4 y 1.5).
/// Datos inventados: servicios 42–52 de los datos de prueba.
/// </summary>
public class MotorTarifasTests
{
    internal const int Recepcion = 42, Despacho = 43, Movilizacion = 44, Visacion = 45, SecurityFee = 46,
        Refacturacion = 47, Almacenaje = 48, Sag = 49, DocExpo = 51, ManipuleoTon = 52;

    internal static readonly TarifaPublicaSnapshot Publica = new(7, new Dictionary<int, TarifaPublica>
    {
        [Recepcion] = new("SRV-REC-001", 17_000m, Moneda.CLP, "LP-PUBLICA"),
        [Despacho] = new("SRV-DES-001", 16_000m, Moneda.CLP, "LP-PUBLICA"),
        [Movilizacion] = new("SRV-MOV-001", 14_000m, Moneda.CLP, "LP-PUBLICA"),
        [Visacion] = new("SRV-VIS-001", 25_000m, Moneda.CLP, "LP-PUBLICA"),
        [SecurityFee] = new("SRV-SECFEE-001", 8_000m, Moneda.CLP, "LP-PUBLICA"),
        [Refacturacion] = new("SRV-REFACT-001", 35_000m, Moneda.CLP, "LP-PUBLICA"),
        [Almacenaje] = new("SRV-ALM-001", 9_000m, Moneda.CLP, "LP-PUBLICA"),
        [Sag] = new("SRV-SAG-001", 21_000m, Moneda.CLP, "LP-PUBLICA"),
    });

    internal static readonly ConceptosDirecto Directo = new(
        new ConceptoDirecto(Visacion, UnidadCobro.BL),
        new ConceptoDirecto(SecurityFee, UnidadCobro.CTR),
        new ConceptoDirecto(Refacturacion, UnidadCobro.CTR),
        Recepcion, Despacho, Movilizacion);

    internal static VersionConvenioSnapshot Convenio(
        decimal? tcMinimo = null, int? diasLibres = null, params (int Id, decimal Tarifa, Moneda Moneda)[] tarifas) =>
        new(Guid.Parse("50000000-0000-0000-0000-000000000003"), 3, tcMinimo, diasLibres,
            tarifas.ToDictionary(t => t.Id, t => new TarifaConvenio(t.Tarifa, t.Moneda)));

    internal static ContextoCalculo Contexto(params ServicioACobrar[] servicios) => new()
    {
        OperacionId = Guid.Parse("40000000-0000-0000-0000-000000000001"),
        Retiro = TipoRetiro.INDIRECTO,
        Terminal = GrupoTerminal.STI,
        TarifaPublica = Publica,
        TipoCambioUsd = 950m,
        Servicios = servicios,
        ConceptosDirecto = Directo,
        CantidadBls = 1,
        CantidadContenedores = 2,
    };

    private static readonly MotorTarifas Motor = new();

    private static ResultadoLinea Unica(ContextoCalculo ctx) => Assert.Single(Motor.Calcular(ctx).Lineas);

    [Fact]
    public void T1_tarifa_de_convenio_simple_en_CLP()
    {
        var ctx = Contexto(new ServicioACobrar(Recepcion, 2m, UnidadCobro.CTR)) with
        {
            Convenio = Convenio(tarifas: (Recepcion, 15_000m, Moneda.CLP)),
        };

        var linea = Unica(ctx);

        Assert.Equal(2m, linea.Cantidad);
        Assert.Equal(15_000m, linea.Tarifa);
        Assert.Equal(Moneda.CLP, linea.Moneda);
        Assert.Equal(30_000m, linea.MontoClp);
        Assert.False(linea.EsExcepcion);
        Assert.Null(linea.Memoria.TipoCambioAplicado);
        Assert.Equal(3, linea.Memoria.VersionConvenio);
        Assert.Equal("CONVENIO", linea.Memoria.Reglas.Single(r => r.Regla == "R1_RESOLUCION_TARIFA").Dato("fuente"));
    }

    [Fact]
    public void T2_tarifa_USD_sobre_el_piso_usa_el_dolar_del_dia()
    {
        var ctx = Contexto(new ServicioACobrar(Recepcion, 1m, UnidadCobro.CTR)) with
        {
            Convenio = Convenio(tcMinimo: 800m, tarifas: (Recepcion, 100m, Moneda.USD)),
            TipoCambioUsd = 950m,
        };

        var linea = Unica(ctx);

        Assert.Equal(95_000m, linea.MontoClp);
        Assert.Equal(950m, linea.Memoria.TipoCambioAplicado);
        Assert.False(linea.Memoria.PisoActivado);
    }

    [Fact]
    public void T3_tarifa_USD_bajo_el_piso_usa_el_piso()
    {
        var ctx = Contexto(new ServicioACobrar(Recepcion, 1m, UnidadCobro.CTR)) with
        {
            Convenio = Convenio(tcMinimo: 800m, tarifas: (Recepcion, 100m, Moneda.USD)),
            TipoCambioUsd = 700m,
        };

        var linea = Unica(ctx);

        Assert.Equal(80_000m, linea.MontoClp);
        Assert.Equal(800m, linea.Memoria.TipoCambioAplicado);
        Assert.True(linea.Memoria.PisoActivado);
    }

    [Fact]
    public void T4_servicio_fuera_del_convenio_usa_la_publica_y_queda_como_excepcion()
    {
        var ctx = Contexto(new ServicioACobrar(Sag, 1m, UnidadCobro.CTR)) with
        {
            Convenio = Convenio(tarifas: (Recepcion, 15_000m, Moneda.CLP)),
        };

        var linea = Unica(ctx);

        Assert.True(linea.EsExcepcion);
        Assert.False(linea.SinTarifa);
        Assert.Equal(21_000m, linea.Tarifa);
        Assert.Equal(21_000m, linea.MontoClp);
        Assert.Equal("SRV-SAG-001", linea.ArticuloSap);
        Assert.Equal(7, linea.Memoria.VersionTarifaPublica);
        Assert.Equal("PUBLICA", linea.Memoria.Reglas.Single(r => r.Regla == "R1_RESOLUCION_TARIFA").Dato("fuente"));
    }

    [Fact]
    public void T5_almacenaje_descuenta_los_dias_libres()
    {
        var ctx = Contexto(new ServicioACobrar(Almacenaje, 12m, UnidadCobro.CTR_DIA, "ABCD1234567")) with
        {
            Convenio = Convenio(diasLibres: 10, tarifas: (Almacenaje, 9_000m, Moneda.CLP)),
            Contenedores = [new CondicionAlmacenaje("ABCD1234567", DiasAlmacenaje: 12, DiasBajoAforo: null)],
        };

        var linea = Unica(ctx);

        Assert.Equal(2m, linea.Cantidad);
        Assert.Equal(18_000m, linea.MontoClp);
        var r4 = linea.Memoria.Reglas.Single(r => r.Regla == "R4_ALMACENAJE");
        Assert.Equal("12", r4.Dato("diasCalculados"));
        Assert.Equal("10", r4.Dato("diasLibres"));
        Assert.Equal("2", r4.Dato("diasCobrados"));
    }

    [Fact]
    public void T6_almacenaje_no_cobra_los_dias_bajo_aforo()
    {
        var ctx = Contexto(new ServicioACobrar(Almacenaje, 5m, UnidadCobro.CTR_DIA)) with
        {
            Contenedores = [new CondicionAlmacenaje("ABCD1234567", DiasAlmacenaje: 5, DiasBajoAforo: 3, DiasAforoCalculados: true)],
        };

        var linea = Unica(ctx);

        Assert.Equal(2m, linea.Cantidad);
        Assert.Equal(18_000m, linea.MontoClp);
        Assert.Equal("3", linea.Memoria.Reglas.Single(r => r.Regla == "R4_ALMACENAJE").Dato("diasBajoAforo"));
        Assert.Contains(linea.Memoria.Reglas, r => r.Regla == DiasBajoAforo.Regla && r.Version == DiasBajoAforo.Version);
    }

    [Fact]
    public void T7_el_mayor_entre_m3_y_toneladas_paga()
    {
        var ctx = Contexto(new ServicioACobrar(ManipuleoTon, 12m, UnidadCobro.TON, MetrosCubicos: 18m, Toneladas: 12m)) with
        {
            Convenio = Convenio(tarifas: (ManipuleoTon, 1_000m, Moneda.CLP)),
        };

        var linea = Unica(ctx);

        Assert.Equal(18m, linea.Cantidad);
        Assert.Equal(18_000m, linea.MontoClp);
        Assert.Equal("MAYOR_M3_TON", linea.Memoria.Reglas.Single(r => r.Regla == "R2_CANTIDAD").Dato("driver"));
    }

    [Fact]
    public void T8_exportacion_cobra_por_cantidad_de_DUS()
    {
        var ctx = Contexto(new ServicioACobrar(DocExpo, 1m, UnidadCobro.DUS)) with
        {
            EsExportacion = true,
            CantidadDus = 3,
            Convenio = Convenio(tarifas: (DocExpo, 12_000m, Moneda.CLP)),
        };

        var linea = Unica(ctx);

        Assert.Equal(3m, linea.Cantidad);
        Assert.Equal(36_000m, linea.MontoClp);
        Assert.Equal("CANTIDAD_DUS", linea.Memoria.Reglas.Single(r => r.Regla == "R2_CANTIDAD").Dato("driver"));
    }

    [Fact]
    public void T9_recargo_de_15_por_ciento_sobre_la_refacturacion()
    {
        var ctx = Contexto(new ServicioACobrar(Refacturacion, 1m, UnidadCobro.CTR)) with
        {
            Convenio = Convenio(tarifas: (Refacturacion, 100_000m, Moneda.CLP)),
            Recargos = [new RecargoIndicado(Refacturacion, "REFACTURACION_TERCEROS", 15m)],
        };

        var linea = Unica(ctx);

        Assert.Equal(115_000m, linea.MontoClp);
        var r5 = linea.Memoria.Reglas.Single(r => r.Regla == "R5_RECARGO");
        Assert.Equal("100000", r5.Dato("base"));
        Assert.Equal("15", r5.Dato("porcentaje"));
        Assert.Equal(15_000m, decimal.Parse(r5.Dato("recargo")!, System.Globalization.CultureInfo.InvariantCulture));
    }

    private static ContextoCalculo DirectoSinConvenio(GrupoTerminal terminal) =>
        Contexto(
            new ServicioACobrar(Recepcion, 2m, UnidadCobro.CTR),
            new ServicioACobrar(Despacho, 2m, UnidadCobro.CTR),
            new ServicioACobrar(Movilizacion, 2m, UnidadCobro.CTR),
            new ServicioACobrar(Visacion, 1m, UnidadCobro.BL)) with
        {
            Retiro = TipoRetiro.DIRECTO,
            Terminal = terminal,
        };

    [Fact]
    public void T10_directo_STI_lleva_visacion_security_fee_y_refacturacion_con_valor()
    {
        var resultado = Motor.Calcular(DirectoSinConvenio(GrupoTerminal.STI));

        Assert.Equal([Visacion, SecurityFee, Refacturacion], resultado.Lineas.Select(l => l.TipoServicioId).ToArray());
        var refacturacion = resultado.Lineas.Single(l => l.TipoServicioId == Refacturacion);
        Assert.Equal(2m, refacturacion.Cantidad); // agregada por la regla: por contenedor
        Assert.Equal(70_000m, refacturacion.MontoClp);
        Assert.Equal(25_000m, resultado.Lineas.Single(l => l.TipoServicioId == Visacion).MontoClp);
        Assert.Equal(16_000m, resultado.Lineas.Single(l => l.TipoServicioId == SecurityFee).MontoClp);
        Assert.All(resultado.Lineas, l => Assert.False(l.EsExcepcion));
        Assert.All(resultado.Lineas, l => Assert.Contains(l.Memoria.Reglas, r => r.Regla == "R0_COMPOSICION_DIRECTO"));
    }

    [Fact]
    public void T10b_directo_DP_World_lleva_la_refacturacion_en_cero()
    {
        var resultado = Motor.Calcular(DirectoSinConvenio(GrupoTerminal.DP_WORLD));

        Assert.Equal(3, resultado.Lineas.Count);
        var refacturacion = resultado.Lineas.Single(l => l.TipoServicioId == Refacturacion);
        Assert.Equal(0m, refacturacion.Tarifa);
        Assert.Equal(0m, refacturacion.MontoClp);
        Assert.False(refacturacion.SinTarifa);
        Assert.True(resultado.Lineas.Single(l => l.TipoServicioId == Visacion).MontoClp > 0m);
        Assert.True(resultado.Lineas.Single(l => l.TipoServicioId == SecurityFee).MontoClp > 0m);
    }

    [Fact]
    public void T10c_directo_no_cobra_recepcion_despacho_ni_movilizacion()
    {
        var resultado = Motor.Calcular(DirectoSinConvenio(GrupoTerminal.STI));

        Assert.DoesNotContain(resultado.Lineas, l => l.TipoServicioId is Recepcion or Despacho or Movilizacion);
        Assert.Equal([Recepcion, Despacho, Movilizacion], resultado.ServiciosDescartados.ToArray());
        Assert.Equal("42,43,44", resultado.Lineas[0].Memoria.Reglas.Single(r => r.Regla == "R0_COMPOSICION_DIRECTO")
            .Dato("serviciosDescartados"));
    }

    [Fact]
    public void T10_directo_con_convenio_no_aplica_la_regla_0()
    {
        var ctx = DirectoSinConvenio(GrupoTerminal.DP_WORLD) with
        {
            Convenio = Convenio(tarifas: (Recepcion, 15_000m, Moneda.CLP)),
        };

        var resultado = Motor.Calcular(ctx);

        Assert.Equal(4, resultado.Lineas.Count);
        Assert.Empty(resultado.ServiciosDescartados);
    }

    [Fact]
    public void T11_ajuste_manual_queda_auditable()
    {
        var ctx = Contexto(new ServicioACobrar(Recepcion, 2m, UnidadCobro.CTR)) with
        {
            Convenio = Convenio(tarifas: (Recepcion, 15_000m, Moneda.CLP)),
        };
        var linea = Unica(ctx);
        var fecha = new DateTimeOffset(2026, 11, 13, 15, 0, 0, TimeSpan.Zero);

        var ajustada = MotorTarifas.AplicarAjuste(linea, "facturador@zarpe.local", fecha,
            "Descuento comercial acordado por correo", linea.MontoClp - 5_000m);

        Assert.Equal(25_000m, ajustada.MontoClp);
        Assert.NotNull(ajustada.Memoria.Ajuste);
        Assert.Equal("facturador@zarpe.local", ajustada.Memoria.Ajuste.Usuario);
        Assert.Equal(fecha, ajustada.Memoria.Ajuste.Fecha);
        Assert.Equal(30_000m, ajustada.Memoria.Ajuste.ValorAnterior);
        Assert.Equal(25_000m, ajustada.Memoria.Ajuste.ValorNuevo);
        Assert.Equal("R7_AJUSTE_MANUAL", ajustada.Memoria.Reglas[^1].Regla);
        Assert.Equal(30_000m, linea.MontoClp); // la línea original no cambia
        Assert.Throws<ArgumentException>(() => MotorTarifas.AplicarAjuste(linea, "facturador@zarpe.local", fecha, "  ", 1m));
    }

    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void T12_mismo_contexto_mismo_resultado()
    {
        var ctx = DirectoSinConvenio(GrupoTerminal.STI) with
        {
            Convenio = Convenio(tcMinimo: 800m, diasLibres: 5, tarifas: [(Recepcion, 100m, Moneda.USD), (Almacenaje, 10m, Moneda.USD)]),
            TipoCambioUsd = 912.37m,
            Servicios = [new ServicioACobrar(Recepcion, 2m, UnidadCobro.CTR), new ServicioACobrar(Almacenaje, 9m, UnidadCobro.CTR_DIA)],
            Contenedores = [new CondicionAlmacenaje("ABCD1234567", 9, 1)],
        };

        var primero = JsonSerializer.Serialize(Motor.Calcular(ctx), Json);
        var segundo = JsonSerializer.Serialize(new MotorTarifas().Calcular(ctx), Json);

        Assert.Equal(primero, segundo);
    }

    [Fact]
    public void Dias_bajo_aforo_se_cuentan_por_fecha_calendario_UTC()
    {
        Assert.Equal(2, DiasBajoAforo.Calcular(
            new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 23, 8, 0, 0, TimeSpan.Zero)));
        Assert.Null(DiasBajoAforo.Calcular(new DateTimeOffset(2026, 10, 21, 9, 0, 0, TimeSpan.Zero), null));
    }
}
