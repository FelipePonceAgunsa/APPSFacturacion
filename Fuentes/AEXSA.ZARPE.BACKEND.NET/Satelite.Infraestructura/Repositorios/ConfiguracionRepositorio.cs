using Microsoft.EntityFrameworkCore;
using Satelite.Aplicacion.Configuracion;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Infraestructura.Persistencia;

namespace Satelite.Infraestructura.Repositorios;

internal sealed class ConfiguracionRepositorio(PrincipalDbContext db) : IConfiguracionRepositorio
{
    public Task<List<TerminalMapa>> ListarTerminalesAsync(CancellationToken ct) =>
        db.TerminalesMapa.AsNoTracking().OrderBy(t => t.TerminalAexsaId).ToListAsync(ct);

    public Task<TerminalMapa?> ObtenerTerminalAsync(int terminalId, CancellationToken ct) =>
        db.TerminalesMapa.FirstOrDefaultAsync(t => t.TerminalAexsaId == terminalId, ct);

    public async Task<Dictionary<int, int>> PendientesPorTerminalAsync(CancellationToken ct)
    {
        var filas = await db.Operaciones
            .Where(o => o.TerminalAexsaId != null
                        && db.OperacionesPendientes.Any(p => p.OperacionId == o.Id
                                                             && p.Tipo == TipoPendiente.TERMINAL_SIN_MAPEAR
                                                             && p.ResueltoEn == null))
            .GroupBy(o => o.TerminalAexsaId!.Value)
            .Select(g => new { Terminal = g.Key, Cantidad = g.Count() })
            .ToListAsync(ct);
        return filas.ToDictionary(f => f.Terminal, f => f.Cantidad);
    }

    public Task<List<TipoInspeccionMapa>> ListarTiposInspeccionAsync(CancellationToken ct) =>
        db.TiposInspeccionMapa.AsNoTracking().OrderBy(t => t.CodigoAexsa).ToListAsync(ct);

    public Task<TipoInspeccionMapa?> ObtenerTipoInspeccionAsync(string codigo, CancellationToken ct) =>
        db.TiposInspeccionMapa.FirstOrDefaultAsync(t => t.CodigoAexsa == codigo, ct);

    public async Task<Dictionary<string, int>> PendientesPorTipoInspeccionAsync(CancellationToken ct)
    {
        var filas = await db.Database.SqlQueryRaw<ConteoPorCodigo>("""
            SELECT i.value->>'tipo' AS "Codigo", count(DISTINCT c.operacion_id)::int AS "Cantidad"
            FROM contenedor_operacion c
            CROSS JOIN LATERAL jsonb_array_elements(c.inspecciones) AS i(value)
            WHERE c.inspecciones IS NOT NULL
              AND EXISTS (SELECT 1 FROM operacion_pendiente p
                          WHERE p.operacion_id = c.operacion_id AND p.tipo = 'DIAS_AFORO' AND p.resuelto_en IS NULL)
            GROUP BY i.value->>'tipo'
            """).ToListAsync(ct);
        return filas.Where(f => f.Codigo is not null).ToDictionary(f => f.Codigo!, f => f.Cantidad);
    }

    private sealed class ConteoPorCodigo
    {
        public string? Codigo { get; set; }
        public int Cantidad { get; set; }
    }
}
