using Microsoft.EntityFrameworkCore;
using Satelite.Aplicacion.Convenios;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Infraestructura.Persistencia;

namespace Satelite.Infraestructura.Repositorios;

internal sealed class ConveniosRepositorio(PrincipalDbContext db) : IConveniosRepositorio
{
    public Task<List<Convenio>> ListarAsync(string? rut, EstadoVersionConvenio? estado, CancellationToken ct)
    {
        var consulta = db.Convenios.AsNoTracking().Include(c => c.Cliente).Include(c => c.Versiones).AsQueryable();
        if (rut is not null)
        {
            consulta = consulta.Where(c => c.Cliente!.Rut == rut);
        }
        if (estado is not null)
        {
            consulta = consulta.Where(c => c.Versiones.Any(v => v.Estado == estado));
        }
        return consulta.OrderBy(c => c.Cliente!.Rut).ThenBy(c => c.CreadoEn).AsSplitQuery().ToListAsync(ct);
    }

    public Task<Convenio?> ObtenerAsync(Guid convenioId, CancellationToken ct) =>
        db.Convenios
            .Include(c => c.Cliente)
            .Include(c => c.Versiones).ThenInclude(v => v.Servicios)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == convenioId, ct);

    public Task<Cliente?> ObtenerClientePorRutAsync(string rut, CancellationToken ct) =>
        db.Clientes.FirstOrDefaultAsync(c => c.Rut == rut, ct);

    public Task<List<VersionConvenio>> ListarVersionesEnVigorAsync(string rut, DateOnly fecha, CancellationToken ct) =>
        db.VersionesConvenio.AsNoTracking()
            .Include(v => v.Convenio).ThenInclude(c => c!.Cliente)
            .Include(v => v.Servicios)
            .Where(v => v.Estado == EstadoVersionConvenio.VIGENTE
                        && v.Convenio!.Cliente!.Rut == rut
                        && v.VigenciaDesde <= fecha
                        && (v.VigenciaHasta == null || v.VigenciaHasta >= fecha))
            .AsSplitQuery()
            .ToListAsync(ct);

    public void AgregarCliente(Cliente cliente) => db.Clientes.Add(cliente);

    public void AgregarConvenio(Convenio convenio) => db.Convenios.Add(convenio);

    public void AgregarVersion(VersionConvenio version) => db.VersionesConvenio.Add(version);
}
