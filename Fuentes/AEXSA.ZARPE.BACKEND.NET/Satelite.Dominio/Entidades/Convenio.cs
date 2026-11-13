using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

/// <summary>Convenio de un cliente (ADR-007). Tabla <c>convenio</c>.</summary>
public class Convenio
{
    public Guid Id { get; set; }
    public Guid ClienteId { get; set; }
    public string? Comercial { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Cliente? Cliente { get; set; }
    public List<VersionConvenio> Versiones { get; set; } = [];
}

/// <summary>Versión de un convenio. Nunca se edita una VIGENTE: cada cambio es una versión nueva. Tabla <c>version_convenio</c>.</summary>
public class VersionConvenio
{
    public Guid Id { get; set; }
    public Guid ConvenioId { get; set; }
    public int Numero { get; set; }
    public EstadoVersionConvenio Estado { get; set; }
    public DateOnly VigenciaDesde { get; set; }
    public DateOnly? VigenciaHasta { get; set; }
    public decimal? TcMinimo { get; set; }
    public int? DiasLibres { get; set; }
    public OrigenVersionConvenio? Origen { get; set; }
    public string? DocumentoS3 { get; set; }
    public string? ConfirmadoPor { get; set; }
    public DateTimeOffset? ConfirmadoEn { get; set; }
    public DateTimeOffset CreadoEn { get; set; }

    public Convenio? Convenio { get; set; }
    public List<ServicioConvenio> Servicios { get; set; } = [];
}

/// <summary>Tarifa pactada de un servicio en una versión de convenio. Tabla <c>servicio_convenio</c>.</summary>
public class ServicioConvenio
{
    public Guid Id { get; set; }
    public Guid VersionConvenioId { get; set; }
    public int TipoServicioId { get; set; }
    public decimal Tarifa { get; set; }
    public Moneda Moneda { get; set; }

    /// <summary>Copiada del catálogo (tipo_servicio.unidad_normalizada), no se digita (RN-036).</summary>
    public string Unidad { get; set; } = string.Empty;
}
