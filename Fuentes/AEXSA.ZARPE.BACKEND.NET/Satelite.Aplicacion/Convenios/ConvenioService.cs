using Satelite.Aplicacion.Catalogo;
using Satelite.Aplicacion.Comun;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Aplicacion.Convenios;

public interface IConvenioService
{
    Task<ServiceResult<List<ConvenioResumenDto>>> ListarAsync(string? rut, EstadoVersionConvenio? estado, CancellationToken ct);
    Task<ServiceResult<ConvenioDetalleDto>> ObtenerAsync(Guid convenioId, CancellationToken ct);
    Task<ServiceResult<VersionConvenioVigenteDto>> ObtenerVigenteAsync(string rut, DateOnly fecha, CancellationToken ct);
    Task<ServiceResult<VersionConvenioDto>> CrearAsync(ConvenioManualRequest request, CancellationToken ct);
    Task<ServiceResult<VersionConvenioDto>> CrearVersionAsync(Guid convenioId, NuevaVersionConvenioRequest request, CancellationToken ct);
    Task<ServiceResult<VersionConvenioDto>> ConfirmarVersionAsync(Guid convenioId, Guid versionId, CancellationToken ct);

    /// <summary>
    /// Versión VIGENTE en vigor para un RUT a una fecha, o null. Con varios convenios elige el más antiguo (RN-018).
    /// La usa el cálculo de la OFA (bloques 4 y 5).
    /// </summary>
    Task<ConvenioVigente?> BuscarVigenteAsync(string rut, DateOnly fecha, CancellationToken ct);
}

