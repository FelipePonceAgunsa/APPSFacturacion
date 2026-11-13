using Microsoft.EntityFrameworkCore;
using Satelite.Aplicacion.Operaciones;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Infraestructura.Persistencia;
namespace Satelite.Infraestructura.Repositorios;
internal sealed class OperacionesRepositorio(PrincipalDbContext db) : IOperacionesRepositorio
{
    public async Task BloquearAsync(EntidadAexsa entidad, string clave, CancellationToken ct)
    {
        // modelo-datos.md §4b: la fila queda bloqueada hasta el fin de la transacción.
        var texto = entidad.ToString();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM operacion WHERE entidad = {texto} AND operacion_id_aexsa = {clave} FOR UPDATE", ct);
    }
    private IQueryable<Operacion> ConHijos() =>
        db.Operaciones
            .Include(o => o.Bls)
            .Include(o => o.Contenedores)
            .Include(o => o.Servicios)
            .Include(o => o.Pendientes)
            .AsSplitQuery();
    public Task<Operacion?> ObtenerPorClaveAsync(EntidadAexsa entidad, string clave, CancellationToken ct) =>
        ConHijos().FirstOrDefaultAsync(o => o.Entidad == entidad && o.OperacionIdAexsa == clave, ct);
    public Task<Operacion?> ObtenerAsync(Guid operacionId, CancellationToken ct) =>
        ConHijos().FirstOrDefaultAsync(o => o.Id == operacionId, ct);
    public Task<List<Operacion>> ListarAsync(bool? facturable, string? rut, CancellationToken ct)
    {
        var consulta = db.Operaciones.AsNoTracking().Include(o => o.Pendientes).AsQueryable();
        if (facturable is { } f)
        {
            consulta = consulta.Where(o => o.Facturable == f);
        }
        if (rut is not null)
        {
            var clientes = db.Clientes.Where(c => c.Rut == rut).Select(c => c.Id);
            consulta = consulta.Where(o => (o.ClienteId != null && clientes.Contains(o.ClienteId.Value))
                                           || (o.ConsignatarioId != null && clientes.Contains(o.ConsignatarioId.Value)));
        }
        return consulta.OrderByDescending(o => o.VisadoAprobadoEn).ThenByDescending(o => o.IngeridoEn).AsSplitQuery().ToListAsync(ct);
    }
    public Task<List<Guid>> OperacionesActivasConBlsAsync(Guid excluir, IReadOnlyCollection<long> blIds, CancellationToken ct) =>
        db.Operaciones
            .Where(o => o.Id != excluir && o.EstadoOperacion == EstadoOperacion.ACTIVA
                        && db.OperacionesBl.Any(b => b.OperacionId == o.Id && blIds.Contains(b.BlId)))
            .OrderBy(o => o.IngeridoEn)
            .Select(o => o.Id)
            .ToListAsync(ct);
    public Task<List<string>> AuditoriasCompletarDatosAsync(Guid operacionId, CancellationToken ct) =>
        db.EventosAuditoria.AsNoTracking()
            .Where(a => a.Entidad == "operacion" && a.EntidadId == operacionId && a.Accion == OperacionService.AccionCompletar && a.ValorNuevo != null)
            .OrderBy(a => a.Id)
            .Select(a => a.ValorNuevo!)
            .ToListAsync(ct);
    public async Task<Dictionary<Guid, Cliente>> ObtenerClientesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        return await db.Clientes.Where(c => lista.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
    }
    public Task<Cliente?> ObtenerClientePorRutAsync(string rut, CancellationToken ct) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Rut == rut, ct);
    public Task<TerminalMapa?> ObtenerTerminalAsync(int terminalId, CancellationToken ct) =>
        db.TerminalesMapa.FirstOrDefaultAsync(t => t.TerminalAexsaId == terminalId, ct);
    public async Task<Dictionary<string, TipoInspeccionMapa>> ObtenerTiposInspeccionAsync(IEnumerable<string> codigos, CancellationToken ct)
    {
        var lista = codigos.Distinct().ToList();
        return await db.TiposInspeccionMapa.Where(t => lista.Contains(t.CodigoAexsa)).ToDictionaryAsync(t => t.CodigoAexsa, ct);
    }
    public async Task<Dictionary<Guid, (Guid OfaId, EstadoOfa Estado)>> OfasDeOperacionesAsync(IEnumerable<Guid> operacionIds, CancellationToken ct)
    {
        var lista = operacionIds.Distinct().ToList();
        var filas = await db.Ofas.AsNoTracking()
            .Where(o => o.Tipo == TipoOfa.PRINCIPAL && lista.Contains(o.OperacionId))
            .Select(o => new { o.OperacionId, o.Id, o.Estado })
            .ToListAsync(ct);
        return filas.ToDictionary(f => f.OperacionId, f => (f.Id, f.Estado));
    }
    public Task<VersionConvenio?> ObtenerVersionConvenioAsync(Guid versionConvenioId, CancellationToken ct) =>
        db.VersionesConvenio.AsNoTracking().FirstOrDefaultAsync(v => v.Id == versionConvenioId, ct);
    public void AgregarOperacion(Operacion operacion) => db.Operaciones.Add(operacion);
    public void AgregarEvento(EventoAexsa evento) => db.EventosAexsa.Add(evento);
    public void AgregarCliente(Cliente cliente) => db.Clientes.Add(cliente);
    public void AgregarTerminal(TerminalMapa terminal) => db.TerminalesMapa.Add(terminal);
    public void AgregarTipoInspeccion(TipoInspeccionMapa tipo) => db.TiposInspeccionMapa.Add(tipo);
    public void AgregarBl(OperacionBl bl) => db.OperacionesBl.Add(bl);
    public void AgregarContenedor(ContenedorOperacion contenedor) => db.ContenedoresOperacion.Add(contenedor);
    public void AgregarServicio(ServicioPrestado servicio) => db.ServiciosPrestados.Add(servicio);
    public void AgregarPendiente(OperacionPendiente pendiente) => db.OperacionesPendientes.Add(pendiente);
    public void Eliminar(object entidad) => db.Remove(entidad);
}
