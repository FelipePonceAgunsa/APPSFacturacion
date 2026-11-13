using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Satelite.Api.Controllers;
using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Configuracion;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;
using Satelite.Tests.Infraestructura;

namespace Satelite.Tests.Api;

/// <summary>Tareas 1.1 y 1.1b: catálogo de servicios, importación del catálogo SAP, tipo de cambio, vínculos y mapeos.</summary>
[Collection(PostgresCollection.Nombre)]
public class CatalogoTests(PostgresFixture fixture) : ApiConBaseNueva(fixture)
{
    private const string A1 = """
        codigo_articulo;descripcion;unidad_medida;activo;grupo_articulo
        SRV-REC-001;Recepción contenedor;CTR;S;SERVICIOS
        SRV-DES-001;Despacho contenedor;CTR;S;SERVICIOS
        SRV-VIS-001;Visación documental;BL;S;SERVICIOS
        SRV-OLD-001;Servicio antiguo;CTR;N;
        """;

    private const string A2 = """
        codigo_lista;nombre_lista;codigo_articulo;precio;moneda;vigente_desde;vigente_hasta
        LP-PUBLICA;Lista pública;SRV-REC-001;17000.00;CLP;2026-01-01;
        LP-PUBLICA;Lista pública;SRV-VIS-001;23.00;USD;2026-01-01;
        LP-CONV;Lista convenio;SRV-DES-001;1.5;UF;2026-01-01;2026-12-31
        """;

    private const string A3 = """
        codigo_kit;nombre_kit;codigo_articulo;cantidad;ruta;activo
        SPOT-DIRECTO;SPOT contenedor directo;SRV-VIS-001;1;Directo sin convenio;S
        SPOT-DIRECTO;SPOT contenedor directo;SRV-REC-001;1;Directo sin convenio;S
        SPOT-INDIRECTO;SPOT contenedor indirecto;SRV-DES-001;2;Indirecto sin convenio;S
        """;

    private const string A4 = """
        fecha;moneda;valor
        2026-11-01;USD;812.40
        2026-11-02;USD;700.00
        2026-11-01;UF;39000.50
        """;

    [Fact]
    public async Task Completo_valido_y_confirmado_crea_la_version_1()
    {
        var resultado = await ImportarAsync(TipoImportacion.COMPLETO, ("articulos", A1), ("listasPrecios", A2), ("kits", A3), ("tipoCambio", A4));

        Assert.Equal(EstadoImportacion.PENDIENTE_CONFIRMACION, resultado.Estado);
        Assert.Null(resultado.VersionBase);
        Assert.Equal(13, resultado.FilasLeidas);
        Assert.Equal(13, resultado.FilasValidas);
        Assert.DoesNotContain(resultado.Errores, e => !e.Codigo.StartsWith("ADVERTENCIA_"));

        var resumen = await ConfirmarAsync(resultado.ImportacionId);
        Assert.Equal(1, resumen.Version);
        Assert.Equal(4, resumen.Articulos);
        Assert.Equal(2, resumen.ListasPrecios);
        Assert.Equal(2, resumen.Kits);
        Assert.Equal(new DateOnly(2026, 11, 2), resumen.TipoCambioAl);

        var precios = await LeerAsync<List<PrecioListaDto>>(await Cliente.GetAsync("/api/catalogo/listas-precios/LP-PUBLICA/precios", Ct));
        Assert.Equal(2, precios.Count);
        Assert.Contains(precios, p => p.CodigoArticulo == "SRV-VIS-001" && p.Precio == 23.00m && p.Moneda == Moneda.USD);

        var kits = await LeerAsync<List<KitSapDto>>(await Cliente.GetAsync("/api/catalogo/kits?ruta=Directo%20sin%20convenio", Ct));
        var kit = Assert.Single(kits);
        Assert.Equal(["SRV-REC-001", "SRV-VIS-001"], kit.Articulos.Select(a => a.CodigoArticulo));

        var articulos = await LeerAsync<List<ArticuloSapDto>>(await Cliente.GetAsync("/api/catalogo/articulos?q=visaci", Ct));
        Assert.Equal("SRV-VIS-001", Assert.Single(articulos).CodigoArticulo);

        await using var db = Api.CrearContexto();
        Assert.True((await db.SapListasPrecios.SingleAsync(l => l.Codigo == "LP-PUBLICA", Ct)).EsPublica);
        Assert.False((await db.SapListasPrecios.SingleAsync(l => l.Codigo == "LP-CONV", Ct)).EsPublica);
        var tc = await db.TiposCambio.SingleAsync(t => t.Fecha == new DateOnly(2026, 11, 2) && t.Moneda == Moneda.USD, Ct);
        Assert.Equal(FuenteTipoCambio.ARCHIVO, tc.Fuente);
        Assert.Equal(resultado.ImportacionId, tc.ImportacionId);
        Assert.Equal(EstadoImportacion.CONFIRMADA, (await db.CatalogoImportaciones.SingleAsync(Ct)).Estado);
        Assert.True(await db.EventosAuditoria.AnyAsync(a => a.Entidad == "catalogo_importacion" && a.Accion == "CONFIRMAR"
                                                             && a.Usuario == "admin.local@zarpe.local", Ct));
    }

