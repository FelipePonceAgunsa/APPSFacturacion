using Microsoft.Extensions.Options;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Comun;
using Satelite.Aplicacion.Motor;
using Satelite.Aplicacion.Operaciones;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Dominio.Motor;
namespace Satelite.Aplicacion.Ofa;
/// <summary>Contexto del motor y datos de la versión del catálogo con que se arma la OFA.</summary>
public sealed record ContextoOfa(
    ContextoCalculo Contexto, int VersionCatalogo, IReadOnlyDictionary<int, TipoServicio> Tipos, KitSugeridoDto? Kit);
/// <summary>
/// Arma el <see cref="ContextoCalculo"/> de una operación con snapshots fijos (RNF-03): versión de convenio guardada en la
/// operación, tarifa pública de la versión vigente del catálogo y tipo de cambio del día.
/// </summary>
public sealed class ConstructorContexto(IOfaRepositorio repositorio, ICatalogoRepositorio catalogo, IOptions<MotorOptions> motor)
{
    public const string CatalogoNoCargado = "CATALOGO_NO_CARGADO";
    public const string TerminalSinMapear = "TERMINAL_SIN_MAPEAR";
    public const string ConceptosDirectoSinConfigurar = "CONCEPTOS_DIRECTO_SIN_CONFIGURAR";
    /// <param name="paraLineaManual">true: contexto base de un ítem agregado a mano (no aplica la Regla 0).</param>
    public async Task<ServiceResult<ContextoOfa>> ConstruirAsync(Operacion op, bool paraLineaManual, CancellationToken ct)
    {
        var snapshot = await catalogo.ObtenerVersionVigenteAsync(ct);
        if (snapshot is null)
        {
            return ServiceResult<ContextoOfa>.Error(409, CatalogoNoCargado,
                "No hay una versión confirmada del catálogo SAP: importe el catálogo antes de calcular.");
        }
        var tipos = (await catalogo.ListarTiposServicioAsync(ct)).ToDictionary(t => t.TipoServicioId);
        var grupo = op.TerminalGrupo ?? GrupoTerminal.SIN_MAPEAR;
        var aplicaRegla0 = !paraLineaManual && op.TipoRetiro == TipoRetiro.DIRECTO && !op.ConConvenio;
        ConceptosDirecto? conceptos = null;
        if (aplicaRegla0)
        {
            // Con el terminal sin mapear no se decide la refacturación del directo (ADR-015): la operación queda pendiente.
            if (grupo == GrupoTerminal.SIN_MAPEAR)
            {
                return ServiceResult<ContextoOfa>.Error(409, TerminalSinMapear,
                    "Directo sin convenio con el terminal sin mapear: asigne el grupo del terminal en Configuración → Terminales.");
            }
            conceptos = Conceptos(motor.Value.ConceptosDirecto, tipos);
            if (conceptos is null)
            {
                return ServiceResult<ContextoOfa>.Error(409, ConceptosDirectoSinConfigurar,
                    "Faltan los servicios del directo sin convenio en la configuración (Motor:ConceptosDirecto).");
            }
        }
        var fecha = ResolutorOperacion.FechaOperacion(op);
        // Convenio: la versión que se fijó al ingresar o al decidir (no se busca de nuevo aquí).
        VersionConvenioSnapshot? convenio = null;
        if (op.VersionConvenioId is { } vid && await repositorio.ObtenerVersionConvenioAsync(vid, ct) is { } v)
        {
            convenio = new VersionConvenioSnapshot(v.Id, v.Numero, v.TcMinimo, v.DiasLibres,
                v.Servicios.ToDictionary(s => s.TipoServicioId, s => new TarifaConvenio(s.Tarifa, s.Moneda)));
        }
        // Tarifa pública: vínculo confirmado servicio → artículo, y precio de la lista pública vigente a la fecha.
        var vinculos = (await catalogo.ListarVinculosAsync(ct))
            .Where(x => x.ConfirmadoEn is not null)
            .GroupBy(x => x.TipoServicioId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ConfirmadoEn).First().ArticuloId);
        var precios = await repositorio.ListarTarifasPublicasAsync(snapshot.Version, vinculos.Values.Distinct().ToList(), ct);
        var publica = new Dictionary<int, TarifaPublica>();
        foreach (var (tipoServicioId, articulo) in vinculos)
        {
            var precio = precios
                .Where(p => p.ArticuloId == articulo && p.VigenteDesde <= fecha && (p.VigenteHasta is null || p.VigenteHasta >= fecha))
                .OrderByDescending(p => p.VigenteDesde)
                .ThenBy(p => p.ListaPreciosId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (precio is not null)
            {
                publica[tipoServicioId] = new TarifaPublica(articulo, precio.Precio, precio.Moneda, precio.ListaPreciosId);
            }
        }
        var servicios = op.Servicios
            .Where(s => s.Cantidad is not null)
            .OrderBy(s => s.TipoServicioId)
            .Select(s => new ServicioACobrar(s.TipoServicioId, s.Cantidad!.Value, Unidad(s, tipos), s.Contenedor))
            .ToList();
        var contexto = new ContextoCalculo
        {
            OperacionId = op.Id,
            EsExportacion = op.Sentido == Sentido.EXPORTACION,
            CantidadDus = op.CantidadDus,
            Retiro = paraLineaManual ? null : op.TipoRetiro,
            Terminal = grupo,
            Convenio = convenio,
            TarifaPublica = new TarifaPublicaSnapshot(snapshot.Version, publica),
            ArticulosSap = vinculos,
            // [SUPUESTO: si el día del visado no tiene tipo de cambio se usa el último día anterior con valor]
            TipoCambioUsd = await repositorio.TipoCambioAsync(Moneda.USD, fecha, ct),
            TipoCambioUf = await repositorio.TipoCambioAsync(Moneda.UF, fecha, ct),
            Servicios = servicios,
            Contenedores = op.Contenedores.OrderBy(c => c.Numero).Select(c => new CondicionAlmacenaje(
                c.Numero, c.DiasAlmacenaje, c.DiasBajoAforo, DiasAforoCalculados: DiasDesdeInspeccion(c))).ToList(),
            // [SUPUESTO: sin fuente de la instrucción de recargo en la etapa 1 (PQ-020); se cubre con ítem manual o ajuste]
            Recargos = [],
            ConceptosDirecto = conceptos,
            CantidadBls = op.Bls.Count,
            CantidadContenedores = op.Contenedores.Count,
        };
        return ServiceResult<ContextoOfa>.Ok(new ContextoOfa(contexto, snapshot.Version, tipos, await KitAsync(op, snapshot.Version, ct)));
    }
    /// <summary>Kit cuya ruta es la de la operación ("Directo sin convenio", "Indirecto con convenio"...), sin distinguir mayúsculas.</summary>
    public async Task<KitSugeridoDto?> KitAsync(Operacion op, int versionCatalogo, CancellationToken ct)
    {
        if (op.TipoRetiro is null)
        {
            return null;
        }
        var ruta = $"{(op.TipoRetiro == TipoRetiro.DIRECTO ? "Directo" : "Indirecto")} {(op.ConConvenio ? "con" : "sin")} convenio";
        var kit = (await catalogo.ListarKitsAsync(versionCatalogo, null, ct))
            .FirstOrDefault(k => string.Equals(k.Ruta?.Trim(), ruta, StringComparison.OrdinalIgnoreCase));
        return kit is null ? null : new KitSugeridoDto(kit.KitCodigo, kit.Nombre, kit.Ruta, kit.Articulos.Select(a => a.ArticuloId).ToList());
    }
    private static UnidadCobro Unidad(ServicioPrestado s, IReadOnlyDictionary<int, TipoServicio> tipos) =>
        Enum.TryParse<UnidadCobro>(s.Unidad, out var u) ? u : tipos[s.TipoServicioId].UnidadNormalizada;
    /// <summary>Los días bajo aforo guardados son los que salen de la inspección (no los completó el facturador).</summary>
    private static bool DiasDesdeInspeccion(ContenedorOperacion c) =>
        c.DiasBajoAforo is { } dias && CalculadorPendientes.Inspecciones(c)
            .Where(i => i.AsignadaEn is not null && i.FinalizadaEn is not null)
            .Any(i => DiasBajoAforo.Calcular(i.AsignadaEn!.Value, i.FinalizadaEn) == dias);
    private static ConceptosDirecto? Conceptos(ConceptosDirectoOptions o, IReadOnlyDictionary<int, TipoServicio> tipos)
    {
        int?[] ids = [o.Visacion, o.SecurityFee, o.Refacturacion, o.Recepcion, o.Despacho, o.Movilizacion];
        if (ids.Any(i => i is null || !tipos.ContainsKey(i.Value)))
        {
            return null;
        }
        ConceptoDirecto Concepto(int id) => new(id, tipos[id].UnidadNormalizada);
        return new ConceptosDirecto(Concepto(o.Visacion!.Value), Concepto(o.SecurityFee!.Value), Concepto(o.Refacturacion!.Value),
            o.Recepcion!.Value, o.Despacho!.Value, o.Movilizacion!.Value);
    }
}
