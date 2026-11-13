using Satelite.Dominio.Comun;

namespace Satelite.Aplicacion.Catalogo;

// Contratos de la etiqueta Catalogo del OpenAPI (docs/arquitectura/api/openapi.yaml).

/// <summary>Servicio del catálogo normalizado (tarea 1.1). Esquema <c>TipoServicio</c>.</summary>
public sealed record TipoServicioDto(int TipoServicioId, string Codigo, string Descripcion, UnidadCobro Unidad, bool Habilitado);

public sealed record CatalogoResumenDto(
    int Version, DateTimeOffset Fecha, OrigenCatalogo Origen, int Articulos, int ListasPrecios, int Kits, DateOnly? TipoCambioAl);

public sealed record ArticuloSapDto(string CodigoArticulo, string Descripcion, string UnidadMedida, bool Activo, string? GrupoArticulo);

public sealed record PrecioListaDto(
    string CodigoLista, string NombreLista, string CodigoArticulo, decimal Precio, Moneda Moneda,
    DateOnly VigenteDesde, DateOnly? VigenteHasta);

public sealed record KitArticuloDto(string CodigoArticulo, decimal Cantidad);

public sealed record KitSapDto(string CodigoKit, string Nombre, string? Ruta, bool Activo, IReadOnlyList<KitArticuloDto> Articulos);

/// <summary>Tipo de cambio de un día. En el request se ignora <see cref="Fuente"/> (readOnly).</summary>
public sealed record TipoCambioDto(DateOnly Fecha, Moneda Moneda, decimal Valor, FuenteTipoCambio? Fuente = null);

public static class EstadoVinculo
{
    public const string Propuesto = "PROPUESTO";
    public const string Confirmado = "CONFIRMADO";
    public const string SinVinculo = "SIN_VINCULO";

    public static readonly IReadOnlyList<string> Todos = [Propuesto, Confirmado, SinVinculo];
}

public sealed record VinculoServicioArticuloDto(
    int TipoServicioId, string DescripcionServicio, string? ArticuloSap, string Estado, OrigenVinculo? Origen, string? ConfirmadoPor);

public sealed record VinculoRequest(string ArticuloSap);

public sealed record ErrorImportacionDto(int? Fila, string? Columna, string Codigo, string Mensaje);

public sealed record ResultadoImportacionDto(
    Guid ImportacionId, TipoImportacion Tipo, int? VersionBase, int FilasLeidas, int FilasValidas,
    IReadOnlyList<ErrorImportacionDto> Errores, EstadoImportacion Estado);

/// <summary>Configuración del catálogo (sección <c>Catalogo</c>).</summary>
public sealed class CatalogoOptions
{
    public const string Seccion = "Catalogo";

    /// <summary>
    /// Código de la lista de precios pública ("spot", RN-014) dentro del archivo A2.
    /// [SUPUESTO: "LP-PUBLICA", el código real lo confirma el equipo SAP con los archivos reales]
    /// </summary>
    public string ListaPublica { get; set; } = "LP-PUBLICA";
}