    [Fact]
    public async Task Solo_listas_sobre_la_version_1_crea_la_version_2_copiando_articulos_y_kits()
    {
        await CargarVersion1Async();

        var resultado = await ImportarAsync(TipoImportacion.LISTAS_PRECIOS, ("archivo", """
            codigo_lista;nombre_lista;codigo_articulo;precio;moneda;vigente_desde;vigente_hasta
            LP-PUBLICA;Lista pública;SRV-REC-001;18000;CLP;2026-11-01;
            """));
        Assert.Equal(EstadoImportacion.PENDIENTE_CONFIRMACION, resultado.Estado);
        Assert.Equal(1, resultado.VersionBase);

        var resumen = await ConfirmarAsync(resultado.ImportacionId);
        Assert.Equal(2, resumen.Version);
        Assert.Equal(4, resumen.Articulos);
        Assert.Equal(1, resumen.ListasPrecios);
        Assert.Equal(2, resumen.Kits);

        await using var db = Api.CrearContexto();
        var snapshot = await db.CatalogoSnapshots.SingleAsync(s => s.Version == 2, Ct);
        Assert.Equal(1, snapshot.BaseVersion);
        Assert.Equal(3, await db.SapKitArticulos.CountAsync(k => k.SnapshotVersion == 2, Ct));
        Assert.Equal(18000m, (await db.TarifasSnapshot.SingleAsync(t => t.Version == 2, Ct)).Precio);
        // La versión 1 no cambia.
        Assert.Equal(3, await db.TarifasSnapshot.CountAsync(t => t.Version == 1, Ct));
    }

    [Fact]
    public async Task Lista_con_un_articulo_que_no_existe_queda_rechazada()
    {
        await CargarVersion1Async();

        var resultado = await ImportarAsync(TipoImportacion.LISTAS_PRECIOS, ("archivo", """
            codigo_lista;nombre_lista;codigo_articulo;precio;moneda;vigente_desde;vigente_hasta
            LP-PUBLICA;Lista pública;SRV-NOEXISTE-001;100;CLP;2026-11-01;
            LP-PUBLICA;Lista pública;SRV-OLD-001;100;CLP;2026-11-01;
            """));

        Assert.Equal(EstadoImportacion.RECHAZADA, resultado.Estado);
        var error = Assert.Single(resultado.Errores, e => e.Codigo == ImportacionCatalogoService.ArticuloNoExiste);
        Assert.Equal(2, error.Fila);
        Assert.Contains(resultado.Errores, e => e.Codigo == ImportacionCatalogoService.AdvertenciaArticuloInactivo && e.Fila == 3);
        Assert.Equal(1, resultado.FilasValidas);

        var confirmar = await Cliente.PostAsync($"/api/catalogo/importaciones/{resultado.ImportacionId}/confirmar", null, Ct);
        Assert.Equal(HttpStatusCode.Conflict, confirmar.StatusCode);
    }

    [Fact]
    public async Task Solo_articulos_que_dejan_una_lista_sin_su_articulo_queda_rechazada()
    {
        await CargarVersion1Async();

        var resultado = await ImportarAsync(TipoImportacion.ARTICULOS, ("archivo", """
            codigo_articulo;descripcion;unidad_medida;activo;grupo_articulo
            SRV-REC-001;Recepción contenedor;CTR;S;SERVICIOS
            """));

        Assert.Equal(EstadoImportacion.RECHAZADA, resultado.Estado);
        var error = Assert.Single(resultado.Errores, e => e.Codigo == ImportacionCatalogoService.ArticuloNoExiste);
        Assert.Contains("SRV-DES-001", error.Mensaje);
        Assert.Contains("SRV-VIS-001", error.Mensaje);
    }

