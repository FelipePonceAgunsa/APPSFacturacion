using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Aplicacion.Convenios;

// Contratos de la etiqueta Convenios del OpenAPI (docs/arquitectura/api/openapi.yaml).

/// <summary>Fila de la lista de convenios (C1). La vigencia es la de la versión VIGENTE, si existe.</summary>
public sealed record ConvenioResumenDto(
    Guid ConvenioId, string Rut, string RazonSocial, string? Comercial, int? VersionVigente,
    DateOnly? VigenciaDesde, DateOnly? VigenciaHasta, int UltimaVersion, EstadoVersionConvenio EstadoUltimaVersion);

/// <summary>Tarifa de un servicio. <see cref="Unidad"/> y <see cref="DescripcionServicio"/> salen del catálogo (readOnly).</summary>
public sealed record ServicioConvenioDto(
    int TipoServicioId, decimal Tarifa, Moneda Moneda, UnidadCobro? Unidad = null, string? DescripcionServicio = null);

public sealed record VersionConvenioDto(
    Guid VersionId, Guid ConvenioId, int Numero, EstadoVersionConvenio Estado, DateOnly VigenciaDesde, DateOnly? VigenciaHasta,
    decimal? TcMinimo, int? DiasLibres, OrigenVersionConvenio? Origen, decimal? ConfianzaIa,
    string? ConfirmadoPor, DateTimeOffset? ConfirmadoEn, DateTimeOffset CreadoEn, IReadOnlyList<ServicioConvenioDto> Servicios);

/// <summary>Convenio con todas sus versiones, de la más nueva a la más antigua (C3).</summary>
public sealed record ConvenioDetalleDto(
    Guid ConvenioId, string Rut, string RazonSocial, string? Comercial, DateTimeOffset CreadoEn, IReadOnlyList<VersionConvenioDto> Versiones);

/// <summary>
/// Versión en vigor para un RUT y una fecha. Si hay varios convenios vigentes, se elige el más antiguo (RN-018)
/// y <see cref="Candidatos"/> trae los ids de todas las versiones en vigor, incluida la elegida.
/// </summary>
public sealed record VersionConvenioVigenteDto(VersionConvenioDto Version, IReadOnlyList<Guid> Candidatos);

public sealed record ConvenioManualRequest(
    string Rut, string? RazonSocial, string? Comercial, DateOnly VigenciaDesde, DateOnly? VigenciaHasta,
    decimal? TcMinimo, int? DiasLibres, IReadOnlyList<ServicioConvenioDto> Servicios);

public sealed record NuevaVersionConvenioRequest(
    DateOnly VigenciaDesde, DateOnly? VigenciaHasta, decimal? TcMinimo, int? DiasLibres,
    OrigenVersionConvenio Origen, string? Motivo, IReadOnlyList<ServicioConvenioDto> Servicios);

/// <summary>Resultado de <see cref="IConvenioService.BuscarVigenteAsync"/> para los casos de uso (motor, operaciones).</summary>
/// <param name="Version">Versión elegida, con <c>Servicios</c> y <c>Convenio.Cliente</c> cargados.</param>
/// <param name="Candidatos">Todas las versiones en vigor del RUT a esa fecha (más de una = VARIOS_CONVENIOS_VIGENTES).</param>
public sealed record ConvenioVigente(VersionConvenio Version, IReadOnlyList<VersionConvenio> Candidatos);
