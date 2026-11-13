using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Satelite.Infraestructura.Persistencia;

/// <summary>
/// Fábrica de diseño, solo para <c>dotnet ef migrations add</c> (no se conecta a la base).
/// La cadena no lleva contraseña (regla 7 del steering). Para aplicar migraciones usar
/// <c>dotnet ef database update --connection "..."</c> o <c>db/recrear-con-migracion.ps1</c>.
/// </summary>
public sealed class DisenoDbContextFactory : IDesignTimeDbContextFactory<PrincipalDbContext>
{
    public PrincipalDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PrincipalDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=zarpe_facturacion;Username=postgres")
            .Options;
        return new PrincipalDbContext(options);
    }
}
