using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Satelite.Infraestructura.Persistencia.Migraciones
{
    /// <summary>
    /// Migración inicial: ejecuta, en orden, copias congeladas de <c>db/init/01-esquema.sql</c> (generado desde
    /// <c>docs/arquitectura/modelo-datos.md</c>) y <c>db/init/02-ajustes-etapa1.sql</c> ([E1-1] a [E1-7]).
    /// Así se reproducen tal cual los CHECK, triggers, índices parciales y el CHECK con <c>jsonb_path_exists</c>.
    /// El modelo de EF Core (snapshot) se generó con <c>migrations add</c> y mapea las mismas tablas.
    /// Un cambio de esquema posterior va en una migración nueva, no aquí.
    /// </summary>
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(LeerScript("Migraciones.01-esquema.sql"));
            migrationBuilder.Sql(LeerScript("Migraciones.02-ajustes-etapa1.sql"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "La migración inicial no se revierte: recrear la base (db/recrear-con-migracion.ps1).");
        }

        private static string LeerScript(string nombreRecurso)
        {
            using var stream = typeof(Inicial).Assembly.GetManifestResourceStream(nombreRecurso)
                ?? throw new InvalidOperationException($"No se encontró el recurso embebido '{nombreRecurso}'.");
            using var lector = new StreamReader(stream, System.Text.Encoding.UTF8);
            return lector.ReadToEnd();
        }
    }
}
