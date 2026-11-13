using System.Globalization;
using Microsoft.Extensions.Options;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Comun;
using Satelite.Aplicacion.Operaciones;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Dominio.Motor;
using Satelite.Dominio.Motor.Reglas;
using OfaEntidad = Satelite.Dominio.Entidades.Ofa;
namespace Satelite.Aplicacion.Ofa;
public interface IOfaService
{
    /// <summary>POST /operaciones/{id}/ofa: calcula la OFA; si ya existe en BORRADOR o VALIDADA, la recalcula como versión nueva.</summary>
    Task<ServiceResult<OfaDto>> CalcularAsync(Guid operacionId, CancellationToken ct);
    Task<ServiceResult<OfaDto>> ObtenerAsync(Guid ofaId, CancellationToken ct);
    Task<ServiceResult<LineaOfaDto>> AgregarLineaAsync(Guid ofaId, AgregarLineaRequest request, CancellationToken ct);
    Task<ServiceResult<LineaOfaDto>> AjustarLineaAsync(Guid ofaId, Guid lineaId, AjusteManualRequest request, CancellationToken ct);
    Task<ServiceResult<ResultadoValidacionDto>> ValidarAsync(Guid ofaId, CancellationToken ct);
    Task<ServiceResult<EnvioOfaDto>> EnviarAsync(Guid ofaId, CancellationToken ct);
    /// <summary>
    /// Tras un cambio en la operación (PATCH o versión nueva de AEXSA): recalcula la OFA si está en BORRADOR o VALIDADA.
    /// Si no se puede recalcular (p. ej. terminal sin mapear), una OFA VALIDADA vuelve a BORRADOR. Guarda los cambios.
    /// </summary>
    Task RecalcularPorCambioAsync(Guid operacionId, CancellationToken ct);
}
/// <summary>
/// Armado, recálculo, ítem manual, ajuste de monto y validación de la OFA (tareas 2.2 y 2.3). El monto lo calcula el motor
/// determinístico o lo fija una persona con motivo; nunca la IA. La memoria de cálculo es append-only.
/// </summary>
public sealed class OfaService(
    IOfaRepositorio repositorio,
    IOperacionesRepositorio operaciones,
    ICatalogoRepositorio catalogo,
    ConstructorContexto constructor,
    ResolutorOperacion resolutor,
    IUnidadTrabajo unidadTrabajo,
    IAuditoria auditoria,
    ICurrentUser usuario,
    TimeProvider reloj,
    IOptions<OfaOptions> opciones) : IOfaService
{
    public const string OfaYaEnviada = "OFA_YA_ENVIADA";
    public const string OperacionAnulada = "OPERACION_ANULADA";
    public const string OperacionFueraDeAlcance = "OPERACION_FUERA_DE_ALCANCE";
    public const string OperacionNoFacturable = "OPERACION_NO_FACTURABLE";
    public const string EnvioSapDeshabilitado = "ENVIO_SAP_DESHABILITADO";
    private static bool Editable(OfaEntidad ofa) => ofa.Estado is EstadoOfa.BORRADOR or EstadoOfa.VALIDADA;
    public async Task<ServiceResult<OfaDto>> CalcularAsync(Guid operacionId, CancellationToken ct)
    {
        var op = await operaciones.ObtenerAsync(operacionId, ct);
        if (op is null)
        {
            return ServiceResult<OfaDto>.NoEncontrado("La operación no existe.");
        }
        var ofa = await repositorio.ObtenerPorOperacionAsync(operacionId, ct);
        if (ofa is not null && !Editable(ofa))
        {
            return ServiceResult<OfaDto>.Error(409, OfaYaEnviada, $"La OFA está {ofa.Estado}: ya no se recalcula.");
        }
        if (op.EstadoOperacion == EstadoOperacion.ACTIVA)
        {
            // Antes de calcular se toman el convenio y el mapeo de terminal vigentes: un convenio cargado o un terminal
            // mapeado después del ingreso se aplican a la operación aún no validada (tarea 3.1a).
            var candidatos = await resolutor.ResolverConvenioAsync(op, await resolutor.RutFacturacionAsync(op, ct), ct);
            await resolutor.RecalcularPendientesAsync(op, candidatos, ResolucionPendiente.YA_NO_APLICA,
                CalculadorPendientes.UsuarioSistema, null, reloj.GetUtcNow(), ct);
        }
        var calculo = await CalcularInternoAsync(op, ofa, ct);
        if (!calculo.Success)
        {
            return ServiceResult<OfaDto>.Error(calculo.StatusCode, calculo.Codigo!, calculo.Message!);
        }
        await unidadTrabajo.GuardarAsync(ct);
        return ServiceResult<OfaDto>.Ok(await DtoAsync(calculo.Data!, op, ct));
    }
    public async Task RecalcularPorCambioAsync(Guid operacionId, CancellationToken ct)
    {
        var ofa = await repositorio.ObtenerPorOperacionAsync(operacionId, ct);
        if (ofa is null || !Editable(ofa))
        {
            return;
        }
        var op = (await operaciones.ObtenerAsync(operacionId, ct))!;
        var calculo = await CalcularInternoAsync(op, ofa, ct);
        if (!calculo.Success && ofa.Estado == EstadoOfa.VALIDADA)
        {
            ofa.Estado = EstadoOfa.BORRADOR;
            auditoria.Registrar("ofa", ofa.Id, "VOLVER_A_BORRADOR", new { Estado = EstadoOfa.VALIDADA },
                new { ofa.Estado, Motivo = $"Cambió la operación y no se pudo recalcular: {calculo.Message}" });
        }
        await unidadTrabajo.GuardarAsync(ct);
    }
    /// <summary>
    /// Corre el motor y deja la OFA (nueva o versión nueva de cálculo) lista para guardar.
    /// Recálculo (DBA-7): sube version_calculo, inserta líneas nuevas, copia las AGREGADA_MANUAL con linea_origen_id y su
    /// memoria original; los ajustes de monto no se copian y una OFA VALIDADA vuelve a BORRADOR.
    /// [POR CONFIRMAR DBA-7: regla del recálculo pendiente de confirmación del DBA]
    /// </summary>
    private async Task<ServiceResult<OfaEntidad>> CalcularInternoAsync(Operacion op, OfaEntidad? ofa, CancellationToken ct)
    {
        if (op.EstadoOperacion == EstadoOperacion.ANULADA)
        {
            return ServiceResult<OfaEntidad>.Error(409, OperacionAnulada, "La operación está anulada: no se calcula la OFA.");
        }
        if (op.TipoCarga == TipoCarga.BULTO)
        {
            return ServiceResult<OfaEntidad>.Error(409, OperacionFueraDeAlcance, "Carga suelta (BULTO): fuera del alcance de la etapa 1.");
        }
        if (!op.Facturable)
        {
            return ServiceResult<OfaEntidad>.Error(409, OperacionNoFacturable, "El facturador decidió no facturar esta operación.");
        }
        var contexto = await constructor.ConstruirAsync(op, paraLineaManual: false, ct);
        if (!contexto.Success)
        {
            return ServiceResult<OfaEntidad>.Error(contexto.StatusCode, contexto.Codigo!, contexto.Message!);
        }
        var ctx = contexto.Data!;
        var resultado = new MotorTarifas().Calcular(ctx.Contexto);
        var ahora = reloj.GetUtcNow();
        var nueva = ofa is null;
        var versionAnterior = ofa?.VersionCalculo;
        if (ofa is null)
        {
            ofa = new OfaEntidad
            {
                Id = Guid.CreateVersion7(),
                OperacionId = op.Id,
                Tipo = TipoOfa.PRINCIPAL,
                Estado = EstadoOfa.BORRADOR,
                VersionCalculo = 1,
                FechaCalculo = ahora,
            };
            repositorio.Agregar(ofa);
        }
        else
        {
            ofa.VersionCalculo++;
            ofa.Estado = EstadoOfa.BORRADOR;
            ofa.FechaCalculo = ahora;
        }
        var asignacion = await AsignacionAsync(ofa, op, nueva, ct);
        var articulosKit = ctx.Kit?.Articulos.ToHashSet();
        foreach (var r in resultado.Lineas)
        {
            var linea = new LineaOfa
            {
                Id = Guid.CreateVersion7(),
                OfaId = ofa.Id,
                AsignacionCobroId = asignacion.Id,
                TipoServicioId = r.TipoServicioId,
                ArticuloSapId = r.ArticuloSap,
                Cantidad = r.Cantidad,
                Unidad = r.Unidad.ToString(),
                Tarifa = r.Tarifa,
                Moneda = r.Moneda,
                Monto = r.MontoClp,
                EsExcepcion = r.EsExcepcion,
                VersionCalculo = ofa.VersionCalculo,
                Origen = OrigenLinea.CALCULADA,
                EnKitSap = articulosKit is null ? null : r.ArticuloSap is not null && articulosKit.Contains(r.ArticuloSap),
            };
            Agregar(ofa, linea, new MemoriaCalculoRegistro
            {
                Id = Guid.CreateVersion7(),
                LineaOfaId = linea.Id,
                ReglasAplicadas = MemoriaJson.Serializar(r.Memoria.Reglas),
                VersionConvenioId = r.Memoria.VersionConvenioId,
                TarifaSnapshotVer = ctx.VersionCatalogo,
                TipoCambio = r.Memoria.TipoCambioAplicado,
                CalculadoEn = ahora,
            });
        }
        if (versionAnterior is { } anterior)
        {
            foreach (var manual in ofa.Lineas.Where(l => l.VersionCalculo == anterior && l.Origen == OrigenLinea.AGREGADA_MANUAL).ToList())
            {
                CopiarLineaManual(ofa, manual, asignacion.Id, ahora);
            }
        }
        var vigentes = ofa.Lineas.Where(l => l.VersionCalculo == ofa.VersionCalculo).ToList();
        auditoria.Registrar("ofa", ofa.Id, nueva ? "CALCULAR" : "RECALCULAR",
            nueva ? null : new { VersionCalculo = versionAnterior },
            new { ofa.VersionCalculo, Lineas = vigentes.Count, TotalClp = vigentes.Sum(l => l.Monto), VersionCatalogo = ctx.VersionCatalogo, resultado.ServiciosDescartados });
        return ServiceResult<OfaEntidad>.Ok(ofa);
    }
    /// <summary>Línea agregada a mano copiada a la versión nueva, con su monto y memoria sin ajustes (DBA-7).</summary>
    private void CopiarLineaManual(OfaEntidad ofa, LineaOfa origen, Guid asignacionId, DateTimeOffset ahora)
    {
        var memorias = origen.Memorias.OrderBy(m => m.CalculadoEn).ToList();
        var original = memorias.First();
        var primerAjuste = memorias.Select(m => JsonSatelite.Leer<AjusteManualDto>(m.AjusteManual)).FirstOrDefault(a => a is not null);
        var copia = new LineaOfa
        {
            Id = Guid.CreateVersion7(),
            OfaId = ofa.Id,
            AsignacionCobroId = asignacionId,
            TipoServicioId = origen.TipoServicioId,
            ArticuloSapId = origen.ArticuloSapId,
            Cantidad = origen.Cantidad,
            Unidad = origen.Unidad,
            Tarifa = origen.Tarifa,
            Moneda = origen.Moneda,
            Monto = primerAjuste?.ValorAnterior ?? origen.Monto,
            EsExcepcion = origen.EsExcepcion,
            VersionCalculo = ofa.VersionCalculo,
            Origen = OrigenLinea.AGREGADA_MANUAL,
            EnKitSap = origen.EnKitSap,
            AgregadaPor = origen.AgregadaPor,
            AgregadaEn = origen.AgregadaEn,
            MotivoAgregada = origen.MotivoAgregada,
            LineaOrigenId = origen.Id,
        };
        var reglas = MemoriaJson.Leer(original.ReglasAplicadas);
        reglas.Add(MemoriaJson.Regla(MemoriaJson.CopiaRecalculo,
            ("lineaOrigenId", origen.Id.ToString()),
            ("versionCalculoOrigen", origen.VersionCalculo.ToString(CultureInfo.InvariantCulture)),
            ("ajustesNoCopiados", memorias.Count(m => m.AjusteManual is not null).ToString(CultureInfo.InvariantCulture))));
        Agregar(ofa, copia, new MemoriaCalculoRegistro
        {
            Id = Guid.CreateVersion7(),
            LineaOfaId = copia.Id,
            ReglasAplicadas = MemoriaJson.Serializar(reglas),
            VersionConvenioId = original.VersionConvenioId,
            TarifaSnapshotVer = original.TarifaSnapshotVer,
            TipoCambio = original.TipoCambio,
            CalculadoEn = ahora,
        });
    }
    /// <summary>
    /// Destinatario por defecto (RN-077): el cliente facturador que indicó AEXSA o, si no hay, el consignatario.
    /// El repartido manual entre varios destinatarios (RN-050) no tiene endpoint en la etapa 1.
    /// </summary>
    private async Task<AsignacionCobro> AsignacionAsync(OfaEntidad ofa, Operacion op, bool nueva, CancellationToken ct)
    {
        var clientes = await operaciones.ObtenerClientesAsync(new[] { op.ClienteId }.Where(i => i is not null).Select(i => i!.Value), ct);
        var rut = op.ClienteId is { } id && clientes.TryGetValue(id, out var cliente) ? cliente.Rut : string.Empty;
        var conFacturador = op.ClienteFacturadorId is not null && op.ClienteFacturadorId != op.ConsignatarioId;
        var rol = conFacturador ? RolCobro.FORWARDER : RolCobro.CONSIGNATARIO;
        var origen = op.ClienteFacturadorId is not null ? "AEXSA_CLIENTE_FACTURADOR" : "DEFECTO_CONSIGNATARIO";
        var asignacion = nueva ? null : (await repositorio.ListarAsignacionesAsync(ofa.Id, ct)).FirstOrDefault();
        if (asignacion is null)
        {
            asignacion = new AsignacionCobro { Id = Guid.CreateVersion7(), OfaId = ofa.Id };
            repositorio.AgregarAsignacion(asignacion);
        }
        asignacion.DestinatarioRut = rut;
        asignacion.Rol = rol;
        asignacion.OrigenInstruccion = origen;
        return asignacion;
    }
    private void Agregar(OfaEntidad ofa, LineaOfa linea, MemoriaCalculoRegistro memoria)
    {
        linea.Memorias.Add(memoria);
        ofa.Lineas.Add(linea);
        repositorio.AgregarLinea(linea);
        repositorio.AgregarMemoria(memoria);
    }
    public async Task<ServiceResult<OfaDto>> ObtenerAsync(Guid ofaId, CancellationToken ct)
    {
        var ofa = await repositorio.ObtenerAsync(ofaId, ct);
        if (ofa is null)
        {
            return ServiceResult<OfaDto>.NoEncontrado("La OFA no existe.");
        }
        var op = (await operaciones.ObtenerAsync(ofa.OperacionId, ct))!;
        return ServiceResult<OfaDto>.Ok(await DtoAsync(ofa, op, ct));
    }
    public async Task<ServiceResult<LineaOfaDto>> AgregarLineaAsync(Guid ofaId, AgregarLineaRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Motivo))
        {
            return ServiceResult<LineaOfaDto>.Invalido("El motivo es obligatorio.");
        }
        if (r.Cantidad <= 0)
        {
            return ServiceResult<LineaOfaDto>.Invalido("La cantidad debe ser mayor que 0.");
        }
        if (r.Tarifa is < 0)
        {
            return ServiceResult<LineaOfaDto>.Invalido("La tarifa no puede ser negativa.");
        }
        if (r.Moneda is not null && r.Tarifa is null)
        {
            return ServiceResult<LineaOfaDto>.Invalido("La moneda solo se indica junto con la tarifa.");
        }
        var ofa = await repositorio.ObtenerAsync(ofaId, ct);
        if (ofa is null)
        {
            return ServiceResult<LineaOfaDto>.NoEncontrado("La OFA no existe.");
        }
        if (!Editable(ofa))
        {
            return ServiceResult<LineaOfaDto>.Error(409, OfaYaEnviada, $"La OFA está {ofa.Estado}: no admite cambios.");
        }
        var tipo = await catalogo.ObtenerTipoServicioAsync(r.TipoServicioId, ct);
        if (tipo is null)
        {
            return ServiceResult<LineaOfaDto>.Invalido($"El servicio {r.TipoServicioId} no existe en el catálogo de servicios.");
        }
        var op = (await operaciones.ObtenerAsync(ofa.OperacionId, ct))!;
        var contexto = await constructor.ConstruirAsync(op, paraLineaManual: true, ct);
        if (!contexto.Success)
        {
            return ServiceResult<LineaOfaDto>.Error(contexto.StatusCode, contexto.Codigo!, contexto.Message!);
        }
        var motivo = r.Motivo.Trim();
        var ahora = reloj.GetUtcNow();
        var baseCtx = contexto.Data!.Contexto;
        // La cantidad la fija la persona: sin Regla 0, sin días libres ni días de contenedor, sin cantidad de DUS.
        var ctx = baseCtx with
        {
            Servicios = [new ServicioACobrar(tipo.TipoServicioId, r.Cantidad, tipo.UnidadNormalizada)],
            Contenedores = [],
            CantidadDus = null,
            Recargos = [],
            Convenio = baseCtx.Convenio is { } c ? c with { DiasLibres = null } : null,
        };
        var motor = r.Tarifa is { } tarifa
            ? new MotorTarifas([new FuenteManual(tarifa, r.Moneda ?? Moneda.CLP), new FuenteConvenio(), new FuentePublica()])
            : new MotorTarifas();
        var calculada = motor.Calcular(ctx).Lineas.Single();
        var reglas = new List<ReglaAplicada>
        {
            MemoriaJson.Regla(MemoriaJson.AgregadoManual,
                ("usuario", usuario.UsuarioAuditoria),
                ("fecha", ahora.ToString("O", CultureInfo.InvariantCulture)),
                ("motivo", motivo),
                ("tarifaInformada", r.Tarifa?.ToString(CultureInfo.InvariantCulture))),
        };
        reglas.AddRange(calculada.Memoria.Reglas);
        var asignacion = (await repositorio.ListarAsignacionesAsync(ofa.Id, ct)).FirstOrDefault();
        var kit = contexto.Data.Kit?.Articulos.ToHashSet();
        var linea = new LineaOfa
        {
            Id = Guid.CreateVersion7(),
            OfaId = ofa.Id,
            AsignacionCobroId = asignacion?.Id,
            TipoServicioId = tipo.TipoServicioId,
            ArticuloSapId = calculada.ArticuloSap,
            Cantidad = calculada.Cantidad,
            Unidad = calculada.Unidad.ToString(),
            Tarifa = calculada.Tarifa,
            Moneda = calculada.Moneda,
            Monto = calculada.MontoClp,
            EsExcepcion = calculada.EsExcepcion,
            VersionCalculo = ofa.VersionCalculo,
            Origen = OrigenLinea.AGREGADA_MANUAL,
            EnKitSap = kit is null ? null : calculada.ArticuloSap is not null && kit.Contains(calculada.ArticuloSap),
            AgregadaPor = usuario.UsuarioAuditoria,
            AgregadaEn = ahora,
            MotivoAgregada = motivo,
        };
        Agregar(ofa, linea, new MemoriaCalculoRegistro
        {
            Id = Guid.CreateVersion7(),
            LineaOfaId = linea.Id,
            ReglasAplicadas = MemoriaJson.Serializar(reglas),
            VersionConvenioId = calculada.Memoria.VersionConvenioId,
            TarifaSnapshotVer = contexto.Data.VersionCatalogo,
            TipoCambio = calculada.Memoria.TipoCambioAplicado,
            CalculadoEn = ahora,
        });
        VolverABorrador(ofa, "Se agregó un ítem a mano.");
        auditoria.Registrar("linea_ofa", linea.Id, "AGREGAR_MANUAL", null, new
        {
            ofaId = ofa.Id, linea.TipoServicioId, linea.Cantidad, linea.Tarifa, linea.Moneda, monto = linea.Monto, motivo,
        });
        await unidadTrabajo.GuardarAsync(ct);
        return ServiceResult<LineaOfaDto>.Created(await LineaDtoAsync(linea, ct));
    }
    public async Task<ServiceResult<LineaOfaDto>> AjustarLineaAsync(Guid ofaId, Guid lineaId, AjusteManualRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Motivo))
        {
            return ServiceResult<LineaOfaDto>.Invalido("El motivo del ajuste es obligatorio.");
        }
        if (r.NuevoMonto < 0)
        {
            return ServiceResult<LineaOfaDto>.Invalido("El monto no puede ser negativo.");
        }
        var ofa = await repositorio.ObtenerAsync(ofaId, ct);
        var linea = ofa?.Lineas.FirstOrDefault(l => l.Id == lineaId);
        if (ofa is null || linea is null)
        {
            return ServiceResult<LineaOfaDto>.NoEncontrado("La línea no existe en la OFA.");
        }
        if (!Editable(ofa))
        {
            return ServiceResult<LineaOfaDto>.Error(409, OfaYaEnviada, $"La OFA está {ofa.Estado}: no admite cambios.");
        }
        if (linea.VersionCalculo != ofa.VersionCalculo)
        {
            return ServiceResult<LineaOfaDto>.Error(409, "LINEA_NO_VIGENTE",
                $"La línea es de la versión de cálculo {linea.VersionCalculo}; la vigente es la {ofa.VersionCalculo}.");
        }
        var motivo = r.Motivo.Trim();
        var ahora = reloj.GetUtcNow();
        var vigente = linea.Memorias.OrderByDescending(m => m.CalculadoEn).First();
        var reconstruida = new ResultadoLinea(
            linea.TipoServicioId, linea.Cantidad, Enum.TryParse<UnidadCobro>(linea.Unidad, out var u) ? u : UnidadCobro.UNIDAD,
            linea.Tarifa, linea.Moneda, linea.Monto, linea.EsExcepcion, SinTarifa: false, SinTipoCambio: false, linea.ArticuloSapId,
            new MemoriaLinea(MemoriaJson.Leer(vigente.ReglasAplicadas), vigente.VersionConvenioId, null, vigente.TarifaSnapshotVer,
                vigente.TipoCambio, PisoActivado: false, Ajuste: null));
        // Regla 7: el monto lo fija la persona; queda {usuario, fecha, motivo, valorAnterior, valorNuevo} (REQ-006).
        var ajustada = MotorTarifas.AplicarAjuste(reconstruida, usuario.UsuarioAuditoria, ahora, motivo, r.NuevoMonto);
        var ajuste = ajustada.Memoria.Ajuste!;
        var memoria = new MemoriaCalculoRegistro
        {
            Id = Guid.CreateVersion7(),
            LineaOfaId = linea.Id,
            ReglasAplicadas = MemoriaJson.Serializar(ajustada.Memoria.Reglas),
            VersionConvenioId = vigente.VersionConvenioId,
            TarifaSnapshotVer = vigente.TarifaSnapshotVer,
            TipoCambio = vigente.TipoCambio,
            ReglaConfigId = vigente.ReglaConfigId,
            AjusteManual = JsonSatelite.Serializar(new AjusteManualDto(ajuste.Usuario, ajuste.Fecha, ajuste.Motivo, ajuste.ValorAnterior, ajuste.ValorNuevo)),
            CalculadoEn = ahora,
        };
        linea.Memorias.Add(memoria);
        repositorio.AgregarMemoria(memoria);
        linea.Monto = ajustada.MontoClp;
        VolverABorrador(ofa, "Se ajustó el monto de una línea.");
        auditoria.Registrar("linea_ofa", linea.Id, "AJUSTE_MANUAL", new { monto = ajuste.ValorAnterior },
            new { monto = ajuste.ValorNuevo, motivo, ofaId = ofa.Id });
        await unidadTrabajo.GuardarAsync(ct);
        return ServiceResult<LineaOfaDto>.Ok(await LineaDtoAsync(linea, ct));
    }
    public async Task<ServiceResult<ResultadoValidacionDto>> ValidarAsync(Guid ofaId, CancellationToken ct)
    {
        var ofa = await repositorio.ObtenerAsync(ofaId, ct);
        if (ofa is null)
        {
            return ServiceResult<ResultadoValidacionDto>.NoEncontrado("La OFA no existe.");
        }
        if (!Editable(ofa))
        {
            return ServiceResult<ResultadoValidacionDto>.Error(409, OfaYaEnviada, $"La OFA está {ofa.Estado}.");
        }
        var op = (await operaciones.ObtenerAsync(ofa.OperacionId, ct))!;
        var tipos = (await catalogo.ListarTiposServicioAsync(ct)).ToDictionary(t => t.TipoServicioId, t => t.Descripcion);
        var faltantes = new List<string>();
        if (op.EstadoOperacion == EstadoOperacion.ANULADA)
        {
            faltantes.Add("La operación está anulada.");
        }
        var asignaciones = await repositorio.ListarAsignacionesAsync(ofa.Id, ct);
        if (asignaciones.Count == 0 || asignaciones.Any(a => string.IsNullOrWhiteSpace(a.DestinatarioRut)))
        {
            faltantes.Add("Falta el destinatario del cobro.");
        }
        var vigentes = ofa.Lineas.Where(l => l.VersionCalculo == ofa.VersionCalculo).OrderBy(l => l.TipoServicioId).ToList();
        if (vigentes.Count == 0)
        {
            faltantes.Add("La OFA no tiene líneas.");
        }
        foreach (var l in vigentes)
        {
            var nombre = $"Servicio {l.TipoServicioId} {tipos.GetValueOrDefault(l.TipoServicioId)}".Trim();
            var reglas = MemoriaJson.Leer(l.Memorias.OrderByDescending(m => m.CalculadoEn).First().ReglasAplicadas);
            var ajustada = MemoriaJson.ConAjuste(reglas);
            if (MemoriaJson.SinTarifa(reglas) && !ajustada)
            {
                faltantes.Add($"{nombre}: sin tarifa (ni convenio ni pública); ajuste el monto o vincule el artículo con precio.");
            }
            if (MemoriaJson.SinTipoCambio(reglas) && !ajustada)
            {
                faltantes.Add($"{nombre}: sin tipo de cambio de {l.Moneda} para la fecha de la operación.");
            }
            if (l.ArticuloSapId is null)
            {
                faltantes.Add($"{nombre}: sin artículo SAP vinculado.");
            }
        }
        foreach (var p in op.Pendientes.Where(p => p.ResueltoEn is null && p.BloqueaValidacion).OrderBy(p => p.Tipo))
        {
            faltantes.Add(CalculadorPendientes.ConDecision.Contains(p.Tipo)
                ? $"Pendiente {p.Tipo}: exige una decisión registrada."
                : $"Pendiente {p.Tipo}: complete el dato en la operación.");
        }
        var valida = faltantes.Count == 0;
        if (valida && ofa.Estado != EstadoOfa.VALIDADA)
        {
            ofa.Estado = EstadoOfa.VALIDADA;
            auditoria.Registrar("ofa", ofa.Id, "VALIDAR", new { Estado = EstadoOfa.BORRADOR },
                new { ofa.Estado, ofa.VersionCalculo, TotalClp = vigentes.Sum(l => l.Monto) });
            await unidadTrabajo.GuardarAsync(ct);
        }
        return ServiceResult<ResultadoValidacionDto>.Ok(new ResultadoValidacionDto(valida, faltantes, ofa.Estado));
    }
    public async Task<ServiceResult<EnvioOfaDto>> EnviarAsync(Guid ofaId, CancellationToken ct)
    {
        var ofa = await repositorio.ObtenerAsync(ofaId, ct);
        if (ofa is null)
        {
            return ServiceResult<EnvioOfaDto>.NoEncontrado("La OFA no existe.");
        }
        // Fase A: la OFA validada se digita en SAP. El envío por Outbox es la tarea 3.3 (Fase B).
        return ServiceResult<EnvioOfaDto>.Error(409, EnvioSapDeshabilitado, opciones.Value.EnvioSapHabilitado
            ? "Integración con SAP no disponible en la etapa 1."
            : "El envío a SAP está deshabilitado (Ofa:EnvioSapHabilitado = false): digite la OFA en SAP.");
    }
    private void VolverABorrador(OfaEntidad ofa, string motivo)
    {
        if (ofa.Estado != EstadoOfa.VALIDADA)
        {
            return;
        }
        ofa.Estado = EstadoOfa.BORRADOR;
        auditoria.Registrar("ofa", ofa.Id, "VOLVER_A_BORRADOR", new { Estado = EstadoOfa.VALIDADA }, new { ofa.Estado, Motivo = motivo });
    }
    private async Task<OfaDto> DtoAsync(OfaEntidad ofa, Operacion op, CancellationToken ct)
    {
        var vigentes = ofa.Lineas.Where(l => l.VersionCalculo == ofa.VersionCalculo).OrderBy(l => l.Origen).ThenBy(l => l.TipoServicioId).ToList();
        var asignaciones = await repositorio.ListarAsignacionesAsync(ofa.Id, ct);
        var clientes = await operaciones.ObtenerClientesAsync(new[] { op.ClienteId }.Where(i => i is not null).Select(i => i!.Value), ct);
        var razon = clientes.Values.ToDictionary(c => c.Rut, c => c.RazonSocial);
        var versionCatalogo = vigentes.SelectMany(l => l.Memorias).Select(m => m.TarifaSnapshotVer).FirstOrDefault(v => v is not null)
                              ?? (await catalogo.ObtenerVersionVigenteAsync(ct))?.Version;
        var lineas = new List<LineaOfaDto>();
        foreach (var l in vigentes)
        {
            lineas.Add(await LineaDtoAsync(l, ct));
        }
        return new OfaDto(
            ofa.Id, ofa.OperacionId, op.IdsAexsa, ofa.Tipo, ofa.Estado, ofa.VersionCalculo, ofa.FechaCalculo,
            asignaciones.Select(a => new AsignacionCobroDto(a.Id, a.DestinatarioRut, razon.GetValueOrDefault(a.DestinatarioRut), a.Rol, a.OrigenInstruccion)).ToList(),
            lineas, vigentes.Sum(l => l.Monto),
            versionCatalogo is { } vc ? await constructor.KitAsync(op, vc, ct) : null,
            versionCatalogo, opciones.Value.EnvioSapHabilitado);
    }
    private async Task<LineaOfaDto> LineaDtoAsync(LineaOfa l, CancellationToken ct)
    {
        var tipo = await catalogo.ObtenerTipoServicioAsync(l.TipoServicioId, ct);
        var memoria = l.Memorias.OrderByDescending(m => m.CalculadoEn).First();
        var reglas = MemoriaJson.Leer(memoria.ReglasAplicadas);
        var numeros = memoria.VersionConvenioId is { } vid
            ? await repositorio.NumerosVersionConvenioAsync([vid], ct)
            : new Dictionary<Guid, int>();
        var ajustada = MemoriaJson.ConAjuste(reglas);
        return new LineaOfaDto(
            l.Id, l.AsignacionCobroId, l.TipoServicioId, tipo?.Descripcion, l.ArticuloSapId, l.Cantidad,
            Enum.TryParse<UnidadCobro>(l.Unidad, out var u) ? u : null, l.Tarifa, l.Moneda, l.Monto, l.EsExcepcion, l.Origen,
            l.EnKitSap, l.VersionCalculo, l.AgregadaPor, l.AgregadaEn, l.MotivoAgregada, l.LineaOrigenId,
            MemoriaJson.Fuente(reglas), MemoriaJson.Lista(reglas), MemoriaJson.SinTarifa(reglas) && !ajustada,
            MemoriaJson.SinTipoCambio(reglas) && !ajustada, l.Memorias.Count,
            new MemoriaCalculoDto(
                memoria.Id,
                reglas.Select(r => new ReglaAplicadaDto(r.Regla, r.Version,
                    r.Detalle.GroupBy(d => d.Clave).ToDictionary(g => g.Key, g => g.Last().Valor))).ToList(),
                memoria.VersionConvenioId,
                memoria.VersionConvenioId is { } v && numeros.TryGetValue(v, out var n) ? n : null,
                memoria.TarifaSnapshotVer, memoria.TipoCambio, memoria.ReglaConfigId,
                JsonSatelite.Leer<AjusteManualDto>(memoria.AjusteManual), memoria.CalculadoEn));
    }
}
/// <summary>Tarifa informada por la persona al agregar un ítem a mano (tiene precedencia sobre convenio y pública).</summary>
public sealed class FuenteManual(decimal tarifa, Moneda moneda) : IFuenteTarifa
{
    public TarifaResuelta? Resolver(ContextoCalculo ctx, int tipoServicioId) =>
        new(tarifa, moneda, "MANUAL", DeConvenio: false, Lista: null);
}
