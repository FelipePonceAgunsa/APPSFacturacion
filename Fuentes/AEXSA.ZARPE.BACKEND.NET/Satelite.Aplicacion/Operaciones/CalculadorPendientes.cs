using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
namespace Satelite.Aplicacion.Operaciones;
/// <summary>Pendiente que corresponde a la operación según sus datos actuales.</summary>
public sealed record PendienteEsperado(TipoPendiente Tipo, bool Bloquea, object? Detalle);
/// <summary>Datos externos a la operación que necesitan los pendientes.</summary>
/// <param name="GruposInspeccion">Código de inspección de AEXSA → grupo (tipo_inspeccion_mapa).</param>
/// <param name="ConveniosCandidatos">Versiones de convenio en vigor para el RUT y la fecha (más de una = decisión).</param>
/// <param name="OperacionesMismoDocumento">Otras operaciones activas que comparten un bls[].id.</param>
public sealed record EntornoPendientes(
    IReadOnlyDictionary<string, GrupoInspeccion> GruposInspeccion,
    IReadOnlyList<VersionConvenio> ConveniosCandidatos,
    IReadOnlyList<Guid> OperacionesMismoDocumento);
/// <summary>
/// Pendientes de la operación (tarea 3.1a, DBA-8). Se recalculan al ingresar un mensaje y al completar datos:
/// se abren los que faltan y se cierran los que ya no aplican. Un pendiente resuelto no se borra.
/// Bloquean la validación de la OFA: TIPO_RETIRO, TERMINAL_SIN_MAPEAR (en directo sin convenio), CANTIDAD_DUS,
/// CANTIDAD_SERVICIO, DOCUMENTO_CON_VARIAS_SOLICITUDES e INSPECCION_NO_FINALIZADA. VARIOS_CONVENIOS_VIGENTES,
/// DOCUMENTO_CON_VARIAS_SOLICITUDES e INSPECCION_RECHAZADA exigen una decisión registrada; una vez decididos no se reabren.
/// </summary>
public static class CalculadorPendientes
{
    public const string UsuarioSistema = "sistema";
    public static readonly IReadOnlySet<TipoPendiente> ConDecision = new HashSet<TipoPendiente>
    {
        TipoPendiente.VARIOS_CONVENIOS_VIGENTES,
        TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES,
        TipoPendiente.INSPECCION_RECHAZADA,
    };
    /// <summary>Inspecciones guardadas en el jsonb del contenedor.</summary>
    public static List<InspeccionDto> Inspecciones(ContenedorOperacion c) => JsonSatelite.Leer<List<InspeccionDto>>(c.Inspecciones) ?? [];
    public static List<PendienteEsperado> Evaluar(Operacion op, EntornoPendientes entorno)
    {
        var lista = new List<PendienteEsperado>();
        // Una operación anulada, fuera de alcance (BULTO) o que el facturador decidió no facturar no tiene pendientes.
        if (op.EstadoOperacion == EstadoOperacion.ANULADA || op.TipoCarga == TipoCarga.BULTO || !op.Facturable)
        {
            return lista;
        }
        if (op.TipoRetiro is null)
        {
            lista.Add(new(TipoPendiente.TIPO_RETIRO, true, null));
        }
        if (op.Sentido is null)
        {
            lista.Add(new(TipoPendiente.SENTIDO, false, null));
        }
        if (op.TerminalGrupo is null or GrupoTerminal.SIN_MAPEAR)
        {
            // Solo decide algo en el directo sin convenio (Regla 0): ahí bloquea.
            var bloquea = op.TipoRetiro == TipoRetiro.DIRECTO && !op.ConConvenio;
            lista.Add(new(TipoPendiente.TERMINAL_SIN_MAPEAR, bloquea, new { terminalId = op.TerminalAexsaId, nombre = op.TerminalAexsaNombre }));
        }
        if (op.Sentido == Sentido.EXPORTACION && op.CantidadDus is null)
        {
            lista.Add(new(TipoPendiente.CANTIDAD_DUS, true, null));
        }
        var sinCantidad = op.Servicios.Where(s => s.Cantidad is null).Select(s => s.TipoServicioId).Distinct().ToList();
        if (sinCantidad.Count > 0)
        {
            lista.Add(new(TipoPendiente.CANTIDAD_SERVICIO, true, new { servicios = sinCantidad }));
        }
        var sinDiasAforo = new List<string>();
        var noFinalizadas = new List<object>();
        var rechazadas = new List<object>();
        foreach (var c in op.Contenedores.OrderBy(c => c.Numero))
        {
            foreach (var i in Inspecciones(c))
            {
                var grupo = entorno.GruposInspeccion.GetValueOrDefault(i.Tipo, GrupoInspeccion.SIN_MAPEAR);
                if (c.DiasBajoAforo is null
                    && (grupo == GrupoInspeccion.SIN_MAPEAR || (grupo == GrupoInspeccion.AFORO_ADUANA && i.FinalizadaEn is null)))
                {
                    sinDiasAforo.Add(c.Numero);
                }
                if (i.FinalizadaEn is null || i.Resultado is null)
                {
                    noFinalizadas.Add(new { contenedor = c.Numero, tipo = i.Tipo });
                }
                if (i.Resultado is ResultadoInspeccion.RECHAZADA_PARCIAL or ResultadoInspeccion.RECHAZADA_TOTAL)
                {
                    rechazadas.Add(new { contenedor = c.Numero, tipo = i.Tipo, resultado = i.Resultado });
                }
            }
        }
        if (sinDiasAforo.Count > 0)
        {
            lista.Add(new(TipoPendiente.DIAS_AFORO, false, new { contenedores = sinDiasAforo.Distinct().ToList() }));
        }
        if (op.ConConvenio)
        {
            // Con convenio se factura tras el despacho (RN-046); mientras no exista la fecha de despacho se usa la de retiro.
            var sinDespacho = op.Contenedores.Where(c => c.FechaDespacho is null && c.FechaRetiro is null).Select(c => c.Numero).ToList();
            if (sinDespacho.Count > 0)
            {
                lista.Add(new(TipoPendiente.FECHA_DESPACHO, false, new { contenedores = sinDespacho }));
            }
        }
        if (entorno.ConveniosCandidatos.Count > 1)
        {
            lista.Add(new(TipoPendiente.VARIOS_CONVENIOS_VIGENTES, true, new
            {
                elegida = op.VersionConvenioId,
                candidatos = entorno.ConveniosCandidatos
                    .OrderBy(v => v.Convenio?.CreadoEn)
                    .Select(v => new { convenioId = v.ConvenioId, versionConvenioId = v.Id, numero = v.Numero, creadoEn = v.Convenio?.CreadoEn })
                    .ToList(),
            }));
        }
        if (entorno.OperacionesMismoDocumento.Count > 0)
        {
            lista.Add(new(TipoPendiente.DOCUMENTO_CON_VARIAS_SOLICITUDES, true, new
            {
                operaciones = entorno.OperacionesMismoDocumento,
                bls = op.Bls.Select(b => b.BlId).ToList(),
            }));
        }
        if (noFinalizadas.Count > 0)
        {
            lista.Add(new(TipoPendiente.INSPECCION_NO_FINALIZADA, true, new { inspecciones = noFinalizadas }));
        }
        if (rechazadas.Count > 0)
        {
            lista.Add(new(TipoPendiente.INSPECCION_RECHAZADA, true, new { inspecciones = rechazadas }));
        }
        return lista;
    }
    /// <summary>
    /// Abre los pendientes esperados que no están abiertos y cierra los abiertos que ya no corresponden.
    /// Un pendiente con decisión ya tomada no se reabre.
    /// </summary>
    /// <param name="resolucionCierre">YA_NO_APLICA en la ingesta; DATO_COMPLETADO cuando lo cierra un PATCH.</param>
    public static void Sincronizar(
        Operacion op, IReadOnlyList<PendienteEsperado> esperados, DateTimeOffset ahora, string usuario,
        ResolucionPendiente resolucionCierre, string? motivo, Action<OperacionPendiente> agregar)
    {
        var abiertos = op.Pendientes.Where(p => p.ResueltoEn is null).ToList();
        foreach (var e in esperados)
        {
            var detalle = e.Detalle is null ? null : JsonSatelite.Serializar(e.Detalle);
            var abierto = abiertos.FirstOrDefault(p => p.Tipo == e.Tipo);
            if (abierto is not null)
            {
                abierto.BloqueaValidacion = e.Bloquea;
                abierto.Detalle = detalle;
                continue;
            }
            if (ConDecision.Contains(e.Tipo) && op.Pendientes.Any(p => p.Tipo == e.Tipo && p.Resolucion == ResolucionPendiente.DECISION))
            {
                continue;
            }
            var nuevo = new OperacionPendiente
            {
                Id = Guid.CreateVersion7(),
                OperacionId = op.Id,
                Tipo = e.Tipo,
                BloqueaValidacion = e.Bloquea,
                Detalle = detalle,
                DetectadoEn = ahora,
            };
            op.Pendientes.Add(nuevo);
            agregar(nuevo);
        }
        var tipos = esperados.Select(e => e.Tipo).ToHashSet();
        foreach (var p in abiertos.Where(p => !tipos.Contains(p.Tipo)))
        {
            p.ResueltoEn = ahora;
            p.ResueltoPor = usuario;
            p.Resolucion = resolucionCierre;
            p.Motivo = motivo;
        }
    }
    /// <summary>Ruta {carga}×{retiro}×{convenio} (REQ-002), solo si el tipo de retiro viene informado; y momento de facturación (RN-046).</summary>
    public static void Clasificar(Operacion op)
    {
        op.RutaClasificada = op.TipoRetiro is null || op.TipoCarga is null
            ? null
            : $"{op.TipoCarga}-{op.TipoRetiro}-{(op.ConConvenio ? "CON" : "SIN")}_CONVENIO";
        op.MomentoFacturacion = op.ConConvenio ? MomentoFacturacion.POST_DESPACHO : MomentoFacturacion.EN_VISACION;
    }
}
