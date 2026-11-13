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

    /// <summary>
    /// Crea una base nueva en el mismo contenedor, con la migración aplicada, y devuelve su cadena de conexión.
    /// Sirve a las pruebas que necesitan partir de una base vacía (p. ej. la primera carga del catálogo).
    /// </summary>
    public async Task<string> CrearBaseNuevaAsync()
    {
        var nombre = "prueba_" + Guid.NewGuid().ToString("N")[..12];
        await using (var db = CrearContexto())
        {
            // El nombre lo arma esta clase (prefijo fijo + hex): no hay entrada externa.
#pragma warning disable EF1002
            await db.Database.ExecuteSqlRawAsync($"CREATE DATABASE {nombre}");
#pragma warning restore EF1002
        }

        var cadena = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = nombre }.ConnectionString;
        await using (var db = new PrincipalDbContext(new DbContextOptionsBuilder<PrincipalDbContext>().UseNpgsql(cadena).Options))
        {
            await db.Database.MigrateAsync();
        }
        return cadena;
    }

    public ValueTask DisposeAsync() => _contenedor.DisposeAsync();
}

[CollectionDefinition(Nombre)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Nombre = "Postgres";
}