    [Fact]
    public async Task La_primera_importacion_sin_articulos_queda_rechazada()
    {
        var resultado = await ImportarAsync(TipoImportacion.LISTAS_PRECIOS, ("archivo", A2));

        Assert.Equal(EstadoImportacion.RECHAZADA, resultado.Estado);
        Assert.Contains(resultado.Errores, e => e.Codigo == ImportacionCatalogoService.PrimeraCargaSinArticulos);
        Assert.Equal(HttpStatusCode.NotFound, (await Cliente.GetAsync("/api/catalogo", Ct)).StatusCode);
    }

    [Fact]
    public async Task Errores_de_formato_duplicado_y_moneda_quedan_con_fila_y_codigo()
    {
        var resultado = await ImportarAsync(TipoImportacion.COMPLETO,
            ("articulos", A1 + "\nSRV-REC-001;Duplicado;CTR;S;SERVICIOS\nSRV-MAL-001;Sin activo;CTR;X;"),
            ("listasPrecios", A2 + "\nLP-PUBLICA;Lista pública;SRV-DES-001;10,5;EUR;2026-13-01;"),
            ("kits", A3));

        Assert.Equal(EstadoImportacion.RECHAZADA, resultado.Estado);
        Assert.Contains(resultado.Errores, e => e.Codigo == ImportacionCatalogoService.Duplicado && e.Fila == 6);
        Assert.Contains(resultado.Errores, e => e.Codigo == CsvCatalogoParser.Formato && e.Fila == 7 && e.Columna == "activo");
        Assert.Contains(resultado.Errores, e => e.Codigo == CsvCatalogoParser.Formato && e.Columna == "precio");
        Assert.Contains(resultado.Errores, e => e.Codigo == CsvCatalogoParser.MonedaInvalida && e.Columna == "moneda");
        Assert.Contains(resultado.Errores, e => e.Codigo == CsvCatalogoParser.Formato && e.Columna == "vigente_desde");
    }

