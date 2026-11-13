using System.Text;
using Microsoft.EntityFrameworkCore;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia;

/// <summary>
/// DbContext principal del Satélite (PostgreSQL).
/// El esquema lo crea la migración inicial, que ejecuta <c>db/init/01-esquema.sql</c> (generado desde
/// <c>docs/arquitectura/modelo-datos.md</c>) y <c>db/init/02-ajustes-etapa1.sql</c> (tarea 0.2, ADR-010).
/// Este modelo mapea todas las tablas para que el snapshot de EF Core quede completo.
/// Recordar: las tablas de memoria de cálculo y auditoría son append-only.
/// </summary>
public class PrincipalDbContext : DbContext
{
    public PrincipalDbContext(DbContextOptions<PrincipalDbContext> options)
        : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<TipoServicio> TiposServicio => Set<TipoServicio>();
    public DbSet<Convenio> Convenios => Set<Convenio>();
    public DbSet<VersionConvenio> VersionesConvenio => Set<VersionConvenio>();
    public DbSet<ServicioConvenio> ServiciosConvenio => Set<ServicioConvenio>();
    public DbSet<Operacion> Operaciones => Set<Operacion>();
    public DbSet<OperacionBl> OperacionesBl => Set<OperacionBl>();
    public DbSet<ContenedorOperacion> ContenedoresOperacion => Set<ContenedorOperacion>();
    public DbSet<ServicioPrestado> ServiciosPrestados => Set<ServicioPrestado>();
    public DbSet<OperacionPendiente> OperacionesPendientes => Set<OperacionPendiente>();
    public DbSet<EventoAexsa> EventosAexsa => Set<EventoAexsa>();
    public DbSet<TerminalMapa> TerminalesMapa => Set<TerminalMapa>();
    public DbSet<TipoInspeccionMapa> TiposInspeccionMapa => Set<TipoInspeccionMapa>();
    public DbSet<CatalogoImportacion> CatalogoImportaciones => Set<CatalogoImportacion>();
    public DbSet<CatalogoImportacionError> CatalogoImportacionErrores => Set<CatalogoImportacionError>();
    public DbSet<CatalogoSnapshot> CatalogoSnapshots => Set<CatalogoSnapshot>();
    public DbSet<SapArticulo> SapArticulos => Set<SapArticulo>();
    public DbSet<SapListaPrecios> SapListasPrecios => Set<SapListaPrecios>();
    public DbSet<SapKit> SapKits => Set<SapKit>();
    public DbSet<SapKitArticulo> SapKitArticulos => Set<SapKitArticulo>();
    public DbSet<ServicioArticuloSap> ServiciosArticuloSap => Set<ServicioArticuloSap>();
    public DbSet<TipoCambio> TiposCambio => Set<TipoCambio>();
    public DbSet<TarifaSnapshot> TarifasSnapshot => Set<TarifaSnapshot>();
    public DbSet<ReglaConfigurable> ReglasConfigurables => Set<ReglaConfigurable>();
    public DbSet<Ofa> Ofas => Set<Ofa>();
    public DbSet<AsignacionCobro> AsignacionesCobro => Set<AsignacionCobro>();
    public DbSet<LineaOfa> LineasOfa => Set<LineaOfa>();
    public DbSet<MemoriaCalculoRegistro> MemoriasCalculo => Set<MemoriaCalculoRegistro>();
    public DbSet<OmisionDetectada> OmisionesDetectadas => Set<OmisionDetectada>();
    public DbSet<Alerta> Alertas => Set<Alerta>();
    public DbSet<Factura> Facturas => Set<Factura>();
    public DbSet<Pago> Pagos => Set<Pago>();
    public DbSet<PagoFactura> PagosFactura => Set<PagoFactura>();
    public DbSet<EventoAuditoria> EventosAuditoria => Set<EventoAuditoria>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuraciones por entidad (IEntityTypeConfiguration<T>) dentro de este ensamblado.
        // Cada una fija su tabla con ToTable("nombre_en_snake_case").
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PrincipalDbContext).Assembly);

        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var propiedad in entidad.GetProperties())
            {
                // Columnas en snake_case: OperacionIdAexsa -> operacion_id_aexsa.
                propiedad.SetColumnName(ASnakeCase(propiedad.Name));

                // Enumeraciones como texto con el mismo valor del CHECK (DP_WORLD, SIN_MAPEAR...).
                var tipo = Nullable.GetUnderlyingType(propiedad.ClrType) ?? propiedad.ClrType;
                if (tipo.IsEnum)
                {
                    propiedad.SetProviderClrType(typeof(string));
                }
            }

            // La base no declara ON DELETE: NO ACTION en todas las FK.
            foreach (var fk in entidad.GetForeignKeys())
            {
                fk.DeleteBehavior = DeleteBehavior.NoAction;
            }
        }
    }

    /// <summary>Convierte PascalCase a snake_case (TarifaSnapshotVer -> tarifa_snapshot_ver, DocumentoS3 -> documento_s3).</summary>
    internal static string ASnakeCase(string nombre)
    {
        var sb = new StringBuilder(nombre.Length + 8);
        for (var i = 0; i < nombre.Length; i++)
        {
            var c = nombre[i];
            if (char.IsUpper(c))
            {
                var anteriorMinOdigito = i > 0 && (char.IsLower(nombre[i - 1]) || char.IsDigit(nombre[i - 1]));
                var finDeSigla = i > 0 && char.IsUpper(nombre[i - 1]) && i + 1 < nombre.Length && char.IsLower(nombre[i + 1]);
                if (anteriorMinOdigito || finDeSigla)
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
