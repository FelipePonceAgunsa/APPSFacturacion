using Satelite.Aplicacion.Comun;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Aplicacion.Catalogo;

public interface ICatalogoService
{
    Task<ServiceResult<List<TipoServicioDto>>> ListarTiposServicioAsync(CancellationToken ct);
    Task<ServiceResult<CatalogoResumenDto>> ObtenerResumenAsync(CancellationToken ct);
    Task<ServiceResult<List<ArticuloSapDto>>> ListarArticulosAsync(string? busqueda, CancellationToken ct);
    Task<ServiceResult<List<PrecioListaDto>>> ListarPreciosAsync(string codigoLista, CancellationToken ct);
    Task<ServiceResult<List<KitSapDto>>> ListarKitsAsync(string? ruta, CancellationToken ct);
    Task<ServiceResult<List<TipoCambioDto>>> ListarTiposCambioAsync(DateOnly? desde, Moneda? moneda, CancellationToken ct);

    /// <summary>Tipo de cambio MANUAL de un día. Gana el de SAP: si el día ya lo tiene, 409 TIPO_CAMBIO_DE_SAP_EXISTE.</summary>
    Task<ServiceResult<TipoCambioDto>> RegistrarTipoCambioManualAsync(TipoCambioDto request, CancellationToken ct);

    Task<ServiceResult<List<VinculoServicioArticuloDto>>> ListarVinculosAsync(string? estado, CancellationToken ct);

    /// <summary>Una persona fija el artículo SAP de un servicio (origen MANUAL, confirmado).</summary>
    Task<ServiceResult<VinculoServicioArticuloDto>> FijarVinculoAsync(int tipoServicioId, VinculoRequest request, CancellationToken ct);
}

