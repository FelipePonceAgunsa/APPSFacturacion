using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Aplicacion.Catalogo;

/// <summary>Puerto de persistencia del catálogo de servicios y del catálogo SAP versionado.</summary>
public interface ICatalogoRepositorio
{
    // --- Tipos de servicio (tarea 1.1) ---
    Task<List<TipoServicio>> ListarTiposServicioAsync(CancellationToken ct);
    Task<TipoServicio?> ObtenerTipoServicioAsync(int tipoServicioId, CancellationToken ct);

    // --- Versión vigente y consultas (la vigente es la de mayor número) ---
    Task<CatalogoSnapshot?> ObtenerVersionVigenteAsync(CancellationToken ct);
    Task<(int Articulos, int ListasPrecios, int Kits)> ContarAsync(int version, CancellationToken ct);
    Task<List<SapArticulo>> ListarArticulosAsync(int version, string? busqueda, CancellationToken ct);
    Task<bool> ExisteArticuloAsync(int version, string codigo, CancellationToken ct);
    Task<SapListaPrecios?> ObtenerListaPreciosAsync(int version, string codigo, CancellationToken ct);
    Task<List<TarifaSnapshot>> ListarPreciosAsync(int version, string codigoLista, CancellationToken ct);
    Task<List<SapKit>> ListarKitsAsync(int version, string? ruta, CancellationToken ct);

    /// <summary>Código de artículo → activo, de una versión.</summary>
    Task<Dictionary<string, bool>> ActivoPorArticuloAsync(int version, CancellationToken ct);

    /// <summary>Artículos que usan las listas de precios y los kits de una versión.</summary>
    Task<HashSet<string>> ArticulosReferenciadosAsync(int version, CancellationToken ct);

    // --- Tipo de cambio ---
    Task<List<TipoCambio>> ListarTiposCambioAsync(DateOnly? desde, Moneda? moneda, CancellationToken ct);
    Task<DateOnly?> UltimaFechaTipoCambioAsync(CancellationToken ct);

    /// <summary>Tipos de cambio del rango, con seguimiento de cambios (para actualizarlos).</summary>
    Task<List<TipoCambio>> ObtenerTiposCambioAsync(DateOnly desde, DateOnly hasta, CancellationToken ct);
    void AgregarTipoCambio(TipoCambio tipoCambio);

    // --- Importaciones ---
    Task<bool> ExisteImportacionConfirmadaConHashAsync(string hash, CancellationToken ct);
    void AgregarImportacion(CatalogoImportacion importacion);
    Task<CatalogoImportacion?> ObtenerImportacionAsync(Guid id, CancellationToken ct);

    // --- Nueva versión (confirmación) ---
    void AgregarSnapshot(CatalogoSnapshot snapshot);
    void AgregarArticulos(IEnumerable<SapArticulo> articulos);
    void AgregarListasPrecios(IEnumerable<SapListaPrecios> listas, IEnumerable<TarifaSnapshot> tarifas);
    void AgregarKits(IEnumerable<SapKit> kits);

    /// <summary>Copia los artículos de una versión a otra (DBA-3b).</summary>
    Task CopiarArticulosAsync(int desde, int hacia, CancellationToken ct);
    /// <summary>Copia las listas de precios y sus tarifas de una versión a otra (DBA-3b).</summary>
    Task CopiarListasPreciosAsync(int desde, int hacia, CancellationToken ct);
    /// <summary>Copia los kits y su composición de una versión a otra (DBA-3b).</summary>
    Task CopiarKitsAsync(int desde, int hacia, CancellationToken ct);

    // --- Vínculo servicio ↔ artículo ---
    Task<List<ServicioArticuloSap>> ListarVinculosAsync(CancellationToken ct);
    Task<List<ServicioArticuloSap>> ObtenerVinculosDeServicioAsync(int tipoServicioId, CancellationToken ct);
    void AgregarVinculo(ServicioArticuloSap vinculo);
    void EliminarVinculo(ServicioArticuloSap vinculo);
}
