using System.Text.Json;
using Satelite.Dominio.Comun;
namespace Satelite.Aplicacion.Operaciones;
// Contratos de la etiqueta Operaciones y del callback /aexsa/eventos (docs/arquitectura/api/openapi.yaml).
// --- Evento facturable de AEXSA v3.0 (contratos-integracion.md §2). Los campos de prioridad A son `required`:
// deben venir aunque sea en null (sentido, tipoRetiro, terminal).
/// <summary>Mensaje que envía AEXSA (esquema EventoFacturable). Trae el estado completo de la operación.</summary>
public sealed class EventoFacturableDto
{
    public required EntidadAexsa Entidad { get; init; }
    public required TipoEvento TipoEvento { get; init; }
    public required int Version { get; init; }
    public required long[] OperacionIdAexsa { get; init; }
    public required DateTimeOffset VisadoAprobadoEn { get; init; }
    public required EstadoOperacion EstadoOperacion { get; init; }
    public required TipoCarga TipoCarga { get; init; }
    public required Sentido? Sentido { get; init; }
    public required TipoRetiro? TipoRetiro { get; init; }
    public required TerminalAexsaDto? Terminal { get; init; }
    public long[]? Din { get; init; }
    public required BlDocumentoDto[] Bls { get; init; }
    public long[]? Papeletas { get; init; }
    public required ParteFacturaDto Consignatario { get; init; }
    public ClienteFacturadorDto? ClienteFacturador { get; init; }
    public ContenedorEventoDto[]? Contenedores { get; init; }
    public required ServicioEventoDto[] Servicios { get; init; }
}
public sealed record TerminalAexsaDto(int Id, string Nombre);
public sealed record BlDocumentoDto(long Id, string Numero);
public sealed record ParteFacturaDto(string Rut, string? RazonSocial);
public sealed record ClienteFacturadorDto(
    string Rut, string? RazonSocial, TipoClienteFacturador? Tipo, string? AsignadoPor, DateTimeOffset? AsignadoEn);
/// <summary>Inspección de un contenedor; incluye el aforo (nota 15).</summary>
public sealed record InspeccionDto(string Tipo, DateTimeOffset? AsignadaEn, DateTimeOffset? FinalizadaEn, ResultadoInspeccion? Resultado);
public sealed record ContenedorEventoDto(
    string Numero, DateTimeOffset? FechaRecepcion, DateTimeOffset? FechaRetiro, DateTimeOffset? FechaDespacho,
    int? DiasAlmacenaje, bool EsReefer, InspeccionDto[]? Inspecciones);
public sealed record ServicioEventoDto(
    int TipoServicioId, decimal? Cantidad, string? Unidad, string? Contenedor, DateTimeOffset? FinalizadoEn, OrigenDato? Origen);
/// <summary>Respuesta de POST /aexsa/eventos (esquema ResultadoIngesta), para todos los códigos HTTP.</summary>
public sealed record ResultadoIngestaDto(
    ResultadoEvento Resultado, string? Codigo, Guid? OperacionId, int? VersionAplicada, IReadOnlyList<string> Errores);
/// <summary>Resultado interno de la ingesta: código HTTP y cuerpo para AEXSA.</summary>
public sealed record ResultadoIngestaHttp(int StatusCode, ResultadoIngestaDto Cuerpo);
// --- Operaciones (F1, F2)
/// <summary>Fila de la bandeja de operaciones (F1).</summary>
public sealed record OperacionResumenDto(
    Guid OperacionId, EntidadAexsa Entidad, IReadOnlyList<long> OperacionIdAexsa, string? Rut, string? RazonSocial,
    bool ConConvenio, bool Facturable, EstadoOperacion EstadoOperacion, TipoCarga? TipoCarga, TipoRetiro? TipoRetiro,
    string? RutaClasificada, DateTimeOffset? VisadoAprobadoEn, int VersionEvento,
    IReadOnlyList<TipoPendiente> Pendientes, Guid? OfaId, EstadoOfa? EstadoOfa);
public sealed record ParteDto(string Rut, string? RazonSocial);
public sealed record ClienteFacturadorDetalleDto(
    string Rut, string? RazonSocial, TipoClienteFacturador? Tipo, string? AsignadoPor, DateTimeOffset? AsignadoEn);