    [Fact]
    public async Task Socios_de_negocio_no_esta_habilitado()
    {
        var respuesta = await PostImportacionAsync(TipoImportacion.SOCIOS_NEGOCIO, ("archivo", "rut;card_code\n"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Equal("TIPO_NO_HABILITADO", (await LeerAsync<ErrorRespuesta>(respuesta)).Codigo);
    }

    [Fact]
    public async Task Tipo_de_cambio_manual_sobre_uno_de_SAP_responde_409_y_SAP_reemplaza_al_manual()
    {
        var manual = await PostJsonAsync("/api/catalogo/tipo-cambio", new { fecha = "2026-11-02", moneda = "USD", valor = 799.5m });
        Assert.Equal(HttpStatusCode.Created, manual.StatusCode);
        Assert.Equal(FuenteTipoCambio.MANUAL, (await LeerAsync<TipoCambioDto>(manual)).Fuente);

        await CargarVersion1Async();

        var tipos = await LeerAsync<List<TipoCambioDto>>(await Cliente.GetAsync("/api/catalogo/tipo-cambio?moneda=USD&desde=2026-11-02", Ct));
        var reemplazado = Assert.Single(tipos);
        Assert.Equal(700.00m, reemplazado.Valor);
        Assert.Equal(FuenteTipoCambio.ARCHIVO, reemplazado.Fuente);

        var conflicto = await PostJsonAsync("/api/catalogo/tipo-cambio", new { fecha = "2026-11-02", moneda = "USD", valor = 801m });
        Assert.Equal(HttpStatusCode.Conflict, conflicto.StatusCode);
        Assert.Equal(CatalogoService.TipoCambioDeSapExiste, (await LeerAsync<ErrorRespuesta>(conflicto)).Codigo);

        await using var db = Api.CrearContexto();
        Assert.True(await db.EventosAuditoria.AnyAsync(a => a.Entidad == "tipo_cambio" && a.Accion == "REEMPLAZO_MANUAL_POR_SAP", Ct));
    }

    [Fact]
    public async Task El_vinculo_lo_fija_una_persona_con_un_articulo_de_la_version_vigente()
    {
        await CargarVersion1Async();

        var invalido = await PutJsonAsync("/api/catalogo/vinculos/42", new { articuloSap = "SRV-NOEXISTE" });
        Assert.Equal(HttpStatusCode.BadRequest, invalido.StatusCode);

        var respuesta = await PutJsonAsync("/api/catalogo/vinculos/42", new { articuloSap = "SRV-DES-001" });
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        respuesta = await PutJsonAsync("/api/catalogo/vinculos/42", new { articuloSap = "SRV-REC-001" });
        var vinculo = await LeerAsync<VinculoServicioArticuloDto>(respuesta);
        Assert.Equal("SRV-REC-001", vinculo.ArticuloSap);
        Assert.Equal(EstadoVinculo.Confirmado, vinculo.Estado);
        Assert.Equal("admin.local@zarpe.local", vinculo.ConfirmadoPor);

        var sinVinculo = await LeerAsync<List<VinculoServicioArticuloDto>>(await Cliente.GetAsync("/api/catalogo/vinculos?estado=SIN_VINCULO", Ct));
        Assert.DoesNotContain(sinVinculo, v => v.TipoServicioId == 42);
        Assert.Contains(sinVinculo, v => v.TipoServicioId == 43);

        await using var db = Api.CrearContexto();
        Assert.Equal("SRV-REC-001", (await db.ServiciosArticuloSap.SingleAsync(v => v.TipoServicioId == 42, Ct)).ArticuloId);
    }

    [Fact]
    public async Task Tipos_de_servicio_traen_su_unidad()
    {
        var tipos = await LeerAsync<List<TipoServicioDto>>(await Cliente.GetAsync("/api/tipos-servicio", Ct));

        Assert.Equal(UnidadCobro.CTR_DIA, tipos.Single(t => t.TipoServicioId == 48).Unidad);
    }

    [Fact]
    public async Task El_administrador_mapea_terminales_y_tipos_de_inspeccion()
    {
        await using (var db = Api.CrearContexto())
        {
            db.TerminalesMapa.Add(new TerminalMapa { TerminalAexsaId = 3, Nombre = "TERMINAL NUEVO" });
            db.TiposInspeccionMapa.Add(new TipoInspeccionMapa { CodigoAexsa = "NUEVO", Descripcion = "Inspección nueva" });
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(HttpStatusCode.BadRequest, (await PutJsonAsync("/api/configuracion/terminales/3", new { grupo = "SIN_MAPEAR" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PutJsonAsync("/api/configuracion/terminales/99", new { grupo = "STI" })).StatusCode);
        var terminal = await LeerAsync<TerminalMapaDto>(await PutJsonAsync("/api/configuracion/terminales/3", new { grupo = "DP_WORLD" }));
        Assert.Equal(GrupoTerminal.DP_WORLD, terminal.Grupo);

        var tipo = await LeerAsync<TipoInspeccionMapaDto>(await PutJsonAsync("/api/configuracion/tipos-inspeccion/NUEVO", new { grupo = "AFORO_ADUANA" }));
        Assert.Equal(GrupoInspeccion.AFORO_ADUANA, tipo.Grupo);

        var tipos = await LeerAsync<List<TipoInspeccionMapaDto>>(await Cliente.GetAsync("/api/configuracion/tipos-inspeccion", Ct));
        Assert.Equal(0, Assert.Single(tipos).OperacionesPendientes);

        await using var verificacion = Api.CrearContexto();
        var guardado = await verificacion.TerminalesMapa.SingleAsync(t => t.TerminalAexsaId == 3, Ct);
        Assert.Equal("admin.local@zarpe.local", guardado.ActualizadoPor);
    }

    private async Task CargarVersion1Async()
    {
        var resultado = await ImportarAsync(TipoImportacion.COMPLETO, ("articulos", A1), ("listasPrecios", A2), ("kits", A3), ("tipoCambio", A4));
        Assert.Equal(1, (await ConfirmarAsync(resultado.ImportacionId)).Version);
    }

    private async Task<ResultadoImportacionDto> ImportarAsync(TipoImportacion tipo, params (string Parte, string Csv)[] partes)
    {
        var respuesta = await PostImportacionAsync(tipo, partes);
        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        return await LeerAsync<ResultadoImportacionDto>(respuesta);
    }

    private Task<HttpResponseMessage> PostImportacionAsync(TipoImportacion tipo, params (string Parte, string Csv)[] partes)
    {
        var formulario = new MultipartFormDataContent { { new StringContent(tipo.ToString()), "tipo" } };
        foreach (var (parte, csv) in partes)
        {
            var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
            archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            formulario.Add(archivo, parte, $"{parte}.csv");
        }
        return Cliente.PostAsync("/api/catalogo/importaciones", formulario, Ct);
    }

    private async Task<CatalogoResumenDto> ConfirmarAsync(Guid importacionId)
    {
        var respuesta = await Cliente.PostAsync($"/api/catalogo/importaciones/{importacionId}/confirmar", null, Ct);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return await LeerAsync<CatalogoResumenDto>(respuesta);
    }
}
