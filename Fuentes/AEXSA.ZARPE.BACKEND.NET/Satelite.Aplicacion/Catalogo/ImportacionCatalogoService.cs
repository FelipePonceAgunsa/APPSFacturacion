using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Satelite.Aplicacion.Comun;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Aplicacion.Catalogo;

public interface IImportacionCatalogoService
{
    /// <summary>
    /// Valida los archivos y deja la importación PENDIENTE_CONFIRMACION (o RECHAZADA si hay errores).
    /// <paramref name="partes"/>: con un tipo parcial, la parte <c>archivo</c>; con COMPLETO, <c>articulos</c>,
    /// <c>listasPrecios</c>, <c>kits</c> y opcionalmente <c>tipoCambio</c>.
    /// </summary>
    Task<ServiceResult<ResultadoImportacionDto>> ImportarAsync(TipoImportacion tipo, IReadOnlyDictionary<string, Stream> partes, CancellationToken ct);

    /// <summary>Confirmación humana: crea la versión nueva y completa del catálogo (DBA-3b).</summary>
    Task<ServiceResult<CatalogoResumenDto>> ConfirmarAsync(Guid importacionId, CancellationToken ct);
}

/// <summary>Importación del catálogo SAP por archivo, Fase A (tarea 1.1b, ADR-017).</summary>
public sealed class ImportacionCatalogoService(
    ICatalogoRepositorio repositorio,
    IUnidadTrabajo unidadTrabajo,
    IAuditoria auditoria,
    IAlmacenArchivos almacen,
    ICurrentUser usuario,
    TimeProvider reloj,
    IOptions<CatalogoOptions> opciones,
    ICatalogoService catalogo) : IImportacionCatalogoService
{
    public const string ParteArchivo = "archivo";
    public const string ParteArticulos = "articulos";
    public const string ParteListasPrecios = "listasPrecios";
    public const string ParteKits = "kits";
    public const string ParteTipoCambio = "tipoCambio";

    public const string Duplicado = "DUPLICADO";
    public const string ArticuloNoExiste = "ARTICULO_NO_EXISTE";
    public const string PrimeraCargaSinArticulos = "PRIMERA_CARGA_SIN_ARTICULOS";
    public const string AdvertenciaArticuloInactivo = "ADVERTENCIA_ARTICULO_INACTIVO";
    public const string AdvertenciaArchivoRepetido = "ADVERTENCIA_ARCHIVO_REPETIDO";
    private const string PrefijoAdvertencia = "ADVERTENCIA_";

    public async Task<ServiceResult<ResultadoImportacionDto>> ImportarAsync(
        TipoImportacion tipo, IReadOnlyDictionary<string, Stream> partes, CancellationToken ct)
    {
        if (tipo == TipoImportacion.SOCIOS_NEGOCIO)
        {
            return ServiceResult<ResultadoImportacionDto>.Error(400, "TIPO_NO_HABILITADO",
                "La importación de socios de negocio (A5) no está habilitada en la etapa 1 (tarea 3.2).");
        }

        var nombres = PartesDe(tipo);
        var faltantes = nombres.Where(p => p.Obligatoria && !partes.ContainsKey(p.Nombre)).Select(p => p.Nombre).ToList();
        if (faltantes.Count > 0)
        {
            return ServiceResult<ResultadoImportacionDto>.Invalido(
                $"Faltan partes del formulario para {tipo}: {string.Join(", ", faltantes)}.");
        }

        // Se copia cada parte a memoria: se usa para el hash, la validación y el almacén.
        var contenido = new Dictionary<string, byte[]>();
        foreach (var parte in nombres.Where(p => partes.ContainsKey(p.Nombre)))
        {
            using var copia = new MemoryStream();
            await partes[parte.Nombre].CopyToAsync(copia, ct);
            contenido[parte.Nombre] = copia.ToArray();
        }

        var vigente = await repositorio.ObtenerVersionVigenteAsync(ct);
        var lectura = await LeerYValidarAsync(tipo, contenido, vigente?.Version, ct);

        var hash = Convert.ToHexStringLower(SHA256.HashData(contenido.OrderBy(p => p.Key, StringComparer.Ordinal).SelectMany(p => p.Value).ToArray()));
        if (await repositorio.ExisteImportacionConfirmadaConHashAsync(hash, ct))
        {
            lectura.Errores.Add(new ErrorImportacionDto(null, null, AdvertenciaArchivoRepetido,
                "Este mismo contenido ya se importó y confirmó antes."));
        }

        var importacion = new CatalogoImportacion
        {
            Id = Guid.CreateVersion7(),
            Origen = OrigenCatalogo.ARCHIVO,
            Tipo = tipo,
            HashContenido = hash,
            FilasLeidas = lectura.FilasLeidas,
            FilasValidas = lectura.FilasValidas,
            Estado = lectura.Errores.Any(EsError) ? EstadoImportacion.RECHAZADA : EstadoImportacion.PENDIENTE_CONFIRMACION,
            CargadoPor = usuario.UsuarioAuditoria,
            CargadoEn = reloj.GetUtcNow(),
            Errores = lectura.Errores.Select(e => new CatalogoImportacionError
            {
                Fila = e.Fila,
                Columna = e.Columna,
                Codigo = e.Codigo,
                Mensaje = e.Mensaje,
            }).ToList(),
        };

        if (importacion.Estado == EstadoImportacion.PENDIENTE_CONFIRMACION)
        {
            // Los archivos se guardan hasta la confirmación (S3 en AWS; carpeta local en desarrollo).
            foreach (var (nombre, bytes) in contenido)
            {
                await almacen.GuardarAsync($"{Carpeta(importacion.Id)}{nombre}.csv", new MemoryStream(bytes), ct);
            }
            importacion.ArchivoS3 = almacen.UriDe(Carpeta(importacion.Id));
        }
        else
        {
            importacion.ResueltoPor = usuario.UsuarioAuditoria;
            importacion.ResueltoEn = importacion.CargadoEn;
        }

        repositorio.AgregarImportacion(importacion);
        auditoria.Registrar("catalogo_importacion", importacion.Id, "IMPORTAR", null,
            new { importacion.Tipo, importacion.Estado, importacion.FilasLeidas, importacion.FilasValidas, importacion.HashContenido });
        await unidadTrabajo.GuardarAsync(ct);

        return ServiceResult<ResultadoImportacionDto>.Created(new ResultadoImportacionDto(
            importacion.Id, tipo, VersionBase(tipo, vigente?.Version), lectura.FilasLeidas, lectura.FilasValidas,
            lectura.Errores, importacion.Estado));
    }

    public async Task<ServiceResult<CatalogoResumenDto>> ConfirmarAsync(Guid importacionId, CancellationToken ct)
    {
        var importacion = await repositorio.ObtenerImportacionAsync(importacionId, ct);
        if (importacion is null)
        {
            return ServiceResult<CatalogoResumenDto>.NoEncontrado("La importación no existe.");
        }
        if (importacion.Estado != EstadoImportacion.PENDIENTE_CONFIRMACION)
        {
            return ServiceResult<CatalogoResumenDto>.Error(409, "IMPORTACION_NO_PENDIENTE",
                $"La importación está {importacion.Estado}; solo se confirma una PENDIENTE_CONFIRMACION.");
        }

        var contenido = new Dictionary<string, byte[]>();
        foreach (var parte in PartesDe(importacion.Tipo))
        {
            try
            {
                await using var archivo = await almacen.AbrirAsync($"{Carpeta(importacion.Id)}{parte.Nombre}.csv", ct);
                using var copia = new MemoryStream();
                await archivo.CopyToAsync(copia, ct);
                contenido[parte.Nombre] = copia.ToArray();
            }
            catch (FileNotFoundException) when (!parte.Obligatoria)
            {
                // Parte opcional que no vino (tipoCambio en COMPLETO).
            }
        }

        // Se valida otra vez contra la versión vigente de hoy: pudo cambiar desde la carga.
        var vigente = await repositorio.ObtenerVersionVigenteAsync(ct);
        var lectura = await LeerYValidarAsync(importacion.Tipo, contenido, vigente?.Version, ct);
        var errores = lectura.Errores.Where(EsError).ToList();
        if (errores.Count > 0)
        {
            return ServiceResult<CatalogoResumenDto>.Error(409, "IMPORTACION_DESACTUALIZADA",
                "La importación ya no es válida contra la versión vigente del catálogo. Vuelve a cargar los archivos.",
                string.Join(" | ", errores.Take(10).Select(e => e.Mensaje)));
        }

        var ahora = reloj.GetUtcNow();
        var version = (vigente?.Version ?? 0) + 1;
        // COMPLETO no copia nada: su base_version queda null.
        var baseVersion = importacion.Tipo == TipoImportacion.COMPLETO ? null : vigente?.Version;

        await using var transaccion = await unidadTrabajo.IniciarTransaccionAsync(ct);

        repositorio.AgregarSnapshot(new CatalogoSnapshot
        {
            Version = version,
            Fecha = ahora,
            Origen = OrigenCatalogo.ARCHIVO,
            ImportacionId = importacion.Id,
            BaseVersion = baseVersion,
            ConfirmadoPor = usuario.UsuarioAuditoria,
            ConfirmadoEn = ahora,
        });
        await unidadTrabajo.GuardarAsync(ct);

        // Cada versión queda completa: lo que vino se carga y lo que no vino se copia de la base (DBA-3b).
        if (lectura.Articulos is { } articulos)
        {
            repositorio.AgregarArticulos(articulos.Select(a => new SapArticulo
            {
                SnapshotVersion = version,
                Codigo = a.Codigo,
                Descripcion = a.Descripcion,
                UnidadMedida = a.UnidadMedida,
                Activo = a.Activo,
                Grupo = a.Grupo,
            }));
            await unidadTrabajo.GuardarAsync(ct);
        }
        else
        {
            await repositorio.CopiarArticulosAsync(baseVersion!.Value, version, ct);
        }

        if (lectura.Precios is { } precios)
        {
            var listas = precios.GroupBy(p => p.CodigoLista).Select(g => new SapListaPrecios
            {
                SnapshotVersion = version,
                Codigo = g.Key,
                Nombre = g.First().NombreLista,
                EsPublica = string.Equals(g.Key, opciones.Value.ListaPublica, StringComparison.Ordinal),
            }).ToList();
            var tarifas = precios.Select(p => new TarifaSnapshot
            {
                Id = Guid.CreateVersion7(),
                Version = version,
                Fecha = ahora,
                ArticuloId = p.CodigoArticulo,
                ListaPreciosId = p.CodigoLista,
                Precio = p.Precio,
                Moneda = p.Moneda,
                VigenteDesde = p.VigenteDesde,
                VigenteHasta = p.VigenteHasta,
            }).ToList();
            repositorio.AgregarListasPrecios(listas, tarifas);
            await unidadTrabajo.GuardarAsync(ct);
        }
        else if (baseVersion is not null)
        {
            await repositorio.CopiarListasPreciosAsync(baseVersion.Value, version, ct);
        }

        if (lectura.Kits is { } kits)
        {
            repositorio.AgregarKits(KitsDesde(kits, version));
            await unidadTrabajo.GuardarAsync(ct);
        }
        else if (baseVersion is not null)
        {
            await repositorio.CopiarKitsAsync(baseVersion.Value, version, ct);
        }

        if (lectura.TiposCambio is { Count: > 0 } tiposCambio)
        {
            await CargarTiposCambioAsync(tiposCambio, importacion.Id, ct);
        }

        importacion.Estado = EstadoImportacion.CONFIRMADA;
        importacion.ResueltoPor = usuario.UsuarioAuditoria;
        importacion.ResueltoEn = ahora;
        importacion.SnapshotVersion = version;
        auditoria.Registrar("catalogo_importacion", importacion.Id, "CONFIRMAR",
            new { Estado = EstadoImportacion.PENDIENTE_CONFIRMACION },
            new { importacion.Estado, importacion.Tipo, Version = version, BaseVersion = baseVersion });
        await unidadTrabajo.GuardarAsync(ct);
        await transaccion.ConfirmarAsync(ct);

        return await catalogo.ObtenerResumenAsync(ct);
    }

    /// <summary>A4: gana el valor de SAP. Un valor MANUAL del mismo día se reemplaza y queda en la auditoría.</summary>
    private async Task CargarTiposCambioAsync(List<FilaTipoCambio> filas, Guid importacionId, CancellationToken ct)
    {
        var existentes = (await repositorio.ObtenerTiposCambioAsync(filas.Min(f => f.Fecha), filas.Max(f => f.Fecha), ct))
            .ToDictionary(t => (t.Fecha, t.Moneda));
        foreach (var fila in filas)
        {
            if (existentes.TryGetValue((fila.Fecha, fila.Moneda), out var actual))
            {
                if (actual.Fuente != FuenteTipoCambio.ARCHIVO || actual.Valor != fila.Valor)
                {
                    auditoria.Registrar("tipo_cambio", null,
                        actual.Fuente == FuenteTipoCambio.MANUAL ? "REEMPLAZO_MANUAL_POR_SAP" : "ACTUALIZAR",
                        new { actual.Fecha, actual.Moneda, actual.Valor, actual.Fuente },
                        new { fila.Fecha, fila.Moneda, fila.Valor, Fuente = FuenteTipoCambio.ARCHIVO, ImportacionId = importacionId });
                }
                actual.Valor = fila.Valor;
                actual.Fuente = FuenteTipoCambio.ARCHIVO;
                actual.ImportacionId = importacionId;
                actual.CargadoPor = usuario.UsuarioAuditoria;
            }
            else
            {
                repositorio.AgregarTipoCambio(new TipoCambio
                {
                    Fecha = fila.Fecha,
                    Moneda = fila.Moneda,
                    Valor = fila.Valor,
                    Fuente = FuenteTipoCambio.ARCHIVO,
                    ImportacionId = importacionId,
                    CargadoPor = usuario.UsuarioAuditoria,
                });
            }
        }
        await unidadTrabajo.GuardarAsync(ct);
    }

    /// <summary>
    /// Kits del archivo A3. [SUPUESTO: una fila con activo = N no entra a la composición del kit, y un kit sin filas
    /// activas no se carga; sap_kit no tiene columna activo]
    /// </summary>
    private static List<SapKit> KitsDesde(List<FilaKit> filas, int version) =>
        filas.Where(f => f.Activo).GroupBy(f => f.CodigoKit).Select(g =>
        {
            var id = Guid.CreateVersion7();
            return new SapKit
            {
                Id = id,
                SnapshotVersion = version,
                KitCodigo = g.Key,
                Nombre = g.First().NombreKit,
                Ruta = g.First().Ruta,
                Articulos = g.Select(f => new SapKitArticulo
                {
                    KitId = id,
                    SnapshotVersion = version,
                    ArticuloId = f.CodigoArticulo,
                    Cantidad = f.Cantidad,
                }).ToList(),
            };
        }).ToList();

    private sealed class Lectura
    {
        public List<FilaArticulo>? Articulos { get; set; }
        public List<FilaPrecio>? Precios { get; set; }
        public List<FilaKit>? Kits { get; set; }
        public List<FilaTipoCambio>? TiposCambio { get; set; }
        public int FilasLeidas { get; set; }
        public int FilasValidas { get; set; }
        public List<ErrorImportacionDto> Errores { get; } = [];
    }

    private async Task<Lectura> LeerYValidarAsync(
        TipoImportacion tipo, IReadOnlyDictionary<string, byte[]> contenido, int? versionVigente, CancellationToken ct)
    {
        var r = new Lectura();
        byte[]? Parte(string completo) => contenido.GetValueOrDefault(tipo == TipoImportacion.COMPLETO ? completo : ParteArchivo);

        if ((tipo is TipoImportacion.COMPLETO or TipoImportacion.ARTICULOS) && Parte(ParteArticulos) is { } a1)
        {
            r.Articulos = Agregar(r, CsvCatalogoParser.LeerArticulos(new MemoryStream(a1)));
        }
        if ((tipo is TipoImportacion.COMPLETO or TipoImportacion.LISTAS_PRECIOS) && Parte(ParteListasPrecios) is { } a2)
        {
            r.Precios = Agregar(r, CsvCatalogoParser.LeerPrecios(new MemoryStream(a2)));
        }
        if ((tipo is TipoImportacion.COMPLETO or TipoImportacion.KITS) && Parte(ParteKits) is { } a3)
        {
            r.Kits = Agregar(r, CsvCatalogoParser.LeerKits(new MemoryStream(a3)));
        }
        if ((tipo is TipoImportacion.COMPLETO or TipoImportacion.TIPO_CAMBIO) && Parte(ParteTipoCambio) is { } a4)
        {
            r.TiposCambio = Agregar(r, CsvCatalogoParser.LeerTiposCambio(new MemoryStream(a4)));
        }

        if (versionVigente is null && r.Articulos is null)
        {
            r.Errores.Add(new ErrorImportacionDto(null, null, PrimeraCargaSinArticulos,
                "La primera carga del catálogo debe ser COMPLETO o ARTICULOS (archivo A1)."));
            return r;
        }

        // Duplicados por clave (formato-archivos-catalogo-sap.md).
        var invalidas = new HashSet<(string, int)>();
        Duplicados(r, r.Articulos, "A1", "codigo_articulo", a => a.Codigo, a => a.Fila, invalidas);
        Duplicados(r, r.Precios, "A2", "codigo_articulo", p => (p.CodigoLista, p.CodigoArticulo, p.VigenteDesde), p => p.Fila, invalidas);
        Duplicados(r, r.Kits, "A3", "codigo_articulo", k => (k.CodigoKit, k.CodigoArticulo), k => k.Fila, invalidas);
        Duplicados(r, r.TiposCambio, "A4", "fecha", t => (t.Fecha, t.Moneda), t => t.Fila, invalidas);

        // Referencias: todo artículo de A2 y A3 existe en A1 de este envío o, si A1 no vino, en la versión vigente.
        var activos = r.Articulos is not null
            ? r.Articulos.GroupBy(a => a.Codigo).ToDictionary(g => g.Key, g => g.First().Activo)
            : await repositorio.ActivoPorArticuloAsync(versionVigente!.Value, ct);
        var origen = r.Articulos is not null ? "A1" : $"la versión vigente {versionVigente}";
        foreach (var (archivo, fila, articulo) in (r.Precios ?? []).Select(p => ("A2", p.Fila, p.CodigoArticulo))
                     .Concat((r.Kits ?? []).Select(k => ("A3", k.Fila, k.CodigoArticulo))))
        {
            if (!activos.TryGetValue(articulo, out var activo))
            {
                r.Errores.Add(new ErrorImportacionDto(fila, "codigo_articulo", ArticuloNoExiste,
                    $"[{archivo}] El artículo {articulo} no existe en {origen}."));
                invalidas.Add((archivo, fila));
            }
            else if (!activo)
            {
                r.Errores.Add(new ErrorImportacionDto(fila, "codigo_articulo", AdvertenciaArticuloInactivo,
                    $"[{archivo}] El artículo {articulo} está inactivo."));
            }
        }

        // ARTICULOS parcial: las listas y kits que se copian de la vigente no pueden quedar sin su artículo.
        if (tipo == TipoImportacion.ARTICULOS && versionVigente is not null && r.Articulos is not null)
        {
            var faltan = (await repositorio.ArticulosReferenciadosAsync(versionVigente.Value, ct))
                .Where(c => !activos.ContainsKey(c)).Order(StringComparer.Ordinal).ToList();
            if (faltan.Count > 0)
            {
                r.Errores.Add(new ErrorImportacionDto(null, "codigo_articulo", ArticuloNoExiste,
                    $"[A1] Faltan artículos que usan las listas de precios o los kits de la versión vigente {versionVigente}: {string.Join(", ", faltan)}."));
            }
        }

        r.FilasValidas -= invalidas.Count;
        return r;
    }

    private static List<T> Agregar<T>(Lectura r, LecturaArchivo<T> lectura)
    {
        r.FilasLeidas += lectura.FilasLeidas;
        r.FilasValidas += lectura.Filas.Count;
        r.Errores.AddRange(lectura.Errores);
        return lectura.Filas;
    }

    private static void Duplicados<T, TClave>(
        Lectura r, List<T>? filas, string archivo, string columna, Func<T, TClave> clave, Func<T, int> fila, HashSet<(string, int)> invalidas)
        where TClave : notnull
    {
        foreach (var grupo in (filas ?? []).GroupBy(clave).Where(g => g.Count() > 1))
        {
            var lineas = grupo.Select(fila).ToList();
            foreach (var linea in lineas.Skip(1))
            {
                r.Errores.Add(new ErrorImportacionDto(linea, columna, Duplicado,
                    $"[{archivo}] Clave duplicada {grupo.Key} (también en la fila {lineas[0]})."));
                invalidas.Add((archivo, linea));
            }
        }
    }

    private static bool EsError(ErrorImportacionDto e) => !e.Codigo.StartsWith(PrefijoAdvertencia, StringComparison.Ordinal);

    private static string Carpeta(Guid importacionId) => $"importaciones/{importacionId}/";

    private static int? VersionBase(TipoImportacion tipo, int? vigente) => tipo == TipoImportacion.COMPLETO ? null : vigente;

    private static IReadOnlyList<(string Nombre, bool Obligatoria)> PartesDe(TipoImportacion tipo) => tipo switch
    {
        TipoImportacion.COMPLETO =>
        [
            (ParteArticulos, true), (ParteListasPrecios, true), (ParteKits, true), (ParteTipoCambio, false),
        ],
        _ => [(ParteArchivo, true)],
    };
}