/// <summary>Convenio encontrado por ZARPE (RUT del facturador o del consignatario y fecha del visado).</summary>
public sealed record ConvenioOperacionDto(Guid VersionConvenioId, Guid ConvenioId, int Numero, DateOnly VigenciaDesde, DateOnly? VigenciaHasta);
/// <summary>Contenedor con los días bajo aforo que usa el cálculo (desde la inspección de aforo o completados a mano).</summary>
public sealed record ContenedorOperacionDto(
    string Numero, DateTimeOffset? FechaRecepcion, DateTimeOffset? FechaRetiro, DateTimeOffset? FechaDespacho,
    int? DiasAlmacenaje, int? DiasBajoAforo, bool EsReefer, IReadOnlyList<InspeccionDto> Inspecciones);
public sealed record ServicioPrestadoDto(
    int TipoServicioId, string? Descripcion, decimal? Cantidad, UnidadCobro? Unidad, string? Contenedor,
    DateTimeOffset? FinalizadoEn, OrigenDato? Origen);
/// <summary>Pendiente de la operación, abierto o resuelto (DBA-8).</summary>
public sealed record PendienteDto(
    Guid PendienteId, TipoPendiente Tipo, bool BloqueaValidacion, bool ExigeDecision, IReadOnlyDictionary<string, JsonElement>? Detalle,
    DateTimeOffset DetectadoEn, DateTimeOffset? ResueltoEn, string? ResueltoPor, ResolucionPendiente? Resolucion,
    IReadOnlyDictionary<string, JsonElement>? Decision, string? Motivo);
/// <summary>Detalle de la operación (F2).</summary>
public sealed record OperacionDto(
    Guid OperacionId, EntidadAexsa Entidad, IReadOnlyList<long> OperacionIdAexsa, string? Rut, string? RazonSocial,
    bool ConConvenio, bool Facturable, TipoCarga? TipoCarga, Sentido? Sentido, TipoRetiro? TipoRetiro,
    EstadoOperacion EstadoOperacion, TerminalAexsaDto? Terminal, GrupoTerminal GrupoTerminal,
    IReadOnlyList<TipoPendiente> Pendientes, IReadOnlyList<PendienteDto> PendientesDetalle,
    IReadOnlyList<long> Din, int? CantidadDus, IReadOnlyList<BlDocumentoDto> Bls, IReadOnlyList<long> Papeletas,
    ParteDto? Consignatario, ClienteFacturadorDetalleDto? ClienteFacturador, ConvenioOperacionDto? Convenio,
    IReadOnlyList<ContenedorOperacionDto> Contenedores, int VersionEvento, string? RutaClasificada,
    MomentoFacturacion? MomentoFacturacion, DateTimeOffset? VisadoAprobadoEn, DateTimeOffset IngeridoEn,
    DateTimeOffset ActualizadoEn, IReadOnlyList<ServicioPrestadoDto> Servicios, Guid? OfaId, EstadoOfa? EstadoOfa);
public sealed record CantidadServicioDto(int TipoServicioId, decimal Cantidad);
/// <summary>
/// Decisión sobre un pendiente que la exige. <see cref="Valor"/>: VARIOS_CONVENIOS_VIGENTES → id del convenio (o de su
/// versión) elegido; DOCUMENTO_CON_VARIAS_SOLICITUDES → FACTURAR | NO_FACTURAR; INSPECCION_RECHAZADA → CONTINUAR | NO_FACTURAR.
/// </summary>
public sealed record DecisionPendienteDto(TipoPendiente Tipo, string Valor);
/// <summary>PATCH /operaciones/{id}: completar o corregir datos con motivo obligatorio. Un campo null no cambia.</summary>
public sealed record CompletarOperacionRequest(
    string Motivo, TipoRetiro? TipoRetiro = null, Sentido? Sentido = null, int? CantidadDus = null,
    DateTimeOffset? FechaDespacho = null, string? Contenedor = null, int? DiasAforo = null,
    IReadOnlyList<CantidadServicioDto>? CantidadesServicio = null, IReadOnlyList<DecisionPendienteDto>? Decisiones = null);
