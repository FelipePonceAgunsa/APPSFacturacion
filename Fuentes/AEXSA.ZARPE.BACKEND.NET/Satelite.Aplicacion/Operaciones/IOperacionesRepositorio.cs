using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
namespace Satelite.Aplicacion.Operaciones;
/// <summary>Puerto de persistencia de operaciones, mensajes de AEXSA y mapeos que la ingesta completa.</summary>
public interface IOperacionesRepositorio
{
    /// <summary>
    /// <c>SELECT … FOR UPDATE</c> sobre la operación (modelo-datos.md §4b). Debe correr dentro de una transacción;
    /// si la operación no existe no bloquea nada (el índice único resuelve dos altas simultáneas).
    /// </summary>
    Task BloquearAsync(EntidadAexsa entidad, string clave, CancellationToken ct);
    /// <summary>Operación con BLs, contenedores, servicios y pendientes, con seguimiento de cambios.</summary>
    Task<Operacion?> ObtenerPorClaveAsync(EntidadAexsa entidad, string clave, CancellationToken ct);
    /// <summary>Operación con BLs, contenedores, servicios y pendientes, con seguimiento de cambios.</summary>
    Task<Operacion?> ObtenerAsync(Guid operacionId, CancellationToken ct);
    /// <summary>Operaciones con sus pendientes, sin seguimiento. <paramref name="rut"/> normalizado: facturador o consignatario.</summary>
    Task<List<Operacion>> ListarAsync(bool? facturable, string? rut, CancellationToken ct);
    /// <summary>Otras operaciones ACTIVAS que comparten algún documento de carga (bls[].id, nota 13).</summary>
    Task<List<Guid>> OperacionesActivasConBlsAsync(Guid excluir, IReadOnlyCollection<long> blIds, CancellationToken ct);
    /// <summary>Valor nuevo (JSON) de las auditorías COMPLETAR_DATOS de la operación, de la más antigua a la más nueva.</summary>
    Task<List<string>> AuditoriasCompletarDatosAsync(Guid operacionId, CancellationToken ct);
    Task<Dictionary<Guid, Cliente>> ObtenerClientesAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task<Cliente?> ObtenerClientePorRutAsync(string rut, CancellationToken ct);
    Task<TerminalMapa?> ObtenerTerminalAsync(int terminalId, CancellationToken ct);
    Task<Dictionary<string, TipoInspeccionMapa>> ObtenerTiposInspeccionAsync(IEnumerable<string> codigos, CancellationToken ct);
    /// <summary>OFA principal de cada operación (id y estado).</summary>
    Task<Dictionary<Guid, (Guid OfaId, EstadoOfa Estado)>> OfasDeOperacionesAsync(IEnumerable<Guid> operacionIds, CancellationToken ct);
    Task<VersionConvenio?> ObtenerVersionConvenioAsync(Guid versionConvenioId, CancellationToken ct);
    void AgregarOperacion(Operacion operacion);
    void AgregarEvento(EventoAexsa evento);
    void AgregarCliente(Cliente cliente);
    void AgregarTerminal(TerminalMapa terminal);
    void AgregarTipoInspeccion(TipoInspeccionMapa tipo);
    void AgregarBl(OperacionBl bl);
    void AgregarContenedor(ContenedorOperacion contenedor);
    void AgregarServicio(ServicioPrestado servicio);
    void AgregarPendiente(OperacionPendiente pendiente);
    void Eliminar(object entidad);
}
