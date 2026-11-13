using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

// Tablas del modelo fuera del alcance de la etapa 1 (reglas configurables, omisiones, alertas, factura, pago)
// y transversales (auditoría, outbox). Se mapean para que el modelo de EF Core sea completo.

/// <summary>Regla configurable de nivel 2 (ADR-008). Tabla <c>regla_configurable</c>.</summary>
public class ReglaConfigurable
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Condicion { get; set; } = "{}";
    public string Accion { get; set; } = "{}";
    public int Version { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
    public string CreadoPor { get; set; } = string.Empty;
    public DateTimeOffset CreadoEn { get; set; }
}

/// <summary>Omisión detectada y decisión del facturador (DBA-10). Tabla <c>omision_detectada</c>.</summary>
public class OmisionDetectada
{
    public Guid Id { get; set; }
    public Guid OperacionId { get; set; }
    public Guid? OfaId { get; set; }
    public int? TipoServicioId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public OrigenOmision Origen { get; set; }
    public decimal? Confianza { get; set; }
    public DateTimeOffset DetectadaEn { get; set; }
    public DecisionOmision? Decision { get; set; }
    public string? Motivo { get; set; }
    public string? DecididaPor { get; set; }
    public DateTimeOffset? DecididaEn { get; set; }
    public Guid? LineaOfaId { get; set; }
}

/// <summary>Alerta operativa; se cierra, no se borra (DBA-10). Tabla <c>alerta</c>.</summary>
public class Alerta
{
    public Guid Id { get; set; }
    public TipoAlerta Tipo { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public Guid? EntidadId { get; set; }
    public string? Detalle { get; set; }
    public EstadoAlerta Estado { get; set; } = EstadoAlerta.ABIERTA;
    public DateTimeOffset CreadaEn { get; set; }
    public string? AtendidaPor { get; set; }
    public DateTimeOffset? AtendidaEn { get; set; }
    public string? Comentario { get; set; }
}

/// <summary>Factura emitida en SAP (ADR-004). Tabla <c>factura</c>.</summary>
public class Factura
{
    public Guid Id { get; set; }
    public Guid OfaId { get; set; }
    public string Folio { get; set; } = string.Empty;
    public int TipoDte { get; set; }
    public decimal? MontoNeto { get; set; }
    public decimal? MontoTotal { get; set; }
    public Moneda Moneda { get; set; } = Moneda.CLP;
    public string? DteXmlS3 { get; set; }
    public DateTimeOffset? EmitidaEn { get; set; }
    public bool Conciliada { get; set; }
}

/// <summary>Pago recibido (RN-063). Tabla <c>pago</c>.</summary>
public class Pago
{
    public Guid Id { get; set; }
    public string? ReferenciaSap { get; set; }
    public string? PagadorRut { get; set; }
    public decimal Monto { get; set; }
    public DateOnly? FechaPago { get; set; }
    public EstadoPago? Estado { get; set; }
    public DateTimeOffset RegistradoEn { get; set; }
}

/// <summary>Aplicación de un pago a una factura (RN-067). Tabla <c>pago_factura</c>.</summary>
public class PagoFactura
{
    public Guid PagoId { get; set; }
    public Guid FacturaId { get; set; }
    public decimal MontoAplicado { get; set; }
}

/// <summary>Log transversal append-only: quién, cuándo, qué (RNF-01). Tabla <c>evento_auditoria</c>.</summary>
public class EventoAuditoria
{
    public long Id { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public Guid? EntidadId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Usuario { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public DateTimeOffset OcurridoEn { get; set; }
}

/// <summary>Outbox transaccional hacia SAP y AEXSA (ADR-002). Tabla <c>outbox_message</c>.</summary>
public class OutboxMessage
{
    public Guid Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public string ClaveIdempotencia { get; set; } = string.Empty;
    public EstadoOutbox Estado { get; set; }
    public int Intentos { get; set; }
    public string? UltimoError { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
    public DateTimeOffset? ProcesadoEn { get; set; }
}
