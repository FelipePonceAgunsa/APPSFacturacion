using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

/// <summary>
/// Prefactura (OFA) de una operación (ADR-010). Un recálculo no crea otra OFA: sube <see cref="VersionCalculo"/>
/// e inserta líneas nuevas con esa versión (DBA-7). Tabla <c>ofa</c>.
/// </summary>
public class Ofa
{
    public Guid Id { get; set; }
    public Guid OperacionId { get; set; }
    public TipoOfa Tipo { get; set; } = TipoOfa.PRINCIPAL;
    public EstadoOfa Estado { get; set; }
    public int VersionCalculo { get; set; } = 1;
    public string? FolioIdempotencia { get; set; }
    public DateTimeOffset FechaCalculo { get; set; }

    public List<LineaOfa> Lineas { get; set; } = [];
}

/// <summary>Destinatario del cobro (repartido, RN-050/077). Tabla <c>asignacion_cobro</c>.</summary>
public class AsignacionCobro
{
    public Guid Id { get; set; }
    public Guid OfaId { get; set; }
    public string DestinatarioRut { get; set; } = string.Empty;
    public RolCobro? Rol { get; set; }
    public string? OrigenInstruccion { get; set; }
}

/// <summary>Línea de la OFA. Vigentes: <c>linea_ofa.version_calculo = ofa.version_calculo</c>. Tabla <c>linea_ofa</c>.</summary>
public class LineaOfa
{
    public Guid Id { get; set; }
    public Guid OfaId { get; set; }
    public Guid? AsignacionCobroId { get; set; }
    public int TipoServicioId { get; set; }

    /// <summary>Código SAP vigente al calcular; null = sin artículo, no enviable.</summary>
    public string? ArticuloSapId { get; set; }

    public decimal Cantidad { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public decimal Tarifa { get; set; }
    public Moneda Moneda { get; set; }

    /// <summary>Monto en CLP tras la conversión.</summary>
    public decimal Monto { get; set; }

    public bool EsExcepcion { get; set; }
    public int VersionCalculo { get; set; }
    public OrigenLinea Origen { get; set; } = OrigenLinea.CALCULADA;
    public bool? EnKitSap { get; set; }
    public string? AgregadaPor { get; set; }
    public string? MotivoAgregada { get; set; }

    /// <summary>Cuándo se agregó a mano ([E1-4]).</summary>
    public DateTimeOffset? AgregadaEn { get; set; }

    public Guid? LineaOrigenId { get; set; }

    public List<MemoriaCalculoRegistro> Memorias { get; set; } = [];
}

/// <summary>
/// Memoria de cálculo de una línea, append-only (REQ-011, RNF-01). Una línea tiene 1..N memorias:
/// el cálculo inicial y cada ajuste; la vigente es la más reciente (DBA-5). Tabla <c>memoria_calculo</c>.
/// </summary>
public class MemoriaCalculoRegistro
{
    public Guid Id { get; set; }
    public Guid LineaOfaId { get; set; }

    /// <summary>JSON: [{regla, version, detalle}].</summary>
    public string ReglasAplicadas { get; set; } = "[]";

    public Guid? VersionConvenioId { get; set; }

    /// <summary>Versión del catálogo usada (DBA-11).</summary>
    public int? TarifaSnapshotVer { get; set; }

    /// <summary>Tipo de cambio aplicado (RN-022).</summary>
    public decimal? TipoCambio { get; set; }

    public Guid? ReglaConfigId { get; set; }

    /// <summary>JSON: {usuario, fecha, motivo, valorAnterior, valorNuevo} (REQ-006).</summary>
    public string? AjusteManual { get; set; }

    public DateTimeOffset CalculadoEn { get; set; }
}
