using Microsoft.EntityFrameworkCore;

namespace Satelite.Infraestructura.Persistencia;

/// <summary>
/// DbContext principal del Satélite (PostgreSQL).
/// Las entidades y la migración inicial salen de <c>docs/arquitectura/modelo-datos.md</c> (tarea 0.2, ADR-010).
/// Recordar: las tablas de memoria de cálculo y auditoría son append-only.
/// </summary>
public class PrincipalDbContext : DbContext
{
    public PrincipalDbContext(DbContextOptions<PrincipalDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuraciones por entidad (IEntityTypeConfiguration<T>) dentro de este ensamblado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrincipalDbContext).Assembly);
    }
}
