using Microsoft.EntityFrameworkCore;
using Satelite.Aplicacion.Ofa;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Infraestructura.Persistencia;
using OfaEntidad = Satelite.Dominio.Entidades.Ofa;
namespace Satelite.Infraestructura.Repositorios;
internal sealed class OfaRepositorio(PrincipalDbContext db) : IOfaRepositorio
{
    private IQueryable<OfaEntidad> ConLineas() =>
        db.Ofas.Include(o => o.Lineas).ThenInclude(l => l.Memorias).AsSplitQuery();
    public Task<OfaEntidad?> ObtenerAsync(Guid ofaId, CancellationToken ct) =>
        ConLineas().FirstOrDefaultAsync(o => o.Id == ofaId, ct);
    public Task<OfaEntidad?> ObtenerPorOperacionAsync(Guid operacionId, CancellationToken ct) =>
        ConLineas().FirstOrDefaultAsync(o => o.OperacionId == operacionId && o.Tipo == TipoOfa.PRINCIPAL, ct);
    public Task<List<AsignacionCobro>> ListarAsignacionesAsync(Guid ofaId, CancellationToken ct) =>
        db.AsignacionesCobro.Where(a => a.OfaId == ofaId).OrderBy(a => a.Id).ToListAsync(ct);
    public Task<List<TarifaSnapshot>> ListarTarifasPublicasAsync(int version, IReadOnlyCollection<string> articulos, CancellationToken ct)
    {
        var publicas = db.SapListasPrecios.Where(l => l.SnapshotVersion == version && l.EsPublica).Select(l => l.Codigo);
        return db.TarifasSnapshot.AsNoTracking()
            .Where(t => t.Version == version && articulos.Contains(t.ArticuloId) && publicas.Contains(t.ListaPreciosId))
            .ToListAsync(ct);
    }
    public Task<decimal?> TipoCambioAsync(Moneda moneda, DateOnly fecha, CancellationToken ct) =>
        db.TiposCambio.AsNoTracking()
            .Where(t => t.Moneda == moneda && t.Fecha <= fecha)
            .OrderByDescending(t => t.Fecha)
            .Select(t => (decimal?)t.Valor)
            .FirstOrDefaultAsync(ct);
    public Task<VersionConvenio?> ObtenerVersionConvenioAsync(Guid versionConvenioId, CancellationToken ct) =>
        db.VersionesConvenio.AsNoTracking().Include(v => v.Servicios).FirstOrDefaultAsync(v => v.Id == versionConvenioId, ct);
    public async Task<Dictionary<Guid, int>> NumerosVersionConvenioAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.VersionesConvenio.AsNoTracking().Where(v => lista.Contains(v.Id)).ToDictionaryAsync(v => v.Id, v => v.Numero, ct);
    }
    public void Agregar(OfaEntidad ofa) => db.Ofas.Add(ofa);
    public void AgregarAsignacion(AsignacionCobro asignacion) => db.AsignacionesCobro.Add(asignacion);
    public void AgregarLinea(LineaOfa linea) => db.LineasOfa.Add(linea);
    public void AgregarMemoria(MemoriaCalculoRegistro memoria) => db.MemoriasCalculo.Add(memoria);
}
