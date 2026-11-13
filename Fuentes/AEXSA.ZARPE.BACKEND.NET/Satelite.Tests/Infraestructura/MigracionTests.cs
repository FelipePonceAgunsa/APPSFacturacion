using Microsoft.EntityFrameworkCore;
using Npgsql;
using Satelite.Dominio.Comun;
using Satelite.Dominio.Entidades;

namespace Satelite.Tests.Infraestructura;

/// <summary>Tarea 0.2: la migración inicial crea el esquema de 01 + 02 y el modelo de EF Core coincide con ella.</summary>
[Collection(PostgresCollection.Nombre)]
public class MigracionTests(PostgresFixture fixture)
{
    [Fact]
    public async Task La_migracion_inicial_se_aplica_y_crea_todas_las_tablas()
    {
        await using var db = fixture.CrearContexto();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        var tablas = await db.Database
            .SqlQueryRaw<string>("SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync(TestContext.Current.CancellationToken);
        var esperadas = db.Model.GetEntityTypes().Select(e => e.GetTableName()!).ToList();
        Assert.All(esperadas, t => Assert.Contains(t, tablas));
        Assert.Contains("operacion_bl", tablas);           // [E1-6]
        Assert.Contains("tipo_inspeccion_mapa", tablas);   // [E1-7]
    }

    [Fact]
    public async Task El_modelo_no_tiene_cambios_pendientes_respecto_de_la_migracion()
    {
        await using var db = fixture.CrearContexto();

        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task La_version_del_evento_no_puede_retroceder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = fixture.CrearContexto();
        var operacion = new Operacion
        {
            Entidad = EntidadAexsa.VISADO_VIRTUAL,
            OperacionIdAexsa = "910001",
            IdsAexsa = [910001],
            VersionEvento = 3,
            Din = [5550001],
        };
        db.Operaciones.Add(operacion);
        await db.SaveChangesAsync(ct);

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE operacion SET version_evento = 2 WHERE id = {0}", [operacion.Id], ct));

        Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState);
        Assert.Contains("version_evento no puede retroceder", error.MessageText);
    }
}
