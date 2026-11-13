using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Satelite.Aplicacion.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia;

internal sealed class UnidadTrabajo(PrincipalDbContext db) : IUnidadTrabajo
{
    /// <summary>Códigos que los triggers de 02-ajustes-etapa1.sql ponen al inicio del mensaje.</summary>
    private static readonly string[] CodigosConocidos = ["TIPO_CAMBIO_DE_SAP_EXISTE"];

    public async Task<ITransaccion> IniciarTransaccionAsync(CancellationToken ct) =>
        new Transaccion(await db.Database.BeginTransactionAsync(ct));

    public async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
                                           && CodigosConocidos.FirstOrDefault(c => pg.MessageText.StartsWith(c, StringComparison.Ordinal)) is { } codigo)
        {
            throw new ReglaDatosException(codigo, pg.MessageText, ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
                                           {
                                               SqlState: PostgresErrorCodes.UniqueViolation,
                                               ConstraintName: "ux_operacion_entidad_clave",
                                           } pg)
        {
            throw new ReglaDatosException(ReglasDatos.OperacionYaExiste, pg.MessageText, ex);
        }
    }
    public void DescartarCambios() => db.ChangeTracker.Clear();

    private sealed class Transaccion(IDbContextTransaction transaccion) : ITransaccion
    {
        public Task ConfirmarAsync(CancellationToken ct) => transaccion.CommitAsync(ct);

        public ValueTask DisposeAsync() => transaccion.DisposeAsync();
    }
}

internal sealed class Auditoria(PrincipalDbContext db, ICurrentUser usuario, TimeProvider reloj) : IAuditoria
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public void Registrar(string entidad, Guid? entidadId, string accion, object? valorAnterior, object? valorNuevo)
    {
        db.EventosAuditoria.Add(new EventoAuditoria
        {
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            Usuario = usuario.UsuarioAuditoria,
            ValorAnterior = valorAnterior is null ? null : JsonSerializer.Serialize(valorAnterior, Json),
            ValorNuevo = valorNuevo is null ? null : JsonSerializer.Serialize(valorNuevo, Json),
            OcurridoEn = reloj.GetUtcNow(),
        });
    }
}
