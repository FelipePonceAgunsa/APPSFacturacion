using Microsoft.AspNetCore.Mvc;
using Satelite.Api.Handlers;
using Satelite.Aplicacion.Catalogo;
using Satelite.Dominio.Comun;

namespace Satelite.Api.Controllers;

/// <summary>Catálogo de servicios del Satélite (tarea 1.1).</summary>
[Route("api/tipos-servicio")]
[Tags("Catalogo")]
public class TiposServicioController(ICatalogoService catalogo) : SateliteControllerBase
{
    /// <summary>Servicios del catálogo normalizado, con su unidad de cobro.</summary>
    [HttpGet(Name = "TiposServicioListar")]
    [AccessAuthorize(AccessNames.Convenios, AccessNames.CatalogoSap, AccessNames.Operaciones)]
    [ProducesResponseType<List<TipoServicioDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Listar(CancellationToken ct) => Responder(await catalogo.ListarTiposServicioAsync(ct));
}

/// <summary>Catálogo SAP importado por archivo en la Fase A (tarea 1.1b, ADR-017). Pantalla T3.</summary>
[Route("api/catalogo")]
[Tags("Catalogo")]
public class CatalogoController(ICatalogoService catalogo, IImportacionCatalogoService importacion) : SateliteControllerBase
{
    /// <summary>Resumen del catálogo SAP vigente (versión, fecha y conteos).</summary>
    [HttpGet(Name = "CatalogoResumen")]
    [AccessAuthorize(AccessNames.CatalogoSap)]
    [ProducesResponseType<CatalogoResumenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Resumen(CancellationToken ct) => Responder(await catalogo.ObtenerResumenAsync(ct));

    /// <summary>Artículos de servicio del catálogo vigente (archivo A1).</summary>
    [HttpGet("articulos", Name = "CatalogoArticulos")]
    [AccessAuthorize(AccessNames.CatalogoSap)]
    [ProducesResponseType<List<ArticuloSapDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Articulos([FromQuery] string? q, CancellationToken ct) =>
        Responder(await catalogo.ListarArticulosAsync(q, ct));

    /// <summary>Precios de una lista de precios del catálogo vigente (archivo A2).</summary>
    [HttpGet("listas-precios/{codigoLista}/precios", Name = "CatalogoPrecios")]
    [AccessAuthorize(AccessNames.CatalogoSap)]
    [ProducesResponseType<List<PrecioListaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Precios(string codigoLista, CancellationToken ct) =>
        Responder(await catalogo.ListarPreciosAsync(codigoLista, ct));

    /// <summary>Kits con su composición (archivo A3). Referencia, no entrada del cálculo.</summary>
    [HttpGet("kits", Name = "CatalogoKits")]
    [AccessAuthorize(AccessNames.CatalogoSap)]
    [ProducesResponseType<List<KitSapDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> Kits([FromQuery] string? ruta, CancellationToken ct) =>
        Responder(await catalogo.ListarKitsAsync(ruta, ct));

    /// <summary>Tipo de cambio diario (archivo A4 o ingreso manual).</summary>
    [HttpGet("tipo-cambio", Name = "CatalogoTipoCambio")]
    [AccessAuthorize(AccessNames.CatalogoSap)]
    [ProducesResponseType<List<TipoCambioDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult> TipoCambio([FromQuery] DateOnly? desde, [FromQuery] Moneda? moneda, CancellationToken ct) =>
        Responder(await catalogo.ListarTiposCambioAsync(desde, moneda, ct));

    /// <summary>Ingresar el tipo de cambio de un día cuando falta el de SAP (fuente MANUAL).</summary>
    [HttpPost("tipo-cambio", Name = "CatalogoTipoCambioRegistrar")]
    [AccessAuthorize(AccessNames.CatalogoSap_Importar)]
    [ProducesResponseType<TipoCambioDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RegistrarTipoCambio([FromBody] TipoCambioDto request, CancellationToken ct) =>
        Responder(await catalogo.RegistrarTipoCambioManualAsync(request, ct));

    /// <summary>Subir los archivos del catálogo (A1 a A4). Valida y deja la importación pendiente de confirmar.</summary>
    [HttpPost("importaciones", Name = "CatalogoImportar")]
    [AccessAuthorize(AccessNames.CatalogoSap_Importar)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ResultadoImportacionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Importar(
        [FromForm] TipoImportacion tipo,
        IFormFile? archivo,
        IFormFile? articulos,
        IFormFile? listasPrecios,
        IFormFile? kits,
        IFormFile? tipoCambio,
        CancellationToken ct)
    {
        var partes = new Dictionary<string, Stream>();
        void Agregar(string nombre, IFormFile? f)
        {
            if (f is { Length: > 0 })
            {
                partes[nombre] = f.OpenReadStream();
            }
        }
        Agregar(ImportacionCatalogoService.ParteArchivo, archivo);
        Agregar(ImportacionCatalogoService.ParteArticulos, articulos);
        Agregar(ImportacionCatalogoService.ParteListasPrecios, listasPrecios);
        Agregar(ImportacionCatalogoService.ParteKits, kits);
        Agregar(ImportacionCatalogoService.ParteTipoCambio, tipoCambio);

        try
        {
            return Responder(await importacion.ImportarAsync(tipo, partes, ct));
        }
        finally
        {
            foreach (var parte in partes.Values)
            {
                await parte.DisposeAsync();
            }
        }
    }

    /// <summary>Confirmar una importación (humano): crea una nueva versión completa del catálogo.</summary>
    [HttpPost("importaciones/{importacionId:guid}/confirmar", Name = "CatalogoConfirmarImportacion")]
    [AccessAuthorize(AccessNames.CatalogoSap_Importar)]
    [ProducesResponseType<CatalogoResumenDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Confirmar(Guid importacionId, CancellationToken ct) =>
        Responder(await importacion.ConfirmarAsync(importacionId, ct));

    /// <summary>Vínculos servicio del Satélite ↔ artículo SAP (todos los servicios del catálogo).</summary>
    [HttpGet("vinculos", Name = "CatalogoVinculos")]
    [AccessAuthorize(AccessNames.CatalogoSap)]
    [ProducesResponseType<List<VinculoServicioArticuloDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> Vinculos([FromQuery] string? estado, CancellationToken ct) =>
        Responder(await catalogo.ListarVinculosAsync(estado, ct));

    /// <summary>Confirmar o cambiar el artículo SAP de un servicio.</summary>
    [HttpPut("vinculos/{tipoServicioId:int}", Name = "CatalogoFijarVinculo")]
    [AccessAuthorize(AccessNames.CatalogoSap_Importar)]
    [ProducesResponseType<VinculoServicioArticuloDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ErrorRespuesta>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> FijarVinculo(int tipoServicioId, [FromBody] VinculoRequest request, CancellationToken ct) =>
        Responder(await catalogo.FijarVinculoAsync(tipoServicioId, request, ct));
}
