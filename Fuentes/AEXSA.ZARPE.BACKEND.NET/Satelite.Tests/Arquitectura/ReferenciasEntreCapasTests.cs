using System.Reflection;

namespace Satelite.Tests.Arquitectura;

/// <summary>
/// Verificación de la tarea 0.1 (ADR-001): las dependencias entre capas van hacia adentro.
/// </summary>
public class ReferenciasEntreCapasTests
{
    private static readonly string[] Infraestructura =
        ["Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore"];

    private static List<string> ReferenciasDe(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();

    [Fact]
    public void Dominio_no_depende_de_otras_capas_ni_de_infraestructura()
    {
        var referencias = ReferenciasDe(typeof(Dominio.AssemblyReference).Assembly);

        Assert.DoesNotContain(referencias, r => r.StartsWith("Satelite.", StringComparison.Ordinal));
        Assert.DoesNotContain(referencias, r => Infraestructura.Any(i => r.StartsWith(i, StringComparison.Ordinal)));
    }

    [Fact]
    public void Aplicacion_no_depende_de_infraestructura_ni_de_la_api()
    {
        var referencias = ReferenciasDe(typeof(Aplicacion.AssemblyReference).Assembly);

        Assert.DoesNotContain("Satelite.Infraestructura", referencias);
        Assert.DoesNotContain("Satelite.Api", referencias);
        Assert.DoesNotContain(referencias, r => Infraestructura.Any(i => r.StartsWith(i, StringComparison.Ordinal)));
    }
}
