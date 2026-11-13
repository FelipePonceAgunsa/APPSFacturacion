using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Comun;
using Satelite.Aplicacion.Ofa;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Dominio.Motor;
namespace Satelite.Aplicacion.Operaciones;
/// <summary>Entrada del evento facturable de AEXSA (POST /aexsa/eventos y alta manual POST /operaciones).</summary>
public interface IIngestaEventoFacturable
{
    /// <summary>Aplica el algoritmo de modelo-datos.md §4b. Recibe el JSON tal como llegó para guardarlo aunque sea inválido.</summary>
    Task<ResultadoIngestaHttp> IngresarAsync(JsonElement carga, CancellationToken ct);
}
/// <summary>
/// Ingesta del contrato v3.0 (contratos-integracion.md §2, tarea 3.1a). Idempotente por entidad + operacionIdAexsa + version:
/// operación nueva → 201; versión mayor → 200; misma versión → 409 YA_RECIBIDO; versión menor → 409 VERSION_SUPERADA;
/// inválido → 400 INVALIDO; VISADO_PRESENCIAL → 400 ENTIDAD_NO_HABILITADA. Cada mensaje queda en evento_aexsa.
/// </summary>
public sealed class IngestaEventoFacturable(
    IOperacionesRepositorio repositorio,
    ICatalogoRepositorio catalogo,
    ResolutorOperacion resolutor,
    IOfaService ofas,
    IUnidadTrabajo unidadTrabajo,
    IAuditoria auditoria,
    TimeProvider reloj,
    ILogger<IngestaEventoFacturable> logger) : IIngestaEventoFacturable
{
    public const string YaRecibido = "YA_RECIBIDO";
    public const string VersionSuperada = "VERSION_SUPERADA";
    public const string Invalido = "INVALIDO";
    public const string EntidadNoHabilitada = "ENTIDAD_NO_HABILITADA";
    public const string AccionConflicto = "CONFLICTO_DATO_MANUAL";
    public async Task<ResultadoIngestaHttp> IngresarAsync(JsonElement carga, CancellationToken ct)
    {
        var crudo = carga.GetRawText();
        var cabecera = Cabecera.Leer(carga);
        if (cabecera.Entidad == EntidadAexsa.VISADO_PRESENCIAL)
        {
            return await RechazarAsync(cabecera, crudo, EntidadNoHabilitada,
                ["VISADO_PRESENCIAL no está habilitado en la etapa 1: falta definir su identidad (contratos §2 nota 12)."], ct);
        }
        EventoFacturableDto? evento = null;
        var errores = new List<string>();
        try
        {
            evento = carga.Deserialize<EventoFacturableDto>(JsonSatelite.Estricto);
        }
        catch (JsonException ex)
        {
            errores.Add(ex.Message);
        }
        if (evento is null && errores.Count == 0)
        {
            errores.Add("El mensaje está vacío.");
        }
        if (evento is not null)
        {
            errores.AddRange(await ValidarAsync(evento, ct));
        }
        if (errores.Count > 0 || evento is null)
        {
            return await RechazarAsync(cabecera, crudo, Invalido, errores, ct);
        }
        try
        {
            return await AplicarAsync(evento, crudo, ct);
        }
        catch (ReglaDatosException ex) when (ex.Codigo == ReglasDatos.OperacionYaExiste)
        {
            // Dos mensajes nuevos simultáneos de la misma operación: el segundo reintenta por la vía de la existente (§4b).
            unidadTrabajo.DescartarCambios();
            return await AplicarAsync(evento, crudo, ct);
        }
    }
    private async Task<List<string>> ValidarAsync(EventoFacturableDto e, CancellationToken ct)
    {
        var errores = new List<string>();
        if (e.Version < 1)
        {
            errores.Add("version debe ser un entero mayor o igual a 1.");
        }
        if (e.Entidad == EntidadAexsa.VISADO_VIRTUAL && e.OperacionIdAexsa.Length != 1)
        {
            errores.Add($"VISADO_VIRTUAL debe traer exactamente un ID en operacionIdAexsa (trae {e.OperacionIdAexsa.Length}).");
        }
        if (e.OperacionIdAexsa.Any(id => id <= 0))
        {
            errores.Add("operacionIdAexsa solo admite IDs positivos.");
        }
        if (e.Bls.Length == 0)
        {
            errores.Add("bls debe traer al menos un documento de carga.");
        }
        foreach (var bl in e.Bls)
        {
            if (string.IsNullOrWhiteSpace(bl.Numero) || bl.Numero.Length > 40)
            {
                errores.Add($"bls[id={bl.Id}].numero es obligatorio y tiene a lo más 40 caracteres.");
            }
        }
        if (e.Bls.GroupBy(b => b.Id).Any(g => g.Count() > 1))
        {
            errores.Add("bls trae el mismo id más de una vez.");
        }
        if (e.Sentido == Sentido.IMPORTACION && (e.Din is null || e.Din.Length == 0))
        {
            errores.Add("din es obligatorio en importación.");
        }
        if (Rut.Normalizar(e.Consignatario.Rut) is null)
        {
            errores.Add($"consignatario.rut inválido: {e.Consignatario.Rut}");
        }
        if (e.ClienteFacturador is { } f && Rut.Normalizar(f.Rut) is null)
        {
            errores.Add($"clienteFacturador.rut inválido: {f.Rut}");
        }
        if (e.Terminal is { } t && string.IsNullOrWhiteSpace(t.Nombre))
        {
            errores.Add("terminal.nombre es obligatorio cuando viene el terminal.");
        }
        foreach (var c in e.Contenedores ?? [])
        {
            if (string.IsNullOrWhiteSpace(c.Numero) || c.Numero.Length > 15)
            {
                errores.Add($"contenedores[].numero es obligatorio y tiene a lo más 15 caracteres ({c.Numero}).");
            }
            if (c.DiasAlmacenaje is < 0)
            {
                errores.Add($"contenedor {c.Numero}: diasAlmacenaje no puede ser negativo.");
            }
            foreach (var i in c.Inspecciones ?? [])
            {
                if (string.IsNullOrWhiteSpace(i.Tipo) || i.Tipo.Length > 40)
                {
                    errores.Add($"contenedor {c.Numero}: inspecciones[].tipo es obligatorio y tiene a lo más 40 caracteres.");
                }
            }
        }
        if ((e.Contenedores ?? []).GroupBy(c => c.Numero).Any(g => g.Count() > 1))
        {
            errores.Add("contenedores trae el mismo número más de una vez.");
        }
        var tipos = (await catalogo.ListarTiposServicioAsync(ct)).Select(t => t.TipoServicioId).ToHashSet();
        foreach (var s in e.Servicios)
        {
            if (!tipos.Contains(s.TipoServicioId))
            {
                errores.Add($"servicios: tipoServicioId {s.TipoServicioId} no existe en el catálogo de servicios.");
            }
            if (s.Cantidad is < 0)
            {
                errores.Add($"servicios: la cantidad del servicio {s.TipoServicioId} no puede ser negativa.");
            }
            if (s.Contenedor is { Length: > 15 })
            {
                errores.Add($"servicios: contenedor {s.Contenedor} tiene más de 15 caracteres.");
            }
        }
        return errores;
    }
    private async Task<ResultadoIngestaHttp> RechazarAsync(Cabecera cabecera, string crudo, string codigo, List<string> errores, CancellationToken ct)
    {
        if (cabecera.TipoEvento is { } tipoEvento)
        {
            repositorio.AgregarEvento(new EventoAexsa
            {
                Id = Guid.CreateVersion7(),
                Entidad = cabecera.Entidad,
                OperacionIdAexsa = cabecera.Clave,
                Version = cabecera.Version ?? 0,
                TipoEvento = tipoEvento,
                RecibidoEn = reloj.GetUtcNow(),
                Carga = crudo,
                Resultado = ResultadoEvento.RECHAZADO,
                DetalleError = string.Join(" | ", errores),
                CodigoError = codigo,
            });
            await unidadTrabajo.GuardarAsync(ct);
        }
        else
        {
            // El CHECK de evento_aexsa.tipo_evento no admite un valor inválido o ausente: el mensaje solo queda en el log.
            logger.LogWarning("Mensaje de AEXSA rechazado sin tipoEvento válido ({Codigo}): {Errores}. Carga: {Carga}",
                codigo, string.Join(" | ", errores), crudo);
        }
        return new ResultadoIngestaHttp(400, new ResultadoIngestaDto(ResultadoEvento.RECHAZADO, codigo, null, null, errores));
    }
    private async Task<ResultadoIngestaHttp> AplicarAsync(EventoFacturableDto evento, string crudo, CancellationToken ct)
    {
        var clave = evento.OperacionIdAexsa[0].ToString(CultureInfo.InvariantCulture);
        var ahora = reloj.GetUtcNow();
        await using var transaccion = await unidadTrabajo.IniciarTransaccionAsync(ct);
        await repositorio.BloquearAsync(evento.Entidad, clave, ct);
        var op = await repositorio.ObtenerPorClaveAsync(evento.Entidad, clave, ct);
        var nueva = op is null;
        if (op is not null && evento.Version <= op.VersionEvento)
        {
            var duplicado = evento.Version == op.VersionEvento;
            var codigo = duplicado ? YaRecibido : VersionSuperada;
            repositorio.AgregarEvento(Evento(evento, clave, crudo, ahora,
                duplicado ? ResultadoEvento.DUPLICADO : ResultadoEvento.RECHAZADO, codigo,
                duplicado ? null : $"Versión {evento.Version} menor que la aplicada ({op.VersionEvento})."));
            await unidadTrabajo.GuardarAsync(ct);
            await transaccion.ConfirmarAsync(ct);
            return new ResultadoIngestaHttp(409, new ResultadoIngestaDto(
                duplicado ? ResultadoEvento.DUPLICADO : ResultadoEvento.RECHAZADO, codigo, op.Id, op.VersionEvento, []));
        }
        if (op is null)
        {
            op = new Operacion { Id = Guid.CreateVersion7(), Entidad = evento.Entidad, OperacionIdAexsa = clave, IngeridoEn = ahora };
            repositorio.AgregarOperacion(op);
        }
        var rut = await AplicarMensajeAsync(op, evento, nueva, ahora, ct);
        repositorio.AgregarEvento(Evento(evento, clave, crudo, ahora, ResultadoEvento.APLICADO, null, null));
        await unidadTrabajo.GuardarAsync(ct);
        // Con los BLs ya guardados: pendientes propios y de las operaciones que comparten documento.
        var candidatos = await resolutor.ResolverConvenioAsync(op, rut, ct);
        await resolutor.RecalcularPendientesAsync(op, candidatos, ResolucionPendiente.YA_NO_APLICA, CalculadorPendientes.UsuarioSistema, null, ahora, ct);
        await resolutor.ActualizarOperacionesDelMismoDocumentoAsync(op, ahora, ct);
        await unidadTrabajo.GuardarAsync(ct);
        if (!nueva)
        {
            // Una OFA aún no enviada se recalcula con los datos nuevos (versión nueva de cálculo, DBA-7).
            await ofas.RecalcularPorCambioAsync(op.Id, ct);
        }
        await transaccion.ConfirmarAsync(ct);
        return new ResultadoIngestaHttp(nueva ? 201 : 200,
            new ResultadoIngestaDto(ResultadoEvento.APLICADO, null, op.Id, op.VersionEvento, []));
    }
    /// <summary>Aplica el estado completo del mensaje. Devuelve el RUT con que se busca el convenio.</summary>
    private async Task<string> AplicarMensajeAsync(Operacion op, EventoFacturableDto e, bool nueva, DateTimeOffset ahora, CancellationToken ct)
    {
        HashSet<string> manuales = nueva ? [] : CamposCompletadosAMano(await repositorio.AuditoriasCompletarDatosAsync(op.Id, ct));
        // Un dato completado a mano no se pisa con vacío; un valor no vacío de AEXSA sí lo pisa y queda el conflicto en la auditoría.
        T Campo<T>(string campo, T actual, T deAexsa)
        {
            if (!manuales.Contains(campo))
            {
                return deAexsa;
            }
            if (deAexsa is null)
            {
                return actual;
            }
            if (!Equals(actual, deAexsa))
            {
                auditoria.Registrar("operacion", op.Id, AccionConflicto,
                    new { campo, valor = actual, origen = "MANUAL" }, new { campo, valor = deAexsa, origen = "AEXSA", version = e.Version });
            }
            return deAexsa;
        }
        op.IdsAexsa = e.OperacionIdAexsa;
        op.TipoCarga = e.TipoCarga;
        op.Sentido = Campo("sentido", op.Sentido, e.Sentido);
        op.TipoRetiro = Campo("tipoRetiro", op.TipoRetiro, e.TipoRetiro);
        op.Din = e.Din;
        op.Papeletas = e.Papeletas;
        op.EstadoOperacion = e.EstadoOperacion;
        op.VisadoAprobadoEn = e.VisadoAprobadoEn;
        op.VersionEvento = e.Version;
        op.ActualizadoEn = ahora;
        // Terminal: un id nuevo se agrega a terminal_mapa como SIN_MAPEAR; sin terminal, SIN_MAPEAR (RN-079).
        if (e.Terminal is { } t)
        {
            var mapa = await repositorio.ObtenerTerminalAsync(t.Id, ct);
            if (mapa is null)
            {
                mapa = new TerminalMapa { TerminalAexsaId = t.Id, Nombre = t.Nombre.Trim(), Grupo = GrupoTerminal.SIN_MAPEAR, ActualizadoEn = ahora };
                repositorio.AgregarTerminal(mapa);
            }
            op.TerminalAexsaId = t.Id;
            op.TerminalAexsaNombre = t.Nombre.Trim();
            op.TerminalGrupo = mapa.Grupo;
        }
        else
        {
            op.TerminalAexsaId = null;
            op.TerminalAexsaNombre = null;
            op.TerminalGrupo = GrupoTerminal.SIN_MAPEAR;
        }
        // Partes: clientes por RUT; se factura al facturador o, si no viene, al consignatario (RN-077).
        var clientes = new Dictionary<string, Cliente>();
        var consignatario = await ClienteAsync(e.Consignatario.Rut, e.Consignatario.RazonSocial, clientes, ahora, ct);
        op.ConsignatarioId = consignatario.Id;
        Cliente? facturador = null;
        if (e.ClienteFacturador is { } f)
        {
            facturador = await ClienteAsync(f.Rut, f.RazonSocial, clientes, ahora, ct);
            op.ClienteFacturadorId = facturador.Id;
            op.TipoClienteFacturador = f.Tipo;
            op.FacturadorAsignadoPor = f.AsignadoPor is { Length: > 80 } largo ? largo[..80] : f.AsignadoPor;
            op.FacturadorAsignadoEn = f.AsignadoEn;
        }
        else
        {
            op.ClienteFacturadorId = null;
            op.TipoClienteFacturador = null;
            op.FacturadorAsignadoPor = null;
            op.FacturadorAsignadoEn = null;
        }
        op.ClienteId = (facturador ?? consignatario).Id;
        // BULTO es de la 2ª etapa: se guarda marcada como fuera de alcance.
        op.Facturable = e.TipoCarga != TipoCarga.BULTO && !ResolutorOperacion.DecidioNoFacturar(op);
        // Documentos de carga (bls[].id identifica el documento, nota 13).
        foreach (var bl in op.Bls.Where(b => e.Bls.All(n => n.Id != b.BlId)).ToList())
        {
            op.Bls.Remove(bl);
            repositorio.Eliminar(bl);
        }
        foreach (var bl in e.Bls)
        {
            if (op.Bls.FirstOrDefault(b => b.BlId == bl.Id) is { } existente)
            {
                existente.Numero = bl.Numero.Trim();
                continue;
            }
            var nuevo = new OperacionBl { OperacionId = op.Id, BlId = bl.Id, Numero = bl.Numero.Trim() };
            op.Bls.Add(nuevo);
            repositorio.AgregarBl(nuevo);
        }
        // Contenedores e inspecciones. Un código de inspección nuevo se agrega como SIN_MAPEAR ([E1-7]).
        var contenedores = e.Contenedores ?? [];
        var codigos = contenedores.SelectMany(c => c.Inspecciones ?? []).Select(i => i.Tipo.Trim()).Distinct().ToList();
        var tiposInspeccion = await repositorio.ObtenerTiposInspeccionAsync(codigos, ct);
        foreach (var codigo in codigos.Where(c => !tiposInspeccion.ContainsKey(c)))
        {
            var tipo = new TipoInspeccionMapa { CodigoAexsa = codigo, Grupo = GrupoInspeccion.SIN_MAPEAR, ActualizadoEn = ahora };
            repositorio.AgregarTipoInspeccion(tipo);
            tiposInspeccion[codigo] = tipo;
        }
        foreach (var c in op.Contenedores.Where(c => contenedores.All(n => n.Numero.Trim() != c.Numero)).ToList())
        {
            op.Contenedores.Remove(c);
            repositorio.Eliminar(c);
        }
        foreach (var ce in contenedores)
        {
            var numero = ce.Numero.Trim();
            var c = op.Contenedores.FirstOrDefault(x => x.Numero == numero);
            if (c is null)
            {
                c = new ContenedorOperacion { Id = Guid.CreateVersion7(), OperacionId = op.Id, Numero = numero };
                op.Contenedores.Add(c);
                repositorio.AgregarContenedor(c);
            }
            var inspecciones = (ce.Inspecciones ?? []).Select(i => i with { Tipo = i.Tipo.Trim() }).ToList();
            c.FechaRecepcion = ce.FechaRecepcion;
            c.FechaRetiro = ce.FechaRetiro;
            c.FechaDespacho = Campo($"fechaDespacho:{numero}", c.FechaDespacho, ce.FechaDespacho);
            c.DiasAlmacenaje = ce.DiasAlmacenaje;
            c.EsReefer = ce.EsReefer;
            c.Inspecciones = JsonSatelite.Serializar(inspecciones);
            c.DiasBajoAforo = Campo($"diasAforo:{numero}", c.DiasBajoAforo,
                DiasAforo(inspecciones, tiposInspeccion.ToDictionary(t => t.Key, t => t.Value.Grupo)));
        }
        // Servicios: el mensaje trae la lista completa. La unidad sale del catálogo, no del texto de AEXSA (nota 8).
        var unidades = (await catalogo.ListarTiposServicioAsync(ct)).ToDictionary(t => t.TipoServicioId, t => t.UnidadNormalizada);
        var anteriores = op.Servicios.ToList();
        foreach (var s in anteriores)
        {
            op.Servicios.Remove(s);
            repositorio.Eliminar(s);
        }
        foreach (var s in e.Servicios)
        {
            var previa = anteriores.FirstOrDefault(a => a.TipoServicioId == s.TipoServicioId && a.Contenedor == s.Contenedor);
            var servicio = new ServicioPrestado
            {
                Id = Guid.CreateVersion7(),
                OperacionId = op.Id,
                TipoServicioId = s.TipoServicioId,
                Cantidad = Campo($"cantidad:{s.TipoServicioId}", previa?.Cantidad, s.Cantidad),
                Unidad = unidades[s.TipoServicioId].ToString(),
                Contenedor = string.IsNullOrWhiteSpace(s.Contenedor) ? null : s.Contenedor.Trim(),
                FinalizadoEn = s.FinalizadoEn,
                OrigenDato = s.Origen,
            };
            op.Servicios.Add(servicio);
            repositorio.AgregarServicio(servicio);
        }
        return (facturador ?? consignatario).Rut;
    }
    /// <summary>
    /// Días bajo aforo de un contenedor desde sus inspecciones AFORO_ADUANA (RN-052, <see cref="DiasBajoAforo"/>).
    /// null si no hay aforo o si alguno no ha terminado (queda el pendiente DIAS_AFORO).
    /// </summary>
    private static int? DiasAforo(IReadOnlyList<InspeccionDto> inspecciones, IReadOnlyDictionary<string, GrupoInspeccion> grupos)
    {
        var aforos = inspecciones.Where(i => grupos.GetValueOrDefault(i.Tipo) == GrupoInspeccion.AFORO_ADUANA).ToList();
        if (aforos.Count == 0 || aforos.Any(i => i.AsignadaEn is null))
        {
            return null;
        }
        var dias = aforos.Select(i => DiasBajoAforo.Calcular(i.AsignadaEn!.Value, i.FinalizadaEn)).ToList();
        return dias.Any(d => d is null) ? null : dias.Sum(d => d!.Value);
    }
    private async Task<Cliente> ClienteAsync(string rut, string? razonSocial, Dictionary<string, Cliente> cache, DateTimeOffset ahora, CancellationToken ct)
    {
        var normalizado = Rut.Normalizar(rut)!;
        if (cache.TryGetValue(normalizado, out var enCache))
        {
            return enCache;
        }
        var cliente = await repositorio.ObtenerClientePorRutAsync(normalizado, ct);
        var razon = string.IsNullOrWhiteSpace(razonSocial) ? null : razonSocial.Trim();
        if (cliente is null)
        {
            cliente = new Cliente { Id = Guid.CreateVersion7(), Rut = normalizado, RazonSocial = razon ?? normalizado, CreadoEn = ahora };
            repositorio.AgregarCliente(cliente);
        }
        else if (razon is not null && cliente.RazonSocial == cliente.Rut)
        {
            // Un cliente creado sin razón social (se usó el RUT) la toma del primer mensaje que la trae.
            cliente.RazonSocial = razon;
        }
        cache[normalizado] = cliente;
        return cliente;
    }
    private static EventoAexsa Evento(EventoFacturableDto e, string clave, string crudo, DateTimeOffset ahora, ResultadoEvento resultado, string? codigo, string? detalle) => new()
    {
        Id = Guid.CreateVersion7(),
        Entidad = e.Entidad,
        OperacionIdAexsa = clave,
        Version = e.Version,
        TipoEvento = e.TipoEvento,
        RecibidoEn = ahora,
        Carga = crudo,
        Resultado = resultado,
        CodigoError = codigo,
        DetalleError = detalle,
    };
    /// <summary>Campos que el facturador completó a mano (auditorías COMPLETAR_DATOS, propiedad <c>campos</c>).</summary>
    private static HashSet<string> CamposCompletadosAMano(IEnumerable<string> auditorias)
    {
        var campos = new HashSet<string>();
        foreach (var json in auditorias)
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("campos", out var lista) && lista.ValueKind == JsonValueKind.Array)
            {
                campos.UnionWith(lista.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.String).Select(c => c.GetString()!));
            }
        }
        return campos;
    }
    /// <summary>Datos de control leídos sin validar, para registrar un mensaje rechazado tal como llegó.</summary>
    private sealed record Cabecera(EntidadAexsa? Entidad, string Clave, int? Version, TipoEvento? TipoEvento)
    {
        public static Cabecera Leer(JsonElement carga)
        {
            if (carga.ValueKind != JsonValueKind.Object)
            {
                return new Cabecera(null, string.Empty, null, null);
            }
            var entidad = Texto(carga, "entidad") is { } e && Enum.TryParse<EntidadAexsa>(e, out var en) && Enum.IsDefined(en) ? en : (EntidadAexsa?)null;
            var tipo = Texto(carga, "tipoEvento") is { } t && Enum.TryParse<TipoEvento>(t, out var te) && Enum.IsDefined(te) ? te : (TipoEvento?)null;
            int? version = carga.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;
            var clave = string.Empty;
            if (carga.TryGetProperty("operacionIdAexsa", out var ids) && ids.ValueKind == JsonValueKind.Array)
            {
                clave = string.Join(",", ids.EnumerateArray().Select(i => i.GetRawText()));
            }
            return new Cabecera(entidad, clave.Length > 40 ? clave[..40] : clave, version, tipo);
        }
        private static string? Texto(JsonElement e, string propiedad) =>
            e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}
