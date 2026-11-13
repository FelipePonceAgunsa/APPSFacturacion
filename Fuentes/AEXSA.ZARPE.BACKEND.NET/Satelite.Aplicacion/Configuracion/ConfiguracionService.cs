using Satelite.Aplicacion.Comun;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Aplicacion.Configuracion;

public sealed record TerminalMapaDto(int TerminalId, string Nombre, GrupoTerminal Grupo, int OperacionesPendientes);

public sealed record TipoInspeccionMapaDto(string Codigo, string? Descripcion, GrupoInspeccion Grupo, int OperacionesPendientes);

public sealed record GrupoTerminalRequest(GrupoTerminal Grupo);

public sealed record GrupoInspeccionRequest(GrupoInspeccion Grupo);

/// <summary>Puerto de persistencia de los mapeos que mantiene el Administrador.</summary>
public interface IConfiguracionRepositorio
{
    Task<List<TerminalMapa>> ListarTerminalesAsync(CancellationToken ct);
    Task<TerminalMapa?> ObtenerTerminalAsync(int terminalId, CancellationToken ct);

    /// <summary>Terminal → operaciones con un pendiente TERMINAL_SIN_MAPEAR abierto.</summary>
    Task<Dictionary<int, int>> PendientesPorTerminalAsync(CancellationToken ct);

    Task<List<TipoInspeccionMapa>> ListarTiposInspeccionAsync(CancellationToken ct);
    Task<TipoInspeccionMapa?> ObtenerTipoInspeccionAsync(string codigo, CancellationToken ct);

    /// <summary>Código de inspección → operaciones con un pendiente DIAS_AFORO abierto y una inspección de ese tipo.</summary>
    Task<Dictionary<string, int>> PendientesPorTipoInspeccionAsync(CancellationToken ct);
}

public interface IConfiguracionService
{
    Task<ServiceResult<List<TerminalMapaDto>>> ListarTerminalesAsync(CancellationToken ct);
    Task<ServiceResult<TerminalMapaDto>> AsignarGrupoTerminalAsync(int terminalId, GrupoTerminalRequest request, CancellationToken ct);
    Task<ServiceResult<List<TipoInspeccionMapaDto>>> ListarTiposInspeccionAsync(CancellationToken ct);
    Task<ServiceResult<TipoInspeccionMapaDto>> AsignarGrupoInspeccionAsync(string codigo, GrupoInspeccionRequest request, CancellationToken ct);
}

/// <summary>
/// Mapeo de terminales (RN-079, pantalla T4) y de tipos de inspección ([E1-7], RN-052).
/// Los códigos nuevos los agrega la ingesta de AEXSA como SIN_MAPEAR; aquí solo se asigna su grupo.
/// </summary>
public sealed class ConfiguracionService(
    IConfiguracionRepositorio repositorio,
    IUnidadTrabajo unidadTrabajo,
    IAuditoria auditoria,
    ICurrentUser usuario,
    TimeProvider reloj) : IConfiguracionService
{
    public async Task<ServiceResult<List<TerminalMapaDto>>> ListarTerminalesAsync(CancellationToken ct)
    {
        var terminales = await repositorio.ListarTerminalesAsync(ct);
        var pendientes = await repositorio.PendientesPorTerminalAsync(ct);
        return ServiceResult<List<TerminalMapaDto>>.Ok(terminales
            .Select(t => Dto(t, pendientes.GetValueOrDefault(t.TerminalAexsaId)))
            .ToList());
    }

    public async Task<ServiceResult<TerminalMapaDto>> AsignarGrupoTerminalAsync(int terminalId, GrupoTerminalRequest request, CancellationToken ct)
    {
        if (request.Grupo == GrupoTerminal.SIN_MAPEAR)
        {
            return ServiceResult<TerminalMapaDto>.Invalido("El grupo debe ser STI, DP_WORLD u OTRO.");
        }

        var terminal = await repositorio.ObtenerTerminalAsync(terminalId, ct);
        if (terminal is null)
        {
            return ServiceResult<TerminalMapaDto>.NoEncontrado($"El terminal {terminalId} no existe.");
        }

        var anterior = new { terminal.TerminalAexsaId, terminal.Grupo };
        terminal.Grupo = request.Grupo;
        terminal.ActualizadoPor = usuario.UsuarioAuditoria;
        terminal.ActualizadoEn = reloj.GetUtcNow();
        auditoria.Registrar("terminal_mapa", null, "ASIGNAR_GRUPO", anterior, new { terminal.TerminalAexsaId, terminal.Grupo });
        await unidadTrabajo.GuardarAsync(ct);

        var pendientes = await repositorio.PendientesPorTerminalAsync(ct);
        return ServiceResult<TerminalMapaDto>.Ok(Dto(terminal, pendientes.GetValueOrDefault(terminalId)));
    }

    public async Task<ServiceResult<List<TipoInspeccionMapaDto>>> ListarTiposInspeccionAsync(CancellationToken ct)
    {
        var tipos = await repositorio.ListarTiposInspeccionAsync(ct);
        var pendientes = await repositorio.PendientesPorTipoInspeccionAsync(ct);
        return ServiceResult<List<TipoInspeccionMapaDto>>.Ok(tipos
            .Select(t => Dto(t, pendientes.GetValueOrDefault(t.CodigoAexsa)))
            .ToList());
    }

    public async Task<ServiceResult<TipoInspeccionMapaDto>> AsignarGrupoInspeccionAsync(string codigo, GrupoInspeccionRequest request, CancellationToken ct)
    {
        if (request.Grupo == GrupoInspeccion.SIN_MAPEAR)
        {
            return ServiceResult<TipoInspeccionMapaDto>.Invalido("El grupo debe ser AFORO_ADUANA, SAG u OTRA.");
        }

        var tipo = await repositorio.ObtenerTipoInspeccionAsync(codigo, ct);
        if (tipo is null)
        {
            return ServiceResult<TipoInspeccionMapaDto>.NoEncontrado($"El tipo de inspección {codigo} no existe.");
        }

        var anterior = new { tipo.CodigoAexsa, tipo.Grupo };
        tipo.Grupo = request.Grupo;
        tipo.ActualizadoPor = usuario.UsuarioAuditoria;
        tipo.ActualizadoEn = reloj.GetUtcNow();
        auditoria.Registrar("tipo_inspeccion_mapa", null, "ASIGNAR_GRUPO", anterior, new { tipo.CodigoAexsa, tipo.Grupo });
        await unidadTrabajo.GuardarAsync(ct);

        var pendientes = await repositorio.PendientesPorTipoInspeccionAsync(ct);
        return ServiceResult<TipoInspeccionMapaDto>.Ok(Dto(tipo, pendientes.GetValueOrDefault(tipo.CodigoAexsa)));
    }

    private static TerminalMapaDto Dto(TerminalMapa t, int pendientes) => new(t.TerminalAexsaId, t.Nombre, t.Grupo, pendientes);

    private static TipoInspeccionMapaDto Dto(TipoInspeccionMapa t, int pendientes) => new(t.CodigoAexsa, t.Descripcion, t.Grupo, pendientes);
}
