using System.Text.Json;
using Satelite.Aplicacion.Convenios;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
namespace Satelite.Aplicacion.Operaciones;
/// <summary>
/// Pasos comunes a la ingesta y al PATCH de una operación: convenio vigente por RUT y fecha, clasificación de la ruta
/// y recálculo de los pendientes (propios y de las operaciones que comparten un documento de carga).
/// </summary>
public sealed class ResolutorOperacion(IOperacionesRepositorio repositorio, IConvenioService convenios)
{
    /// <summary>Fecha con que se busca el convenio. [SUPUESTO: la fecha UTC de visadoAprobadoEn; sin visado, la de ingreso]</summary>
    public static DateOnly FechaOperacion(Operacion op) => DateOnly.FromDateTime((op.VisadoAprobadoEn ?? op.IngeridoEn).UtcDateTime);
    /// <summary>RUT al que se factura: el cliente facturador si AEXSA lo indicó; si no, el consignatario (RN-077).</summary>
    public async Task<string?> RutFacturacionAsync(Operacion op, CancellationToken ct)
    {
        if (op.ClienteId is not { } id)
        {
            return null;
        }
        return (await repositorio.ObtenerClientesAsync([id], ct)).GetValueOrDefault(id)?.Rut;
    }
    /// <summary>
    /// Busca el convenio vigente (RN-043, RN-071; con varios, el más antiguo, RN-018) y lo fija en la operación.
    /// Si el facturador ya decidió entre varios (VARIOS_CONVENIOS_VIGENTES), se respeta su elección mientras ese
    /// convenio siga en vigor. Devuelve todas las versiones en vigor (candidatas).
    /// </summary>
    public async Task<IReadOnlyList<VersionConvenio>> ResolverConvenioAsync(Operacion op, string? rut, CancellationToken ct)
    {
        var vigente = rut is null ? null : await convenios.BuscarVigenteAsync(rut, FechaOperacion(op), ct);
        if (vigente is null)
        {
            op.ConConvenio = false;
            op.VersionConvenioId = null;
            return [];
        }
        var elegida = vigente.Version;
        var decision = op.Pendientes
            .Where(p => p.Tipo == TipoPendiente.VARIOS_CONVENIOS_VIGENTES && p.Resolucion == ResolucionPendiente.DECISION)
            .OrderByDescending(p => p.ResueltoEn)
            .Select(p => LeerGuid(p.Decision, "convenioId"))
            .FirstOrDefault();
        if (decision is { } convenioElegido && vigente.Candidatos.FirstOrDefault(v => v.ConvenioId == convenioElegido) is { } candidata)
        {
            elegida = candidata;
        }
        op.ConConvenio = true;
        op.VersionConvenioId = elegida.Id;
        return vigente.Candidatos;
    }
    /// <summary>Versiones de convenio en vigor para el RUT a la fecha de la operación.</summary>
    public async Task<IReadOnlyList<VersionConvenio>> ResolverCandidatosAsync(string rut, Operacion op, CancellationToken ct) =>
        (await convenios.BuscarVigenteAsync(rut, FechaOperacion(op), ct))?.Candidatos ?? [];
    /// <summary>Clasifica la ruta y sincroniza los pendientes de la operación.</summary>
    public async Task RecalcularPendientesAsync(
        Operacion op, IReadOnlyList<VersionConvenio> candidatos, ResolucionPendiente resolucionCierre, string usuario,
        string? motivo, DateTimeOffset ahora, CancellationToken ct)
    {
        // El Administrador pudo mapear el terminal después del ingreso (pantalla T4): se toma el grupo vigente.
        if (op.TerminalAexsaId is { } terminalId && await repositorio.ObtenerTerminalAsync(terminalId, ct) is { } mapa)
        {
            op.TerminalGrupo = mapa.Grupo;
        }
        CalculadorPendientes.Clasificar(op);
        var codigos = op.Contenedores.SelectMany(CalculadorPendientes.Inspecciones).Select(i => i.Tipo).Distinct().ToList();
        var grupos = (await repositorio.ObtenerTiposInspeccionAsync(codigos, ct)).ToDictionary(t => t.Key, t => t.Value.Grupo);
        var otras = op.EstadoOperacion == EstadoOperacion.ACTIVA && op.Bls.Count > 0
            ? await repositorio.OperacionesActivasConBlsAsync(op.Id, op.Bls.Select(b => b.BlId).ToList(), ct)
            : [];
        var esperados = CalculadorPendientes.Evaluar(op, new EntornoPendientes(grupos, candidatos, otras));
        CalculadorPendientes.Sincronizar(op, esperados, ahora, usuario, resolucionCierre, motivo, repositorio.AgregarPendiente);
    }
    /// <summary>
    /// Abre o cierra DOCUMENTO_CON_VARIAS_SOLICITUDES en las otras operaciones activas que comparten un documento
    /// con <paramref name="op"/> (nota 13: el pendiente queda en ambas). Requiere los BLs de <paramref name="op"/> ya guardados.
    /// </summary>
    public async Task ActualizarOperacionesDelMismoDocumentoAsync(Operacion op, DateTimeOffset ahora, CancellationToken ct)
    {
        if (op.Bls.Count == 0)
        {
            return;
        }
        var relacionadas = await repositorio.OperacionesActivasConBlsAsync(op.Id, op.Bls.Select(b => b.BlId).ToList(), ct);
        foreach (var id in relacionadas)
        {
            var otra = await repositorio.ObtenerAsync(id, ct);
            if (otra is null)
            {
                continue;
            }
            var pares = await repositorio.OperacionesActivasConBlsAsync(otra.Id, otra.Bls.Select(b => b.BlId).ToList(), ct);
            var abierto = otra.Pendientes.FirstOrDefault(p => p.Tipo == TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES && p.ResueltoEn is null);
            var decidido = otra.Pendientes.Any(p => p.Tipo == TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES && p.Resolucion == ResolucionPendiente.DECISION);
            var detalle = JsonSatelite.Serializar(new { operaciones = pares, bls = otra.Bls.Select(b => b.BlId).ToList() });
            if (pares.Count > 0 && abierto is null && !decidido && otra.Facturable)
            {
                var nuevo = new OperacionPendiente
                {
                    Id = Guid.CreateVersion7(),
                    OperacionId = otra.Id,
                    Tipo = TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES,
                    BloqueaValidacion = true,
                    Detalle = detalle,
                    DetectadoEn = ahora,
                };
                otra.Pendientes.Add(nuevo);
                repositorio.AgregarPendiente(nuevo);
            }
            else if (pares.Count > 0 && abierto is not null)
            {
                abierto.Detalle = detalle;
            }
        }
    }
    /// <summary>El facturador decidió no facturar la operación (documento con varias solicitudes o inspección rechazada).</summary>
    public static bool DecidioNoFacturar(Operacion op) =>
        op.Pendientes.Any(p => p.Resolucion == ResolucionPendiente.DECISION && LeerTexto(p.Decision, "valor") == ValoresDecision.NoFacturar);
    private static Guid? LeerGuid(string? json, string propiedad) =>
        Guid.TryParse(LeerTexto(json, propiedad), out var id) ? id : null;
    private static string? LeerTexto(string? json, string propiedad)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(propiedad, out var v)
               && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}
/// <summary>Valores de <see cref="DecisionPendienteDto.Valor"/>.</summary>
public static class ValoresDecision
{
    public const string Facturar = "FACTURAR";
    public const string NoFacturar = "NO_FACTURAR";
    public const string Continuar = "CONTINUAR";
}
