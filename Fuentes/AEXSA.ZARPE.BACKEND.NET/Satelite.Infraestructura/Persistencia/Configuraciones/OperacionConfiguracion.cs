using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia.Configuraciones;

internal sealed class OperacionConfiguracion : IEntityTypeConfiguration<Operacion>
{
    public void Configure(EntityTypeBuilder<Operacion> b)
    {
        b.ToTable("operacion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.OperacionIdAexsa).HasMaxLength(40);
        b.Property(x => x.FacturadorAsignadoPor).HasMaxLength(80);
        b.Property(x => x.IngeridoEn).HasDefaultValueSql(Sql.Ahora);
        b.Property(x => x.ActualizadoEn).HasDefaultValueSql(Sql.Ahora);

        // [E1-6] La clave es única por entidad.
        b.HasIndex(x => new { x.Entidad, x.OperacionIdAexsa }, "ux_operacion_entidad_clave").IsUnique();
        b.HasIndex(x => x.ClienteId, "ix_operacion_cliente");

        b.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId);
        b.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ConsignatarioId);
        b.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteFacturadorId);
        b.HasOne<VersionConvenio>().WithMany().HasForeignKey(x => x.VersionConvenioId);

        b.HasMany(x => x.Bls).WithOne().HasForeignKey(x => x.OperacionId);
        b.HasMany(x => x.Contenedores).WithOne().HasForeignKey(x => x.OperacionId);
        b.HasMany(x => x.Servicios).WithOne().HasForeignKey(x => x.OperacionId);
        b.HasMany(x => x.Pendientes).WithOne().HasForeignKey(x => x.OperacionId);
    }
}

internal sealed class OperacionBlConfiguracion : IEntityTypeConfiguration<OperacionBl>
{
    public void Configure(EntityTypeBuilder<OperacionBl> b)
    {
        b.ToTable("operacion_bl");
        b.HasKey(x => new { x.OperacionId, x.BlId });
        b.Property(x => x.Numero).HasMaxLength(40);
        b.HasIndex(x => x.BlId, "ix_operacion_bl_documento");
    }
}

internal sealed class ContenedorOperacionConfiguracion : IEntityTypeConfiguration<ContenedorOperacion>
{
    public void Configure(EntityTypeBuilder<ContenedorOperacion> b)
    {
        b.ToTable("contenedor_operacion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Numero).HasMaxLength(15);
        b.Property(x => x.Inspecciones).HasColumnType(Sql.Json);
        b.HasIndex(x => new { x.OperacionId, x.Numero }).IsUnique();
    }
}

internal sealed class ServicioPrestadoConfiguracion : IEntityTypeConfiguration<ServicioPrestado>
{
    public void Configure(EntityTypeBuilder<ServicioPrestado> b)
    {
        b.ToTable("servicio_prestado");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.Contenedor).HasMaxLength(15);
        b.HasIndex(x => x.OperacionId, "ix_servicio_prestado_op");
        b.HasOne<TipoServicio>().WithMany().HasForeignKey(x => x.TipoServicioId);
    }
}

internal sealed class OperacionPendienteConfiguracion : IEntityTypeConfiguration<OperacionPendiente>
{
    public void Configure(EntityTypeBuilder<OperacionPendiente> b)
    {
        b.ToTable("operacion_pendiente");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Detalle).HasColumnType(Sql.Json);
        b.Property(x => x.Decision).HasColumnType(Sql.Json);
        b.Property(x => x.ResueltoPor).HasMaxLength(120);
        b.Property(x => x.DetectadoEn).HasDefaultValueSql(Sql.Ahora);
        b.HasIndex(x => new { x.OperacionId, x.Tipo }, "ux_pendiente_abierto")
            .IsUnique()
            .HasFilter("resuelto_en IS NULL");
    }
}

internal sealed class EventoAexsaConfiguracion : IEntityTypeConfiguration<EventoAexsa>
{
    public void Configure(EntityTypeBuilder<EventoAexsa> b)
    {
        b.ToTable("evento_aexsa");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.OperacionIdAexsa).HasMaxLength(40);
        b.Property(x => x.RecibidoEn).HasDefaultValueSql(Sql.Ahora);
        b.Property(x => x.Carga).HasColumnType(Sql.Json);
        b.Property(x => x.CodigoError).HasMaxLength(40);

        // Solo una versión APLICADA por operación ([E1-6]: entidad + operacion_id_aexsa + version).
        b.HasIndex(x => new { x.Entidad, x.OperacionIdAexsa, x.Version }, "ux_evento_aexsa_aplicado")
            .IsUnique()
            .HasFilter("resultado = 'APLICADO'");
        b.HasIndex(x => new { x.Entidad, x.OperacionIdAexsa, x.RecibidoEn }, "ix_evento_aexsa_operacion");
    }
}