/// <summary>
/// Convenios versionados por formulario manual (tarea 1.2, ADR-007/012). Toda versión nace en BORRADOR y una persona
/// la confirma; al confirmarla la VIGENTE anterior pasa a REEMPLAZADA. Ningún endpoint edita una versión VIGENTE.
/// </summary>
public sealed class ConvenioService(
    IConveniosRepositorio repositorio,
    ICatalogoRepositorio catalogo,
    IUnidadTrabajo unidadTrabajo,
    IAuditoria auditoria,
    ICurrentUser usuario,
    TimeProvider reloj) : IConvenioService
{
    public const string VersionNoEditable = "VERSION_NO_EDITABLE";

    public async Task<ServiceResult<List<ConvenioResumenDto>>> ListarAsync(string? rut, EstadoVersionConvenio? estado, CancellationToken ct)
    {
        string? rutNormalizado = null;
        if (!string.IsNullOrWhiteSpace(rut))
        {
            rutNormalizado = Rut.Normalizar(rut);
            if (rutNormalizado is null)
            {
                return ServiceResult<List<ConvenioResumenDto>>.Invalido($"RUT inválido: {rut}");
            }
        }

        var convenios = await repositorio.ListarAsync(rutNormalizado, estado, ct);
        return ServiceResult<List<ConvenioResumenDto>>.Ok(convenios.Select(c =>
        {
            var vigente = c.Versiones.Where(v => v.Estado == EstadoVersionConvenio.VIGENTE).MaxBy(v => v.Numero);
            var ultima = c.Versiones.MaxBy(v => v.Numero)!;
            return new ConvenioResumenDto(c.Id, c.Cliente!.Rut, c.Cliente.RazonSocial, c.Comercial, vigente?.Numero,
                vigente?.VigenciaDesde, vigente?.VigenciaHasta, ultima.Numero, ultima.Estado);
        }).ToList());
    }

    public async Task<ServiceResult<ConvenioDetalleDto>> ObtenerAsync(Guid convenioId, CancellationToken ct)
    {
        var convenio = await repositorio.ObtenerAsync(convenioId, ct);
        if (convenio is null)
        {
            return ServiceResult<ConvenioDetalleDto>.NoEncontrado("El convenio no existe.");
        }

        var servicios = await ServiciosPorIdAsync(ct);
        return ServiceResult<ConvenioDetalleDto>.Ok(new ConvenioDetalleDto(
            convenio.Id, convenio.Cliente!.Rut, convenio.Cliente.RazonSocial, convenio.Comercial, convenio.CreadoEn,
            convenio.Versiones.OrderByDescending(v => v.Numero).Select(v => Dto(v, servicios)).ToList()));
    }

    public async Task<ServiceResult<VersionConvenioVigenteDto>> ObtenerVigenteAsync(string rut, DateOnly fecha, CancellationToken ct)
    {
        if (Rut.Normalizar(rut) is null)
        {
            return ServiceResult<VersionConvenioVigenteDto>.Invalido($"RUT inválido: {rut}");
        }

        var vigente = await BuscarVigenteAsync(rut, fecha, ct);
        if (vigente is null)
        {
            return ServiceResult<VersionConvenioVigenteDto>.NoEncontrado($"El RUT {rut} no tiene un convenio vigente al {fecha:yyyy-MM-dd}.");
        }

        var servicios = await ServiciosPorIdAsync(ct);
        return ServiceResult<VersionConvenioVigenteDto>.Ok(new VersionConvenioVigenteDto(
            Dto(vigente.Version, servicios), vigente.Candidatos.Select(v => v.Id).ToList()));
    }

    public async Task<ConvenioVigente?> BuscarVigenteAsync(string rut, DateOnly fecha, CancellationToken ct)
    {
        var normalizado = Rut.Normalizar(rut);
        if (normalizado is null)
        {
            return null;
        }

        var candidatos = await repositorio.ListarVersionesEnVigorAsync(normalizado, fecha, ct);
        if (candidatos.Count == 0)
        {
            return null;
        }

        // RN-018: con varios convenios vigentes se usa el más antiguo.
        // [SUPUESTO: "más antiguo" = el convenio creado primero (convenio.creado_en); a igual fecha, el de menor id]
        var elegida = candidatos
            .OrderBy(v => v.Convenio!.CreadoEn)
            .ThenBy(v => v.ConvenioId)
            .ThenByDescending(v => v.Numero)
            .First();
        return new ConvenioVigente(elegida, candidatos);
    }

    public async Task<ServiceResult<VersionConvenioDto>> CrearAsync(ConvenioManualRequest request, CancellationToken ct)
    {
        var rut = Rut.Normalizar(request.Rut);
        if (rut is null)
        {
            return ServiceResult<VersionConvenioDto>.Invalido($"RUT inválido: {request.Rut}");
        }

        var servicios = await ServiciosPorIdAsync(ct);
        var error = Validar(request.VigenciaDesde, request.VigenciaHasta, request.TcMinimo, request.DiasLibres, request.Servicios, servicios);
        if (error is not null)
        {
            return ServiceResult<VersionConvenioDto>.Invalido(error);
        }

        var ahora = reloj.GetUtcNow();
        var cliente = await repositorio.ObtenerClientePorRutAsync(rut, ct);
        if (cliente is null)
        {
            // Si no viene la razón social de un cliente nuevo, se usa el RUT (la columna es obligatoria).
            cliente = new Cliente
            {
                Id = Guid.CreateVersion7(),
                Rut = rut,
                RazonSocial = string.IsNullOrWhiteSpace(request.RazonSocial) ? rut : request.RazonSocial.Trim(),
                CreadoEn = ahora,
            };
            repositorio.AgregarCliente(cliente);
        }

        var convenio = new Convenio
        {
            Id = Guid.CreateVersion7(),
            ClienteId = cliente.Id,
            Cliente = cliente,
            Comercial = string.IsNullOrWhiteSpace(request.Comercial) ? null : request.Comercial.Trim(),
            CreadoEn = ahora,
        };
        var version = NuevaVersion(convenio.Id, 1, request.VigenciaDesde, request.VigenciaHasta, request.TcMinimo,
            request.DiasLibres, OrigenVersionConvenio.MANUAL, request.Servicios, servicios, ahora);
        convenio.Versiones.Add(version);
        repositorio.AgregarConvenio(convenio);

        auditoria.Registrar("convenio", convenio.Id, "CREAR", null, new { cliente.Rut, convenio.Comercial, VersionId = version.Id });
        auditoria.Registrar("version_convenio", version.Id, "CREAR", null, Dto(version, servicios));
        await unidadTrabajo.GuardarAsync(ct);

        return ServiceResult<VersionConvenioDto>.Created(Dto(version, servicios));
    }

    public async Task<ServiceResult<VersionConvenioDto>> CrearVersionAsync(Guid convenioId, NuevaVersionConvenioRequest request, CancellationToken ct)
    {
        var convenio = await repositorio.ObtenerAsync(convenioId, ct);
        if (convenio is null)
        {
            return ServiceResult<VersionConvenioDto>.NoEncontrado("El convenio no existe.");
        }
        if (request.Origen is not (OrigenVersionConvenio.MANUAL or OrigenVersionConvenio.COMPLEMENTO_CORREO))
        {
            return ServiceResult<VersionConvenioDto>.Invalido("El origen de una versión nueva debe ser MANUAL o COMPLEMENTO_CORREO.");
        }

        var servicios = await ServiciosPorIdAsync(ct);
        var error = Validar(request.VigenciaDesde, request.VigenciaHasta, request.TcMinimo, request.DiasLibres, request.Servicios, servicios);
        if (error is not null)
        {
            return ServiceResult<VersionConvenioDto>.Invalido(error);
        }

        var numero = convenio.Versiones.Count == 0 ? 1 : convenio.Versiones.Max(v => v.Numero) + 1;
        var version = NuevaVersion(convenio.Id, numero, request.VigenciaDesde, request.VigenciaHasta, request.TcMinimo,
            request.DiasLibres, request.Origen, request.Servicios, servicios, reloj.GetUtcNow());
        repositorio.AgregarVersion(version);

        var motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim();
        auditoria.Registrar("version_convenio", version.Id, "CREAR_VERSION", null, new { Version = Dto(version, servicios), Motivo = motivo });
        await unidadTrabajo.GuardarAsync(ct);

        return ServiceResult<VersionConvenioDto>.Created(Dto(version, servicios));
    }

    public async Task<ServiceResult<VersionConvenioDto>> ConfirmarVersionAsync(Guid convenioId, Guid versionId, CancellationToken ct)
    {
        var convenio = await repositorio.ObtenerAsync(convenioId, ct);
        var version = convenio?.Versiones.FirstOrDefault(v => v.Id == versionId);
        if (convenio is null || version is null)
        {
            return ServiceResult<VersionConvenioDto>.NoEncontrado("La versión del convenio no existe.");
        }
        if (version.Estado != EstadoVersionConvenio.BORRADOR)
        {
            return ServiceResult<VersionConvenioDto>.Error(409, VersionNoEditable,
                $"La versión {version.Numero} está {version.Estado}; solo se confirma una versión en BORRADOR.");
        }

        var ahora = reloj.GetUtcNow();
        foreach (var anterior in convenio.Versiones.Where(v => v.Estado == EstadoVersionConvenio.VIGENTE))
        {
            anterior.Estado = EstadoVersionConvenio.REEMPLAZADA;
            auditoria.Registrar("version_convenio", anterior.Id, "REEMPLAZAR",
                new { Estado = EstadoVersionConvenio.VIGENTE }, new { anterior.Estado, ReemplazadaPor = version.Id });
        }

        version.Estado = EstadoVersionConvenio.VIGENTE;
        version.ConfirmadoPor = usuario.UsuarioAuditoria;
        version.ConfirmadoEn = ahora;
        auditoria.Registrar("version_convenio", version.Id, "CONFIRMAR",
            new { Estado = EstadoVersionConvenio.BORRADOR }, new { version.Estado, version.ConfirmadoPor, version.ConfirmadoEn });
        await unidadTrabajo.GuardarAsync(ct);

        return ServiceResult<VersionConvenioDto>.Ok(Dto(version, await ServiciosPorIdAsync(ct)));
    }

    /// <summary>Reglas del formulario manual (ADR-012). Devuelve el mensaje del primer error o null.</summary>
    private static string? Validar(
        DateOnly desde, DateOnly? hasta, decimal? tcMinimo, int? diasLibres,
        IReadOnlyList<ServicioConvenioDto>? servicios, IReadOnlyDictionary<int, TipoServicio> catalogo)
    {
        if (hasta is not null && hasta < desde)
        {
            return "vigenciaHasta no puede ser anterior a vigenciaDesde.";
        }
        if (tcMinimo is <= 0)
        {
            return "tcMinimo debe ser mayor que 0.";
        }
        if (diasLibres is < 0)
        {
            return "diasLibres no puede ser negativo.";
        }
        if (servicios is null || servicios.Count == 0)
        {
            return "El convenio debe tener al menos un servicio.";
        }

        foreach (var s in servicios)
        {
            if (!catalogo.ContainsKey(s.TipoServicioId))
            {
                return $"El servicio {s.TipoServicioId} no existe en el catálogo de servicios.";
            }
            if (s.Moneda is not (Moneda.CLP or Moneda.USD))
            {
                return $"Servicio {s.TipoServicioId}: la moneda debe ser CLP o USD (UF no aplica en convenios).";
            }
            if (s.Tarifa < 0)
            {
                return $"Servicio {s.TipoServicioId}: la tarifa no puede ser negativa.";
            }
        }

        var repetido = servicios.GroupBy(s => s.TipoServicioId).FirstOrDefault(g => g.Count() > 1);
        return repetido is null ? null : $"El servicio {repetido.Key} viene más de una vez.";
    }

    /// <summary>
    /// Versión en BORRADOR. La unidad se copia de tipo_servicio.unidad_normalizada (RN-036).
    /// Las tarifas USD sin tcMinimo se aceptan: el campo es opcional y el motor usa el tipo de cambio del día sin piso.
    /// </summary>
    private static VersionConvenio NuevaVersion(
        Guid convenioId, int numero, DateOnly desde, DateOnly? hasta, decimal? tcMinimo, int? diasLibres,
        OrigenVersionConvenio origen, IReadOnlyList<ServicioConvenioDto> servicios,
        IReadOnlyDictionary<int, TipoServicio> catalogo, DateTimeOffset ahora)
    {
        var id = Guid.CreateVersion7();
        return new VersionConvenio
        {
            Id = id,
            ConvenioId = convenioId,
            Numero = numero,
            Estado = EstadoVersionConvenio.BORRADOR,
            VigenciaDesde = desde,
            VigenciaHasta = hasta,
            TcMinimo = tcMinimo,
            DiasLibres = diasLibres,
            Origen = origen,
            CreadoEn = ahora,
            Servicios = servicios.Select(s => new ServicioConvenio
            {
                Id = Guid.CreateVersion7(),
                VersionConvenioId = id,
                TipoServicioId = s.TipoServicioId,
                Tarifa = s.Tarifa,
                Moneda = s.Moneda,
                Unidad = catalogo[s.TipoServicioId].UnidadNormalizada.ToString(),
            }).ToList(),
        };
    }

    private async Task<IReadOnlyDictionary<int, TipoServicio>> ServiciosPorIdAsync(CancellationToken ct) =>
        (await catalogo.ListarTiposServicioAsync(ct)).ToDictionary(t => t.TipoServicioId);

    private static VersionConvenioDto Dto(VersionConvenio v, IReadOnlyDictionary<int, TipoServicio> catalogo) => new(
        v.Id, v.ConvenioId, v.Numero, v.Estado, v.VigenciaDesde, v.VigenciaHasta, v.TcMinimo, v.DiasLibres, v.Origen,
        ConfianzaIa: null, v.ConfirmadoPor, v.ConfirmadoEn, v.CreadoEn,
        v.Servicios.OrderBy(s => s.TipoServicioId).Select(s => new ServicioConvenioDto(
            s.TipoServicioId, s.Tarifa, s.Moneda,
            Enum.TryParse<UnidadCobro>(s.Unidad, out var unidad) ? unidad : null,
            catalogo.TryGetValue(s.TipoServicioId, out var t) ? t.Descripcion : null)).ToList());
}
