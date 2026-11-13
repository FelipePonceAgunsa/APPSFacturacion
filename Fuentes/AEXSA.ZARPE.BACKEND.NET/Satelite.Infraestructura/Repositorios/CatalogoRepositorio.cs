using Microsoft.EntityFrameworkCore;
using Satelite.Aplicacion.Catalogo;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Infraestructura.Persistencia;

namespace Satelite.Infraestructura.Repositorios;

internal sealed class CatalogoRepositorio(PrincipalDbContext db) : ICatalogoRepositorio
{
    public Task<List<TipoServicio>> ListarTiposServicioAsync(CancellationToken ct) =>
        db.TiposServicio.AsNoTracking().OrderBy(t => t.TipoServicioId).ToListAsync(ct);

    public Task<TipoServicio?> ObtenerTipoServicioAsync(int tipoServicioId, CancellationToken ct) =>
        db.TiposServicio.AsNoTracking().FirstOrDefaultAsync(t => t.TipoServicioId == tipoServicioId, ct);

    public Task<CatalogoSnapshot?> ObtenerVersionVigenteAsync(CancellationToken ct) =>
        db.CatalogoSnapshots.AsNoTracking().OrderByDescending(s => s.Version).FirstOrDefaultAsync(ct);

    public async Task<(int Articulos, int ListasPrecios, int Kits)> ContarAsync(int version, CancellationToken ct) =>
        (await db.SapArticulos.CountAsync(a => a.SnapshotVersion == version, ct),
         await db.SapListasPrecios.CountAsync(l => l.SnapshotVersion == version, ct),
         await db.SapKits.CountAsync(k => k.SnapshotVersion == version, ct));

    public Task<List<SapArticulo>> ListarArticulosAsync(int version, string? busqueda, CancellationToken ct)
    {
        var consulta = db.SapArticulos.AsNoTracking().Where(a => a.SnapshotVersion == version);
        if (!string.IsNullOrEmpty(busqueda))
        {
            var patron = $"%{busqueda.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            consulta = consulta.Where(a => EF.Functions.ILike(a.Codigo, patron) || EF.Functions.ILike(a.Descripcion, patron));
        }
        return consulta.OrderBy(a => a.Codigo).ToListAsync(ct);
    }

    public Task<bool> ExisteArticuloAsync(int version, string codigo, CancellationToken ct) =>
        db.SapArticulos.AnyAsync(a => a.SnapshotVersion == version && a.Codigo == codigo, ct);

    public Task<SapListaPrecios?> ObtenerListaPreciosAsync(int version, string codigo, CancellationToken ct) =>
        db.SapListasPrecios.AsNoTracking().FirstOrDefaultAsync(l => l.SnapshotVersion == version && l.Codigo == codigo, ct);

    public Task<List<TarifaSnapshot>> ListarPreciosAsync(int version, string codigoLista, CancellationToken ct) =>
        db.TarifasSnapshot.AsNoTracking()
            .Where(t => t.Version == version && t.ListaPreciosId == codigoLista)
            .OrderBy(t => t.ArticuloId).ThenBy(t => t.VigenteDesde)
            .ToListAsync(ct);

    public Task<List<SapKit>> ListarKitsAsync(int version, string? ruta, CancellationToken ct)
    {
        var consulta = db.SapKits.AsNoTracking().Include(k => k.Articulos).Where(k => k.SnapshotVersion == version);
        if (ruta is not null)
        {
            consulta = consulta.Where(k => k.Ruta == ruta);
        }
        return consulta.OrderBy(k => k.KitCodigo).ToListAsync(ct);
    }

    public Task<Dictionary<string, bool>> ActivoPorArticuloAsync(int version, CancellationToken ct) =>
        db.SapArticulos.AsNoTracking().Where(a => a.SnapshotVersion == version).ToDictionaryAsync(a => a.Codigo, a => a.Activo, ct);

    public async Task<HashSet<string>> ArticulosReferenciadosAsync(int version, CancellationToken ct)
    {
        var enListas = await db.TarifasSnapshot.Where(t => t.Version == version).Select(t => t.ArticuloId).Distinct().ToListAsync(ct);
        var enKits = await db.SapKitArticulos.Where(k => k.SnapshotVersion == version).Select(k => k.ArticuloId).Distinct().ToListAsync(ct);
        return [.. enListas, .. enKits];
    }