/// <summary>Consultas del catálogo SAP vigente, tipo de cambio y vínculos servicio ↔ artículo (tareas 1.1 y 1.1b).</summary>
public sealed class CatalogoService(
    ICatalogoRepositorio repositorio,
    IUnidadTrabajo unidadTrabajo,
    IAuditoria auditoria,
    ICurrentUser usuario,
    TimeProvider reloj) : ICatalogoService
{
    public const string TipoCambioDeSapExiste = "TIPO_CAMBIO_DE_SAP_EXISTE";

    public async Task<ServiceResult<List<TipoServicioDto>>> ListarTiposServicioAsync(CancellationToken ct)
    {
        var tipos = await repositorio.ListarTiposServicioAsync(ct);
        return ServiceResult<List<TipoServicioDto>>.Ok(tipos
            .Select(t => new TipoServicioDto(t.TipoServicioId, t.Codigo, t.Descripcion, t.UnidadNormalizada, t.Habilitado))
            .ToList());
    }

    public async Task<ServiceResult<CatalogoResumenDto>> ObtenerResumenAsync(CancellationToken ct)
    {
        var vigente = await repositorio.ObtenerVersionVigenteAsync(ct);
        if (vigente is null)
        {
            return ServiceResult<CatalogoResumenDto>.NoEncontrado("Aún no hay una versión confirmada del catálogo SAP.");
        }

        var (articulos, listas, kits) = await repositorio.ContarAsync(vigente.Version, ct);
        var tipoCambioAl = await repositorio.UltimaFechaTipoCambioAsync(ct);
        return ServiceResult<CatalogoResumenDto>.Ok(new CatalogoResumenDto(
            vigente.Version, vigente.Fecha, vigente.Origen, articulos, listas, kits, tipoCambioAl));
    }

    public async Task<ServiceResult<List<ArticuloSapDto>>> ListarArticulosAsync(string? busqueda, CancellationToken ct)
    {
        var vigente = await repositorio.ObtenerVersionVigenteAsync(ct);
        if (vigente is null)
        {
            return ServiceResult<List<ArticuloSapDto>>.Ok([]);
        }

        var articulos = await repositorio.ListarArticulosAsync(vigente.Version, busqueda?.Trim(), ct);
        return ServiceResult<List<ArticuloSapDto>>.Ok(articulos
            .Select(a => new ArticuloSapDto(a.Codigo, a.Descripcion, a.UnidadMedida, a.Activo, a.Grupo))
            .ToList());
    }

    public async Task<ServiceResult<List<PrecioListaDto>>> ListarPreciosAsync(string codigoLista, CancellationToken ct)
    {
        var vigente = await repositorio.ObtenerVersionVigenteAsync(ct);
        var lista = vigente is null ? null : await repositorio.ObtenerListaPreciosAsync(vigente.Version, codigoLista, ct);
        if (vigente is null || lista is null)
        {
            return ServiceResult<List<PrecioListaDto>>.NoEncontrado($"La lista de precios {codigoLista} no existe en el catálogo vigente.");
        }

        var precios = await repositorio.ListarPreciosAsync(vigente.Version, codigoLista, ct);
        return ServiceResult<List<PrecioListaDto>>.Ok(precios
            .Select(p => new PrecioListaDto(lista.Codigo, lista.Nombre, p.ArticuloId, p.Precio, p.Moneda, p.VigenteDesde, p.VigenteHasta))
            .ToList());
    }

    public async Task<ServiceResult<List<KitSapDto>>> ListarKitsAsync(string? ruta, CancellationToken ct)
    {
        var vigente = await repositorio.ObtenerVersionVigenteAsync(ct);
        if (vigente is null)
        {
            return ServiceResult<List<KitSapDto>>.Ok([]);
        }

        var kits = await repositorio.ListarKitsAsync(vigente.Version, string.IsNullOrWhiteSpace(ruta) ? null : ruta.Trim(), ct);
        return ServiceResult<List<KitSapDto>>.Ok(kits
            .Select(k => new KitSapDto(k.KitCodigo, k.Nombre, k.Ruta, true,
                k.Articulos.OrderBy(a => a.ArticuloId, StringComparer.Ordinal).Select(a => new KitArticuloDto(a.ArticuloId, a.Cantidad)).ToList()))
            .ToList());
    }

    public async Task<ServiceResult<List<TipoCambioDto>>> ListarTiposCambioAsync(DateOnly? desde, Moneda? moneda, CancellationToken ct)
    {
        var tipos = await repositorio.ListarTiposCambioAsync(desde, moneda, ct);
        return ServiceResult<List<TipoCambioDto>>.Ok(tipos.Select(Dto).ToList());
    }

    public async Task<ServiceResult<TipoCambioDto>> RegistrarTipoCambioManualAsync(TipoCambioDto request, CancellationToken ct)
    {
        if (request.Moneda is not (Moneda.USD or Moneda.UF))
        {
            return ServiceResult<TipoCambioDto>.Invalido("La moneda del tipo de cambio debe ser USD o UF.");
        }
        if (request.Valor <= 0)
        {
            return ServiceResult<TipoCambioDto>.Invalido("El valor del tipo de cambio debe ser mayor que 0.");
        }

        var actual = (await repositorio.ObtenerTiposCambioAsync(request.Fecha, request.Fecha, ct))
            .FirstOrDefault(t => t.Moneda == request.Moneda);
        if (actual is { Fuente: FuenteTipoCambio.API or FuenteTipoCambio.ARCHIVO })
        {
            return ConflictoSap(request, actual.Fuente);
        }

        if (actual is null)
        {
            actual = new TipoCambio
            {
                Fecha = request.Fecha,
                Moneda = request.Moneda,
                Valor = request.Valor,
                Fuente = FuenteTipoCambio.MANUAL,
                CargadoPor = usuario.UsuarioAuditoria,
            };
            repositorio.AgregarTipoCambio(actual);
            auditoria.Registrar("tipo_cambio", null, "REGISTRAR_MANUAL", null, Dto(actual));
        }
        else
        {
            var anterior = Dto(actual);
            actual.Valor = request.Valor;
            actual.CargadoPor = usuario.UsuarioAuditoria;
            auditoria.Registrar("tipo_cambio", null, "CORREGIR_MANUAL", anterior, Dto(actual));
        }

        try
        {
            await unidadTrabajo.GuardarAsync(ct);
        }
        catch (ReglaDatosException ex) when (ex.Codigo == TipoCambioDeSapExiste)
        {
            // Respaldo: el trigger trg_tipo_cambio_sap_prevalece (E1-3) rechazó el cambio.
            return ServiceResult<TipoCambioDto>.Error(409, TipoCambioDeSapExiste, ex.Message);
        }

        return ServiceResult<TipoCambioDto>.Created(Dto(actual));
    }

    public async Task<ServiceResult<List<VinculoServicioArticuloDto>>> ListarVinculosAsync(string? estado, CancellationToken ct)
    {
        if (estado is not null && !EstadoVinculo.Todos.Contains(estado))
        {
            return ServiceResult<List<VinculoServicioArticuloDto>>.Invalido(
                $"Estado inválido: {estado}. Valores: {string.Join(", ", EstadoVinculo.Todos)}.");
        }

        var servicios = await repositorio.ListarTiposServicioAsync(ct);
        var vinculos = (await repositorio.ListarVinculosAsync(ct)).ToLookup(v => v.TipoServicioId);
        var resultado = servicios.Select(s => Dto(s, vinculos[s.TipoServicioId].FirstOrDefault()))
            .Where(v => estado is null || v.Estado == estado)
            .ToList();
        return ServiceResult<List<VinculoServicioArticuloDto>>.Ok(resultado);
    }

    public async Task<ServiceResult<VinculoServicioArticuloDto>> FijarVinculoAsync(int tipoServicioId, VinculoRequest request, CancellationToken ct)
    {
        var servicio = await repositorio.ObtenerTipoServicioAsync(tipoServicioId, ct);
        if (servicio is null)
        {
            return ServiceResult<VinculoServicioArticuloDto>.NoEncontrado($"El servicio {tipoServicioId} no existe.");
        }

        var articulo = request.ArticuloSap?.Trim();
        var vigente = await repositorio.ObtenerVersionVigenteAsync(ct);
        if (string.IsNullOrEmpty(articulo) || vigente is null || !await repositorio.ExisteArticuloAsync(vigente.Version, articulo, ct))
        {
            return ServiceResult<VinculoServicioArticuloDto>.Error(400, "ARTICULO_NO_EXISTE",
                $"El artículo '{articulo}' no existe en la versión vigente del catálogo SAP.");
        }

        var actuales = await repositorio.ObtenerVinculosDeServicioAsync(tipoServicioId, ct);
        var anterior = actuales.FirstOrDefault();
        var anteriorDto = anterior is null ? null : Dto(servicio, anterior);

        // Un servicio tiene un solo artículo: el vínculo nuevo reemplaza a los anteriores.
        var vinculo = actuales.FirstOrDefault(v => v.ArticuloId == articulo);
        foreach (var otro in actuales.Where(v => v != vinculo))
        {
            repositorio.EliminarVinculo(otro);
        }
        if (vinculo is null)
        {
            vinculo = new ServicioArticuloSap { TipoServicioId = tipoServicioId, ArticuloId = articulo };
            repositorio.AgregarVinculo(vinculo);
        }
        vinculo.Origen = OrigenVinculo.MANUAL;
        vinculo.ConfirmadoPor = usuario.UsuarioAuditoria;
        vinculo.ConfirmadoEn = reloj.GetUtcNow();

        var nuevo = Dto(servicio, vinculo);
        auditoria.Registrar("servicio_articulo_sap", null, "FIJAR_VINCULO", anteriorDto, nuevo);
        await unidadTrabajo.GuardarAsync(ct);
        return ServiceResult<VinculoServicioArticuloDto>.Ok(nuevo);
    }

    private static ServiceResult<TipoCambioDto> ConflictoSap(TipoCambioDto request, FuenteTipoCambio fuente) =>
        ServiceResult<TipoCambioDto>.Error(409, TipoCambioDeSapExiste,
            $"El {request.Fecha:yyyy-MM-dd} ya tiene tipo de cambio {request.Moneda} de SAP (fuente {fuente}); gana el valor de SAP.");

    private static TipoCambioDto Dto(TipoCambio t) => new(t.Fecha, t.Moneda, t.Valor, t.Fuente);

    private static VinculoServicioArticuloDto Dto(TipoServicio s, ServicioArticuloSap? v) => new(
        s.TipoServicioId,
        s.Descripcion,
        v?.ArticuloId,
        v is null ? EstadoVinculo.SinVinculo
            : v.Origen == OrigenVinculo.MANUAL && v.ConfirmadoEn is not null ? EstadoVinculo.Confirmado : EstadoVinculo.Propuesto,
        v?.Origen,
        v?.ConfirmadoPor);
}
