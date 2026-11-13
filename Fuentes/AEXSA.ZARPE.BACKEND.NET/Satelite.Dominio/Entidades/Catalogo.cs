using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

/// <summary>Intento de carga del catálogo SAP, antes de confirmarse (DBA-3). Tabla <c>catalogo_importacion</c>.</summary>
public class CatalogoImportacion
{
    public Guid Id { get; set; }
    public OrigenCatalogo Origen { get; set; }
    public TipoImportacion Tipo { get; set; }
    public string? ArchivoS3 { get; set; }

    /// <summary>SHA-256 en hexadecimal (64 caracteres).</summary>
    public string? HashContenido { get; set; }

    public int FilasLeidas { get; set; }
    public int FilasValidas { get; set; }
    public EstadoImportacion Estado { get; set; }
    public string CargadoPor { get; set; } = string.Empty;
    public DateTimeOffset CargadoEn { get; set; }
    public string? ResueltoPor { get; set; }
    public DateTimeOffset? ResueltoEn { get; set; }
    public int? SnapshotVersion { get; set; }

    public List<CatalogoImportacionError> Errores { get; set; } = [];
}

/// <summary>Error o advertencia de una importación. Tabla <c>catalogo_importacion_error</c>.</summary>
public class CatalogoImportacionError
{
    public long Id { get; set; }
    public Guid ImportacionId { get; set; }
    public int? Fila { get; set; }
    public string? Columna { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}

/// <summary>Versión confirmada y completa del catálogo (DBA-3b). Tabla <c>catalogo_snapshot</c>.</summary>
public class CatalogoSnapshot
{
    public int Version { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public OrigenCatalogo Origen { get; set; }
    public Guid ImportacionId { get; set; }
    public int? BaseVersion { get; set; }
    public string ConfirmadoPor { get; set; } = string.Empty;
    public DateTimeOffset ConfirmadoEn { get; set; }
}

/// <summary>Artículo de servicio de SAP por versión (archivo A1). Tabla <c>sap_articulo</c>.</summary>
public class SapArticulo
{
    public int SnapshotVersion { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string UnidadMedida { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public string? Grupo { get; set; }
}

/// <summary>Encabezado de lista de precios por versión (archivo A2). Tabla <c>sap_lista_precios</c>.</summary>
public class SapListaPrecios
{
    public int SnapshotVersion { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;

    /// <summary>La lista "spot" (RN-014).</summary>
    public bool EsPublica { get; set; }
}

/// <summary>Kit de SAP por versión (archivo A3, RN-061). Tabla <c>sap_kit</c>.</summary>
public class SapKit
{
    public Guid Id { get; set; }
    public int SnapshotVersion { get; set; }
    public string KitCodigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Ruta { get; set; }

    public List<SapKitArticulo> Articulos { get; set; } = [];
}

/// <summary>Composición de un kit, en la misma versión del kit (DBA-3b). Tabla <c>sap_kit_articulo</c>.</summary>
public class SapKitArticulo
{
    public Guid KitId { get; set; }
    public int SnapshotVersion { get; set; }
    public string ArticuloId { get; set; } = string.Empty;
    public decimal Cantidad { get; set; } = 1m;
}

/// <summary>Vínculo servicio del Satélite ↔ artículo SAP, fijado por una persona. Tabla <c>servicio_articulo_sap</c>.</summary>
public class ServicioArticuloSap
{
    public int TipoServicioId { get; set; }
    public string ArticuloId { get; set; } = string.Empty;
    public OrigenVinculo Origen { get; set; }
    public string? ConfirmadoPor { get; set; }
    public DateTimeOffset? ConfirmadoEn { get; set; }
}

/// <summary>Tipo de cambio diario que usa el motor (RN-022, [E1-3]). Tabla <c>tipo_cambio</c>.</summary>
public class TipoCambio
{
    public DateOnly Fecha { get; set; }

    /// <summary>USD o UF.</summary>
    public Moneda Moneda { get; set; }

    public decimal Valor { get; set; }
    public FuenteTipoCambio Fuente { get; set; }
    public Guid? ImportacionId { get; set; }
    public string CargadoPor { get; set; } = string.Empty;
}

/// <summary>Precio de un artículo en una lista, por versión del catálogo (ADR-003). Tabla <c>tarifa_snapshot</c>.</summary>
public class TarifaSnapshot
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public DateTimeOffset Fecha { get; set; }
    public string ArticuloId { get; set; } = string.Empty;
    public string ListaPreciosId { get; set; } = string.Empty;
    public decimal Precio { get; set; }
    public Moneda Moneda { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public DateOnly? VigenteHasta { get; set; }
}
