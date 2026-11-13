using Microsoft.EntityFrameworkCore;
using Satelite.Infraestructura.Persistencia;
using Testcontainers.PostgreSql;

namespace Satelite.Tests.Infraestructura;

/// <summary>
/// PostgreSQL 16 en un contenedor desechable (Testcontainers) con la migración de EF Core aplicada.
/// Requiere Docker. Se comparte entre las clases de la colección <see cref="PostgresCollection"/>.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _contenedor = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("zarpe_facturacion")
        .Build();

    public string ConnectionString => _contenedor.GetConnectionString();

    public PrincipalDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<PrincipalDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask InitializeAsync()
    {
        await _contenedor.StartAsync();
        await using var db = CrearContexto();
        await db.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _contenedor.DisposeAsync();
}

[CollectionDefinition(Nombre)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Nombre = "Postgres";
}
