using Satelite.Dominio.Comun;
namespace Satelite.Aplicacion.Ofa;
// Contratos de la etiqueta OFA del OpenAPI (docs/arquitectura/api/openapi.yaml).
public sealed record AsignacionCobroDto(Guid AsignacionId, string DestinatarioRut, string? RazonSocial, RolCobro? Rol, string? OrigenInstruccion);
/// <summary>Regla aplicada con sus datos (texto con cultura invariante).</summary>
public sealed record ReglaAplicadaDto(string Regla, int Version, IReadOnlyDictionary<string, string> Detalle);
public sealed record AjusteManualDto(string Usuario, DateTimeOffset Fecha, string Motivo, decimal ValorAnterior, decimal ValorNuevo);
/// <summary>Memoria de cálculo vigente de la línea (la más reciente; append-only, DBA-5).</summary>
public sealed record MemoriaCalculoDto(
    Guid MemoriaId, IReadOnlyList<ReglaAplicadaDto> ReglasAplicadas, Guid? VersionConvenioId, int? VersionConvenio,
    int? VersionTarifaPublica, decimal? TipoCambioAplicado, Guid? ReglaConfigurableId, AjusteManualDto? AjusteManual,
    DateTimeOffset CalculadoEn);
/// <summary>
/// Línea de la OFA. <see cref="FuenteTarifa"/> y <see cref="ListaPrecios"/> salen de la memoria (CONVENIO, PUBLICA, MANUAL,
/// SIN_TARIFA). <see cref="SinTarifa"/> y <see cref="SinTipoCambio"/> marcan lo que impide validar si nadie ajustó el monto.
/// </summary>
public sealed record LineaOfaDto(
    Guid LineaId, Guid? AsignacionId, int TipoServicioId, string? DescripcionServicio, string? ArticuloSap,
    decimal Cantidad, UnidadCobro? Unidad, decimal Tarifa, Moneda Moneda, decimal MontoClp, bool EsExcepcion,
    OrigenLinea Origen, bool? EnKitSap, int VersionCalculo, string? AgregadaPor, DateTimeOffset? AgregadaEn,
    string? MotivoAgregada, Guid? LineaOrigenId, string? FuenteTarifa, string? ListaPrecios, bool SinTarifa, bool SinTipoCambio,
    int CantidadMemorias, MemoriaCalculoDto Memoria);
/// <summary>Kit SAP sugerido por la ruta de la operación (ADR-017). Referencia para digitar en SAP, no entrada del cálculo.</summary>
public sealed record KitSugeridoDto(string CodigoKit, string Nombre, string? Ruta, IReadOnlyList<string> Articulos);
public sealed record OfaDto(
    Guid OfaId, Guid OperacionId, IReadOnlyList<long> OperacionIdAexsa, TipoOfa Tipo, EstadoOfa Estado, int VersionCalculo,
    DateTimeOffset FechaCalculo, IReadOnlyList<AsignacionCobroDto> Asignaciones, IReadOnlyList<LineaOfaDto> Lineas,
    decimal TotalClp, KitSugeridoDto? KitSugerido, int? VersionCatalogo, bool EnvioSapHabilitado);
/// <summary>POST /ofa/{id}/lineas. Sin tarifa se usa la del convenio o la pública; la moneda solo aplica con tarifa (por defecto CLP).</summary>
public sealed record AgregarLineaRequest(int TipoServicioId, decimal Cantidad, string Motivo, decimal? Tarifa = null, Moneda? Moneda = null);
/// <summary>POST /ofa/{id}/lineas/{lineaId}/ajuste. El monto lo fija la persona (nunca la IA); motivo obligatorio.</summary>
public sealed record AjusteManualRequest(decimal NuevoMonto, string Motivo);
public sealed record ResultadoValidacionDto(bool Valida, IReadOnlyList<string> Faltantes, EstadoOfa Estado);
public sealed record EnvioOfaDto(Guid OfaId, EstadoOfa Estado);
/// <summary>Opciones de la OFA (sección <c>Ofa</c>).</summary>
public sealed class OfaOptions
{
    public const string Seccion = "Ofa";
    /// <summary>Envío automático a SAP (Fase B, tarea 3.3). En la etapa 1 queda en false: la OFA se digita en SAP.</summary>
    public bool EnvioSapHabilitado { get; set; }
}
