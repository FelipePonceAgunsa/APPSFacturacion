using System.Text.Json;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Comun;
using Satelite.Aplicacion.Ofa;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
namespace Satelite.Aplicacion.Operaciones;
public interface IOperacionService
{
    Task<ServiceResult<List<OperacionResumenDto>>> ListarAsync(bool? facturable, string? rut, CancellationToken ct);
    Task<ServiceResult<OperacionDto>> ObtenerAsync(Guid operacionId, CancellationToken ct);
    /// <summary>Alta manual (POST /operaciones): mismo esquema e idempotencia que la ingesta de AEXSA.</summary>
    Task<ServiceResult<OperacionDto>> RegistrarAsync(JsonElement evento, CancellationToken ct);
    /// <summary>Completar o corregir datos con motivo (PATCH /operaciones/{id}), auditable.</summary>
    Task<ServiceResult<OperacionDto>> CompletarAsync(Guid operacionId, CompletarOperacionRequest request, CancellationToken ct);
}
/// <summary>Consulta de operaciones (F1, F2) y completado de datos por el facturador (tarea 3.1a).</summary>
public sealed class OperacionService(
    IOperacionesRepositorio repositorio,
    ICatalogoRepositorio catalogo,
    IIngestaEventoFacturable ingesta,
    ResolutorOperacion resolutor,
    IOfaService ofas,
    IUnidadTrabajo unidadTrabajo,
    IAuditoria auditoria,
    ICurrentUser usuario,
    TimeProvider reloj) : IOperacionService
{
    public const string OfaYaEnviada = "OFA_YA_ENVIADA";
    public const string OperacionAnulada = "OPERACION_ANULADA";
    public const string AccionCompletar = "COMPLETAR_DATOS";
    public async Task<ServiceResult<List<OperacionResumenDto>>> ListarAsync(bool? facturable, string? rut, CancellationToken ct)
    {
        string? normalizado = null;
        if (!string.IsNullOrWhiteSpace(rut))
        {
            normalizado = Rut.Normalizar(rut);
            if (normalizado is null)
            {
                return ServiceResult<List<OperacionResumenDto>>.Invalido($"RUT inválido: {rut}");
            }
        }
        var operaciones = await repositorio.ListarAsync(facturable, normalizado, ct);
        var clientes = await repositorio.ObtenerClientesAsync(operaciones.Where(o => o.ClienteId != null).Select(o => o.ClienteId!.Value).Distinct(), ct);
        var ofasPorOperacion = await repositorio.OfasDeOperacionesAsync(operaciones.Select(o => o.Id), ct);
        return ServiceResult<List<OperacionResumenDto>>.Ok(operaciones.Select(o =>
        {
            var cliente = o.ClienteId is { } id ? clientes.GetValueOrDefault(id) : null;
            var ofa = ofasPorOperacion.TryGetValue(o.Id, out var f) ? f : ((Guid, EstadoOfa)?)null;
            return new OperacionResumenDto(o.Id, o.Entidad, o.IdsAexsa, cliente?.Rut, cliente?.RazonSocial, o.ConConvenio, o.Facturable,
                o.EstadoOperacion, o.TipoCarga, o.TipoRetiro, o.RutaClasificada, o.VisadoAprobadoEn, o.VersionEvento,
                Abiertos(o), ofa?.Item1, ofa?.Item2);
        }).ToList());
    }
    public async Task<ServiceResult<OperacionDto>> ObtenerAsync(Guid operacionId, CancellationToken ct)
    {
        var op = await repositorio.ObtenerAsync(operacionId, ct);
        return op is null
            ? ServiceResult<OperacionDto>.NoEncontrado("La operación no existe.")
            : ServiceResult<OperacionDto>.Ok(await DtoAsync(op, ct));
    }
    public async Task<ServiceResult<OperacionDto>> RegistrarAsync(JsonElement evento, CancellationToken ct)
    {
        var resultado = await ingesta.IngresarAsync(evento, ct);
        var cuerpo = resultado.Cuerpo;
        if (resultado.StatusCode == 400 || cuerpo.OperacionId is not { } id)
        {
            return ServiceResult<OperacionDto>.Error(400, cuerpo.Codigo ?? IngestaEventoFacturable.Invalido,
                "El evento no es válido.", string.Join(" | ", cuerpo.Errores));
        }
        unidadTrabajo.DescartarCambios();
        var op = (await repositorio.ObtenerAsync(id, ct))!;
        var dto = await DtoAsync(op, ct);
        // 201 si la operación es nueva; si ya existía (actualizada, repetida o versión superada) se devuelve la existente.
        return resultado.StatusCode == 201 ? ServiceResult<OperacionDto>.Created(dto) : ServiceResult<OperacionDto>.Ok(dto);
    }
    public async Task<ServiceResult<OperacionDto>> CompletarAsync(Guid operacionId, CompletarOperacionRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Motivo))
        {
            return ServiceResult<OperacionDto>.Invalido("El motivo es obligatorio.");
        }
        var motivo = r.Motivo.Trim();
        var op = await repositorio.ObtenerAsync(operacionId, ct);
        if (op is null)
        {
            return ServiceResult<OperacionDto>.NoEncontrado("La operación no existe.");
        }
        if (op.EstadoOperacion == EstadoOperacion.ANULADA)
        {
            return ServiceResult<OperacionDto>.Error(409, OperacionAnulada, "La operación está anulada.");
        }
        var ofa = (await repositorio.OfasDeOperacionesAsync([op.Id], ct)).GetValueOrDefault(op.Id);
        if (ofa.OfaId != Guid.Empty && ofa.Estado is not (EstadoOfa.BORRADOR or EstadoOfa.VALIDADA))
        {
            return ServiceResult<OperacionDto>.Error(409, OfaYaEnviada,
                $"La OFA está {ofa.Estado}: ya no se recalcula. Los datos de la operación no se cambian.");
        }
        var ahora = reloj.GetUtcNow();
        var campos = new List<string>();
        var anterior = new Dictionary<string, object?>();
        var nuevo = new Dictionary<string, object?>();
        void Cambio(string campo, object? antes, object? despues)
        {
            campos.Add(campo);
            anterior[campo] = antes;
            nuevo[campo] = despues;
        }
        if (r.TipoRetiro is { } tipoRetiro && tipoRetiro != op.TipoRetiro)
        {
            Cambio("tipoRetiro", op.TipoRetiro, tipoRetiro);
            op.TipoRetiro = tipoRetiro;
        }
        if (r.Sentido is { } sentido && sentido != op.Sentido)
        {
            Cambio("sentido", op.Sentido, sentido);
            op.Sentido = sentido;
        }
        if (r.CantidadDus is { } dus)
        {
            if (dus < 0)
            {
                return ServiceResult<OperacionDto>.Invalido("cantidadDus no puede ser negativa.");
            }
            Cambio("cantidadDus", op.CantidadDus, dus);
            op.CantidadDus = dus;
        }
        if (r.FechaDespacho is not null || r.DiasAforo is not null)
        {
            var destino = string.IsNullOrWhiteSpace(r.Contenedor)
                ? op.Contenedores.ToList()
                : op.Contenedores.Where(c => c.Numero == r.Contenedor.Trim()).ToList();
            if (destino.Count == 0)
            {
                return ServiceResult<OperacionDto>.Invalido(string.IsNullOrWhiteSpace(r.Contenedor)
                    ? "La operación no tiene contenedores."
                    : $"El contenedor {r.Contenedor} no es de la operación.");
            }
            if (r.DiasAforo is < 0)
            {
                return ServiceResult<OperacionDto>.Invalido("diasAforo no puede ser negativo.");
            }
            foreach (var c in destino)
            {
                if (r.FechaDespacho is { } fecha)
                {
                    Cambio($"fechaDespacho:{c.Numero}", c.FechaDespacho, fecha);
                    c.FechaDespacho = fecha;
                }
                if (r.DiasAforo is { } dias)
                {
                    Cambio($"diasAforo:{c.Numero}", c.DiasBajoAforo, dias);
                    c.DiasBajoAforo = dias;
                }
            }
        }
        foreach (var cs in r.CantidadesServicio ?? [])
        {
            var servicios = op.Servicios.Where(s => s.TipoServicioId == cs.TipoServicioId).ToList();
            if (servicios.Count == 0)
            {
                return ServiceResult<OperacionDto>.Invalido($"El servicio {cs.TipoServicioId} no está en la operación.");
            }
            if (cs.Cantidad < 0)
            {
                return ServiceResult<OperacionDto>.Invalido($"La cantidad del servicio {cs.TipoServicioId} no puede ser negativa.");
            }
            Cambio($"cantidad:{cs.TipoServicioId}", servicios[0].Cantidad, cs.Cantidad);
            servicios.ForEach(s => s.Cantidad = cs.Cantidad);
        }
        var rut = await resolutor.RutFacturacionAsync(op, ct);
        var decisiones = new List<object>();
        foreach (var d in r.Decisiones ?? [])
        {
            var error = await DecidirAsync(op, d, rut, motivo, ahora, ct);
            if (error is not null)
            {
                return ServiceResult<OperacionDto>.Invalido(error);
            }
            decisiones.Add(new { tipo = d.Tipo, valor = d.Valor });
        }
        if (campos.Count == 0 && decisiones.Count == 0)
        {
            return ServiceResult<OperacionDto>.Invalido("No hay datos que completar ni decisiones que registrar.");
        }
        op.Facturable = op.TipoCarga != TipoCarga.BULTO && !ResolutorOperacion.DecidioNoFacturar(op);
        op.ActualizadoEn = ahora;
        auditoria.Registrar("operacion", op.Id, AccionCompletar, anterior, new { campos, valores = nuevo, decisiones, motivo });
        var candidatos = await resolutor.ResolverConvenioAsync(op, rut, ct);
        var cierre = op.Facturable ? ResolucionPendiente.DATO_COMPLETADO : ResolucionPendiente.YA_NO_APLICA;
        await resolutor.RecalcularPendientesAsync(op, candidatos, cierre, usuario.UsuarioAuditoria, motivo, ahora, ct);
        await using (var transaccion = await unidadTrabajo.IniciarTransaccionAsync(ct))
        {
            await unidadTrabajo.GuardarAsync(ct);
            // Una OFA en BORRADOR o VALIDADA se recalcula como versión nueva (los ajustes de monto se repiten, DBA-7).
            await ofas.RecalcularPorCambioAsync(op.Id, ct);
            await transaccion.ConfirmarAsync(ct);
        }
        return ServiceResult<OperacionDto>.Ok(await DtoAsync(op, ct));
    }
    /// <summary>Registra la decisión del facturador sobre un pendiente que la exige. Devuelve el error o null.</summary>
    private async Task<string?> DecidirAsync(Operacion op, DecisionPendienteDto d, string? rut, string motivo, DateTimeOffset ahora, CancellationToken ct)
    {
        if (!CalculadorPendientes.ConDecision.Contains(d.Tipo))
        {
            return $"El pendiente {d.Tipo} se resuelve completando el dato, no con una decisión.";
        }
        var pendiente = op.Pendientes.FirstOrDefault(p => p.Tipo == d.Tipo && p.ResueltoEn is null);
        if (pendiente is null)
        {
            return $"La operación no tiene un pendiente {d.Tipo} abierto.";
        }
        var valor = d.Valor?.Trim() ?? string.Empty;
        object decision;
        switch (d.Tipo)
        {
            case TipoPendiente.VARIOS_CONVENIOS_VIGENTES:
                var vigente = rut is null || !Guid.TryParse(valor, out var elegido)
                    ? null
                    : (await resolutor.ResolverCandidatosAsync(rut, op, ct)).FirstOrDefault(v => v.ConvenioId == elegido || v.Id == elegido);
                if (vigente is null)
                {
                    return "El valor debe ser el id de uno de los convenios vigentes del cliente.";
                }
                decision = new { valor = vigente.ConvenioId.ToString(), convenioId = vigente.ConvenioId, versionConvenioId = vigente.Id };
                break;
            case TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES:
                if (valor is not (ValoresDecision.Facturar or ValoresDecision.NoFacturar))
                {
                    return "El valor debe ser FACTURAR o NO_FACTURAR.";
                }
                decision = new { valor };
                break;
            default:
                if (valor is not (ValoresDecision.Continuar or ValoresDecision.NoFacturar))
                {
                    return "El valor debe ser CONTINUAR o NO_FACTURAR.";
                }
                decision = new { valor };
                break;
        }
        pendiente.ResueltoEn = ahora;
        pendiente.ResueltoPor = usuario.UsuarioAuditoria;
        pendiente.Resolucion = ResolucionPendiente.DECISION;
        pendiente.Decision = JsonSatelite.Serializar(decision);
        pendiente.Motivo = motivo;
        return null;
    }
    private static List<TipoPendiente> Abiertos(Operacion o) =>
        o.Pendientes.Where(p => p.ResueltoEn is null).Select(p => p.Tipo).Order().ToList();
    private async Task<OperacionDto> DtoAsync(Operacion op, CancellationToken ct)
    {
        var ids = new[] { op.ConsignatarioId, op.ClienteFacturadorId, op.ClienteId }.Where(i => i is not null).Select(i => i!.Value).Distinct();
        var clientes = await repositorio.ObtenerClientesAsync(ids, ct);
        var cliente = op.ClienteId is { } c ? clientes.GetValueOrDefault(c) : null;
        var consignatario = op.ConsignatarioId is { } ci ? clientes.GetValueOrDefault(ci) : null;
        var facturador = op.ClienteFacturadorId is { } fi ? clientes.GetValueOrDefault(fi) : null;
        var version = op.VersionConvenioId is { } vid ? await repositorio.ObtenerVersionConvenioAsync(vid, ct) : null;
        var tipos = (await catalogo.ListarTiposServicioAsync(ct)).ToDictionary(t => t.TipoServicioId);
        var ofa = (await repositorio.OfasDeOperacionesAsync([op.Id], ct)).TryGetValue(op.Id, out var f) ? f : ((Guid, EstadoOfa)?)null;
        return new OperacionDto(
            op.Id, op.Entidad, op.IdsAexsa, cliente?.Rut, cliente?.RazonSocial, op.ConConvenio, op.Facturable,
            op.TipoCarga, op.Sentido, op.TipoRetiro, op.EstadoOperacion,
            op.TerminalAexsaId is { } tid ? new TerminalAexsaDto(tid, op.TerminalAexsaNombre ?? string.Empty) : null,
            op.TerminalGrupo ?? GrupoTerminal.SIN_MAPEAR,
            Abiertos(op),
            op.Pendientes.OrderBy(p => p.ResueltoEn is not null).ThenBy(p => p.DetectadoEn).Select(p => new PendienteDto(
                p.Id, p.Tipo, p.BloqueaValidacion, CalculadorPendientes.ConDecision.Contains(p.Tipo), JsonSatelite.Objeto(p.Detalle),
                p.DetectadoEn, p.ResueltoEn, p.ResueltoPor, p.Resolucion, JsonSatelite.Objeto(p.Decision), p.Motivo)).ToList(),
            op.Din ?? [], op.CantidadDus, op.Bls.OrderBy(b => b.BlId).Select(b => new BlDocumentoDto(b.BlId, b.Numero)).ToList(),
            op.Papeletas ?? [],
            consignatario is null ? null : new ParteDto(consignatario.Rut, consignatario.RazonSocial),
            facturador is null ? null : new ClienteFacturadorDetalleDto(facturador.Rut, facturador.RazonSocial,
                op.TipoClienteFacturador, op.FacturadorAsignadoPor, op.FacturadorAsignadoEn),
            version is null ? null : new ConvenioOperacionDto(version.Id, version.ConvenioId, version.Numero, version.VigenciaDesde, version.VigenciaHasta),
            op.Contenedores.OrderBy(x => x.Numero).Select(x => new ContenedorOperacionDto(
                x.Numero, x.FechaRecepcion, x.FechaRetiro, x.FechaDespacho, x.DiasAlmacenaje, x.DiasBajoAforo, x.EsReefer,
                CalculadorPendientes.Inspecciones(x))).ToList(),
            op.VersionEvento, op.RutaClasificada, op.MomentoFacturacion, op.VisadoAprobadoEn, op.IngeridoEn, op.ActualizadoEn,
            op.Servicios.OrderBy(s => s.TipoServicioId).Select(s => new ServicioPrestadoDto(
                s.TipoServicioId, tipos.GetValueOrDefault(s.TipoServicioId)?.Descripcion, s.Cantidad,
                Enum.TryParse<UnidadCobro>(s.Unidad, out var u) ? u : null, s.Contenedor, s.FinalizadoEn, s.OrigenDato)).ToList(),
            ofa?.Item1, ofa?.Item2);
    }
}
