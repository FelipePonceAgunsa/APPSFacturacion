using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
namespace Satelite.Aplicacion.Ofa;
/// <summary>Puerto de persistencia de la OFA, sus líneas y su memoria de cálculo (append-only).</summary>
public interface IOfaRepositorio
{
    /// <summary>OFA con todas sus líneas (todas las versiones de cálculo) y sus memorias, con seguimiento de cambios.</summary>
    Task<Dominio.Entidades.Ofa?> ObtenerAsync(Guid ofaId, CancellationToken ct);
    /// <summary>OFA PRINCIPAL de la operación, con líneas y memorias, con seguimiento de cambios.</summary>
    Task<Dominio.Entidades.Ofa?> ObtenerPorOperacionAsync(Guid operacionId, CancellationToken ct);
    Task<List<AsignacionCobro>> ListarAsignacionesAsync(Guid ofaId, CancellationToken ct);
    /// <summary>Precios de las listas públicas (es_publica) de una versión del catálogo, para esos artículos.</summary>
    Task<List<TarifaSnapshot>> ListarTarifasPublicasAsync(int version, IReadOnlyCollection<string> articulos, CancellationToken ct);
    /// <summary>Valor del día o, si falta, el del último día anterior con valor.</summary>
    Task<decimal?> TipoCambioAsync(Moneda moneda, DateOnly fecha, CancellationToken ct);
    /// <summary>Versión de convenio con sus servicios.</summary>
    Task<VersionConvenio?> ObtenerVersionConvenioAsync(Guid versionConvenioId, CancellationToken ct);
    Task<Dictionary<Guid, int>> NumerosVersionConvenioAsync(IEnumerable<Guid> ids, CancellationToken ct);
    void Agregar(Dominio.Entidades.Ofa ofa);
    void AgregarAsignacion(AsignacionCobro asignacion);
    void AgregarLinea(LineaOfa linea);
    void AgregarMemoria(MemoriaCalculoRegistro memoria);
}
