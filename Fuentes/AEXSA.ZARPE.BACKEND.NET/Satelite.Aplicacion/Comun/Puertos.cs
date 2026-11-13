namespace Satelite.Aplicacion.Comun;

/// <summary>Unidad de trabajo sobre la persistencia (implementada con EF Core en Infraestructura).</summary>
public interface IUnidadTrabajo
{
    /// <summary>Inicia una transacción. Si no se confirma, se revierte al liberarla.</summary>
    Task<ITransaccion> IniciarTransaccionAsync(CancellationToken ct);

    /// <summary>
    /// Guarda los cambios pendientes. Traduce las reglas de la base que tienen código conocido
    /// (p. ej. el trigger <c>TIPO_CAMBIO_DE_SAP_EXISTE</c>) a <see cref="ReglaDatosException"/>.
    /// </summary>
    Task GuardarAsync(CancellationToken ct);
    /// <summary>Olvida los cambios pendientes (p. ej. para reintentar tras <see cref="ReglasDatos.OperacionYaExiste"/>).</summary>
    void DescartarCambios();
}
/// <summary>Códigos de <see cref="ReglaDatosException"/> que traduce <see cref="IUnidadTrabajo.GuardarAsync"/>.</summary>
public static class ReglasDatos
{
    /// <summary>Trigger de 02-ajustes-etapa1.sql: el día ya tiene tipo de cambio de SAP.</summary>
    public const string TipoCambioDeSapExiste = "TIPO_CAMBIO_DE_SAP_EXISTE";
    /// <summary>Índice único ux_operacion_entidad_clave: otra transacción insertó la misma operación (§4b).</summary>
    public const string OperacionYaExiste = "OPERACION_YA_EXISTE";
}

public interface ITransaccion : IAsyncDisposable
{
    Task ConfirmarAsync(CancellationToken ct);
}

/// <summary>
/// Auditoría append-only en <c>evento_auditoria</c>. El usuario sale de <see cref="ICurrentUser.UsuarioAuditoria"/>.
/// El registro se guarda junto con los demás cambios en <see cref="IUnidadTrabajo.GuardarAsync"/>.
/// </summary>
public interface IAuditoria
{
    /// <param name="entidad">Tabla o agregado afectado (p. ej. <c>version_convenio</c>).</param>
    /// <param name="entidadId">Id uuid de la entidad; null si su clave no es uuid (va en el valor).</param>
    /// <param name="valorAnterior">Se serializa a JSON; null si no aplica.</param>
    /// <param name="valorNuevo">Se serializa a JSON; null si no aplica.</param>
    void Registrar(string entidad, Guid? entidadId, string accion, object? valorAnterior, object? valorNuevo);
}

/// <summary>
/// Almacenamiento de archivos (S3 en AWS; carpeta local en desarrollo).
/// Las rutas son relativas, con <c>/</c> como separador (p. ej. <c>importaciones/{id}/articulos.csv</c>).
/// </summary>
public interface IAlmacenArchivos
{
    /// <summary>Guarda el contenido y devuelve la URI con que se referencia (p. ej. <c>local://importaciones/{id}/</c>).</summary>
    Task<string> GuardarAsync(string ruta, Stream contenido, CancellationToken ct);

    Task<Stream> AbrirAsync(string ruta, CancellationToken ct);

    /// <summary>URI de una carpeta, para guardarla como referencia (p. ej. <c>catalogo_importacion.archivo_s3</c>).</summary>
    string UriDe(string ruta);
}

/// <summary>Una regla de la base rechazó el cambio con un código conocido del contrato.</summary>
public sealed class ReglaDatosException(string codigo, string mensaje, Exception? inner = null)
    : Exception(mensaje, inner)
{
    public string Codigo { get; } = codigo;
}
