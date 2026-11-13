using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Motor;

/// <summary>
/// Entrada inmutable del motor de tarifas (motor-tarifas-detalle.md §2). Todo lo que el cálculo necesita
/// viaja aquí: convenio y tarifa pública como snapshots con versión, y el tipo de cambio como dato.
/// No se consulta nada en vivo durante el cálculo (reproducibilidad, RNF-03).
/// </summary>
public sealed record ContextoCalculo
{
    public required Guid OperacionId { get; init; }

    /// <summary>Exportación: los servicios por DUS usan <see cref="CantidadDus"/> (RN-047).</summary>
    public bool EsExportacion { get; init; }

    /// <summary>Cantidad de DUS, captura manual (RN-047, adenda A2).</summary>
    public int? CantidadDus { get; init; }

    /// <summary>DIRECTO | INDIRECTO (RN-026).</summary>
    public TipoRetiro? Retiro { get; init; }

    /// <summary>
    /// Grupo del terminal (terminal_mapa). Con SIN_MAPEAR la capa de aplicación no invoca el cálculo del
    /// directo sin convenio: deja la operación pendiente (ADR-015).
    /// </summary>
    public GrupoTerminal Terminal { get; init; } = GrupoTerminal.SIN_MAPEAR;

    /// <summary>Versión de convenio fijada; null = sin convenio (tarifa pública).</summary>
    public VersionConvenioSnapshot? Convenio { get; init; }

    /// <summary>Hay convenio vigente: lo determina ZARPE por RUT y fecha, no viene de AEXSA.</summary>
    public bool ConConvenio => Convenio is not null;

    /// <summary>Tarifa pública (lista spot de SAP) de una versión fija del catálogo (RN-014).</summary>
    public required TarifaPublicaSnapshot TarifaPublica { get; init; }

    /// <summary>Artículo SAP vinculado a cada servicio (servicio_articulo_sap). Si falta, se usa el de la tarifa pública.</summary>
    public IReadOnlyDictionary<int, string> ArticulosSap { get; init; } = new Dictionary<int, string>();

    /// <summary>Dólar del día (RN-022). null = no hay tipo de cambio cargado.</summary>
    public decimal? TipoCambioUsd { get; init; }

    /// <summary>Valor de la UF del día.</summary>
    public decimal? TipoCambioUf { get; init; }

    public IReadOnlyList<ServicioACobrar> Servicios { get; init; } = [];

    /// <summary>Días de almacenaje y bajo aforo por contenedor (RN-023/051/052).</summary>
    public IReadOnlyList<CondicionAlmacenaje> Contenedores { get; init; } = [];

    /// <summary>Recargos indicados por instrucción comercial (RN-024). Sin recargo indicado no se aplica.</summary>
    public IReadOnlyList<RecargoIndicado> Recargos { get; init; } = [];

    /// <summary>Servicios que componen el directo sin convenio (Regla 0). Obligatorio si Retiro = DIRECTO sin convenio.</summary>
    public ConceptosDirecto? ConceptosDirecto { get; init; }

    public int CantidadBls { get; init; }
    public int CantidadContenedores { get; init; }
}

/// <summary>Tarifa pactada en el convenio para un servicio.</summary>
public sealed record TarifaConvenio(decimal Tarifa, Moneda Moneda);

/// <summary>Versión de convenio fijada para el cálculo.</summary>
public sealed record VersionConvenioSnapshot(
    Guid VersionConvenioId,
    int Numero,
    decimal? TcMinimo,
    int? DiasLibres,
    IReadOnlyDictionary<int, TarifaConvenio> Tarifas);

/// <summary>Precio público (lista spot) de un servicio, a través de su artículo SAP.</summary>
public sealed record TarifaPublica(string? ArticuloSap, decimal Precio, Moneda Moneda, string Lista);

/// <summary>Tarifa pública de una versión del catálogo: tipoServicioId → precio.</summary>
public sealed record TarifaPublicaSnapshot(int Version, IReadOnlyDictionary<int, TarifaPublica> Tarifas);

/// <summary>Servicio prestado a valorizar.</summary>
public sealed record ServicioACobrar(
    int TipoServicioId,
    decimal Cantidad,
    UnidadCobro Unidad,
    string? Contenedor = null,
    decimal? MetrosCubicos = null,
    decimal? Toneladas = null);

/// <summary>
/// Condición de almacenaje de un contenedor. <paramref name="DiasAforoCalculados"/> = true si los días bajo aforo
/// salieron de la inspección de aforo con <see cref="DiasBajoAforo"/> (y no los completó el facturador).
/// </summary>
public sealed record CondicionAlmacenaje(
    string Numero,
    int? DiasAlmacenaje,
    int? DiasBajoAforo,
    bool DiasAforoCalculados = false);

/// <summary>Recargo indicado sobre un servicio. <paramref name="Porcentaje"/> en puntos: 15 = 15 %.</summary>
public sealed record RecargoIndicado(int TipoServicioId, string Tipo, decimal Porcentaje);

/// <summary>Servicio del catálogo con su unidad de cobro.</summary>
public sealed record ConceptoDirecto(int TipoServicioId, UnidadCobro Unidad);

/// <summary>
/// Servicios de la Regla 0 (RN-078/079): los tres que se cobran en el directo sin convenio y los tres que no.
/// Los IDs salen de la configuración (Motor:ConceptosDirecto).
/// </summary>
public sealed record ConceptosDirecto(
    ConceptoDirecto Visacion,
    ConceptoDirecto SecurityFee,
    ConceptoDirecto Refacturacion,
    int Recepcion,
    int Despacho,
    int Movilizacion);