    public Task<List<TipoCambio>> ListarTiposCambioAsync(DateOnly? desde, Moneda? moneda, CancellationToken ct)
    {
        var consulta = db.TiposCambio.AsNoTracking();
        if (desde is not null)
        {
            consulta = consulta.Where(t => t.Fecha >= desde);
        }
        if (moneda is not null)
        {
            consulta = consulta.Where(t => t.Moneda == moneda);
        }
        return consulta.OrderByDescending(t => t.Fecha).ThenBy(t => t.Moneda).ToListAsync(ct);
    }

    public async Task<DateOnly?> UltimaFechaTipoCambioAsync(CancellationToken ct) =>
        await db.TiposCambio.MaxAsync(t => (DateOnly?)t.Fecha, ct);

    public Task<List<TipoCambio>> ObtenerTiposCambioAsync(DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        db.TiposCambio.Where(t => t.Fecha >= desde && t.Fecha <= hasta).ToListAsync(ct);

    public void AgregarTipoCambio(TipoCambio tipoCambio) => db.TiposCambio.Add(tipoCambio);

    public Task<bool> ExisteImportacionConfirmadaConHashAsync(string hash, CancellationToken ct) =>
        db.CatalogoImportaciones.AnyAsync(i => i.HashContenido == hash && i.Estado == EstadoImportacion.CONFIRMADA, ct);

    public void AgregarImportacion(CatalogoImportacion importacion) => db.CatalogoImportaciones.Add(importacion);

    public Task<CatalogoImportacion?> ObtenerImportacionAsync(Guid id, CancellationToken ct) =>
        db.CatalogoImportaciones.FirstOrDefaultAsync(i => i.Id == id, ct);

    public void AgregarSnapshot(CatalogoSnapshot snapshot) => db.CatalogoSnapshots.Add(snapshot);

    public void AgregarArticulos(IEnumerable<SapArticulo> articulos) => db.SapArticulos.AddRange(articulos);

    public void AgregarListasPrecios(IEnumerable<SapListaPrecios> listas, IEnumerable<TarifaSnapshot> tarifas)
    {
        db.SapListasPrecios.AddRange(listas);
        db.TarifasSnapshot.AddRange(tarifas);
    }

    public void AgregarKits(IEnumerable<SapKit> kits) => db.SapKits.AddRange(kits);

    public Task CopiarArticulosAsync(int desde, int hacia, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sap_articulo (snapshot_version, codigo, descripcion, unidad_medida, activo, grupo)
            SELECT {hacia}, codigo, descripcion, unidad_medida, activo, grupo
            FROM sap_articulo WHERE snapshot_version = {desde}
            """, ct);

    public async Task CopiarListasPreciosAsync(int desde, int hacia, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sap_lista_precios (snapshot_version, codigo, nombre, es_publica)
            SELECT {hacia}, codigo, nombre, es_publica
            FROM sap_lista_precios WHERE snapshot_version = {desde}
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO tarifa_snapshot (version, fecha, articulo_id, lista_precios_id, precio, moneda, vigente_desde, vigente_hasta)
            SELECT {hacia}, fecha, articulo_id, lista_precios_id, precio, moneda, vigente_desde, vigente_hasta
            FROM tarifa_snapshot WHERE version = {desde}
            """, ct);
    }

    public async Task CopiarKitsAsync(int desde, int hacia, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sap_kit (snapshot_version, kit_codigo, nombre, ruta)
            SELECT {hacia}, kit_codigo, nombre, ruta
            FROM sap_kit WHERE snapshot_version = {desde}
            """, ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sap_kit_articulo (kit_id, snapshot_version, articulo_id, cantidad)
            SELECT nuevo.id, {hacia}, ka.articulo_id, ka.cantidad
            FROM sap_kit_articulo ka
            JOIN sap_kit viejo ON viejo.id = ka.kit_id
            JOIN sap_kit nuevo ON nuevo.snapshot_version = {hacia} AND nuevo.kit_codigo = viejo.kit_codigo
            WHERE ka.snapshot_version = {desde}
            """, ct);
    }

    public Task<List<ServicioArticuloSap>> ListarVinculosAsync(CancellationToken ct) =>
        db.ServiciosArticuloSap.AsNoTracking().ToListAsync(ct);

    public Task<List<ServicioArticuloSap>> ObtenerVinculosDeServicioAsync(int tipoServicioId, CancellationToken ct) =>
        db.ServiciosArticuloSap.Where(v => v.TipoServicioId == tipoServicioId).ToListAsync(ct);

    public void AgregarVinculo(ServicioArticuloSap vinculo) => db.ServiciosArticuloSap.Add(vinculo);

    public void EliminarVinculo(ServicioArticuloSap vinculo) => db.ServiciosArticuloSap.Remove(vinculo);
}
