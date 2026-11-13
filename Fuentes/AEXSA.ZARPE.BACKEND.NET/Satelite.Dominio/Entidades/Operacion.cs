using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

/// <summary>Operación facturable recibida de AEXSA o dada de alta a mano (ADR-005). Tabla <c>operacion</c>.</summary>
public class Operacion
{
    public Guid Id { get; set; }

    /// <summary>Origen en AEXSA ([E1-6]).</summary>
    public EntidadAexsa Entidad { get; set; } = EntidadAexsa.VISADO_VIRTUAL;

    /// <summary>Clave de la operación por entidad. En visado virtual es el único ID, como texto.</summary>
    public string OperacionIdAexsa { get; set; } = string.Empty;

    /// <summary>operacionIdAexsa del contrato v3.0: uno o más IDs ([E1-6]).</summary>
    public long[] IdsAexsa { get; set; } = [];

    public Guid? ClienteId { get; set; }
    public TipoCarga? TipoCarga { get; set; }
    public Sentido? Sentido { get; set; }
    public TipoRetiro? TipoRetiro { get; set; }
    public bool ConConvenio { get; set; }
    public Guid? VersionConvenioId { get; set; }
    public string? Puerto { get; set; }
    public long[]? Din { get; set; }
    public string[]? Dus { get; set; }
    public int? CantidadDus { get; set; }
    public long[]? Papeletas { get; set; }
    public int? TerminalAexsaId { get; set; }
    public string? TerminalAexsaNombre { get; set; }
    public GrupoTerminal? TerminalGrupo { get; set; }
    public EstadoOperacion EstadoOperacion { get; set; } = EstadoOperacion.ACTIVA;
    public Guid? ConsignatarioId { get; set; }
    public Guid? ClienteFacturadorId { get; set; }
    public TipoClienteFacturador? TipoClienteFacturador { get; set; }
    public string? FacturadorAsignadoPor { get; set; }
    public DateTimeOffset? FacturadorAsignadoEn { get; set; }
    public string? RutaClasificada { get; set; }
    public MomentoFacturacion? MomentoFacturacion { get; set; }
    public bool Facturable { get; set; } = true;
    public DateTimeOffset? VisadoAprobadoEn { get; set; }
    public int VersionEvento { get; set; } = 1;
    public DateTimeOffset IngeridoEn { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }

    public List<OperacionBl> Bls { get; set; } = [];
    public List<ContenedorOperacion> Contenedores { get; set; } = [];
    public List<ServicioPrestado> Servicios { get; set; } = [];
    public List<OperacionPendiente> Pendientes { get; set; } = [];
}

/// <summary>Documento de carga (BL) de la operación: bls[] del contrato v3.0 ([E1-6]). Tabla <c>operacion_bl</c>.</summary>
public class OperacionBl
{
    public Guid OperacionId { get; set; }
    public long BlId { get; set; }
    public string Numero { get; set; } = string.Empty;
}

/// <summary>Contenedor de la operación: almacenaje, despacho e inspecciones. Tabla <c>contenedor_operacion</c>.</summary>
public class ContenedorOperacion
{
    public Guid Id { get; set; }
    public Guid OperacionId { get; set; }
    public string Numero { get; set; } = string.Empty;
    public DateTimeOffset? FechaRecepcion { get; set; }
    public DateTimeOffset? FechaRetiro { get; set; }
    public DateTimeOffset? FechaDespacho { get; set; }
    public int? DiasAlmacenaje { get; set; }
    public int? DiasBajoAforo { get; set; }
    public bool EsReefer { get; set; }

    /// <summary>JSON: [{tipo, asignadaEn, finalizadaEn, resultado}].</summary>
    public string? Inspecciones { get; set; }
}

/// <summary>Servicio prestado informado por AEXSA. Tabla <c>servicio_prestado</c>.</summary>
public class ServicioPrestado
{
    public Guid Id { get; set; }
    public Guid OperacionId { get; set; }
    public int TipoServicioId { get; set; }

    /// <summary>null = pendiente: sin cantidad no se valoriza.</summary>
    public decimal? Cantidad { get; set; }

    public string Unidad { get; set; } = string.Empty;
    public string? Contenedor { get; set; }
    public DateTimeOffset? FinalizadoEn { get; set; }
    public OrigenDato? OrigenDato { get; set; }
}

/// <summary>Pendiente de la operación y decisión del facturador (DBA-8). Tabla <c>operacion_pendiente</c>.</summary>
public class OperacionPendiente
{
    public Guid Id { get; set; }
    public Guid OperacionId { get; set; }
    public TipoPendiente Tipo { get; set; }
    public bool BloqueaValidacion { get; set; }

    /// <summary>JSON con el detalle (convenios candidatos, otra operación del mismo documento...).</summary>
    public string? Detalle { get; set; }

    public DateTimeOffset DetectadoEn { get; set; }
    public DateTimeOffset? ResueltoEn { get; set; }
    public string? ResueltoPor { get; set; }
    public ResolucionPendiente? Resolucion { get; set; }

    /// <summary>JSON con la decisión tomada.</summary>
    public string? Decision { get; set; }

    public string? Motivo { get; set; }
}

/// <summary>Registro append-only de cada mensaje recibido de AEXSA. Tabla <c>evento_aexsa</c>.</summary>
public class EventoAexsa
{
    public Guid Id { get; set; }

    /// <summary>Puede faltar solo en un mensaje RECHAZADO ([E1-6]).</summary>
    public EntidadAexsa? Entidad { get; set; }

    public string OperacionIdAexsa { get; set; } = string.Empty;
    public int Version { get; set; }
    public TipoEvento TipoEvento { get; set; }
    public DateTimeOffset RecibidoEn { get; set; }

    /// <summary>JSON: el mensaje tal como llegó.</summary>
    public string Carga { get; set; } = "{}";

    public ResultadoEvento Resultado { get; set; }
    public string? DetalleError { get; set; }

    /// <summary>Código del rechazo de ResultadoIngesta ([E1-1]).</summary>
    public string? CodigoError { get; set; }
}
