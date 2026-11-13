using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Aplicacion.Convenios;

/// <summary>Puerto de persistencia de clientes y convenios versionados.</summary>
public interface IConveniosRepositorio
{
    /// <summary>Convenios con su cliente y sus versiones (sin servicios). Filtra por RUT normalizado y por estado de alguna versión.</summary>
    Task<List<Convenio>> ListarAsync(string? rut, EstadoVersionConvenio? estado, CancellationToken ct);

    /// <summary>Convenio con su cliente, versiones y servicios, con seguimiento de cambios.</summary>
    Task<Convenio?> ObtenerAsync(Guid convenioId, CancellationToken ct);

    Task<Cliente?> ObtenerClientePorRutAsync(string rut, CancellationToken ct);

    /// <summary>Versiones VIGENTE del RUT cuya vigencia cubre la fecha, con convenio, cliente y servicios.</summary>
    Task<List<VersionConvenio>> ListarVersionesEnVigorAsync(string rut, DateOnly fecha, CancellationToken ct);

    void AgregarCliente(Cliente cliente);
    void AgregarConvenio(Convenio convenio);
    void AgregarVersion(VersionConvenio version);
}
