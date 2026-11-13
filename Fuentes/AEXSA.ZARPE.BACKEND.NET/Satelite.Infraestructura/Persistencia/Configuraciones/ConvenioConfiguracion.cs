using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia.Configuraciones;

internal sealed class ConvenioConfiguracion : IEntityTypeConfiguration<Convenio>
{
    public void Configure(EntityTypeBuilder<Convenio> b)
    {
        b.ToTable("convenio");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.CreadoEn).HasDefaultValueSql(Sql.Ahora);
        b.HasOne(x => x.Cliente).WithMany().HasForeignKey(x => x.ClienteId);
        b.HasMany(x => x.Versiones).WithOne(v => v.Convenio).HasForeignKey(v => v.ConvenioId);
    }
}

internal sealed class VersionConvenioConfiguracion : IEntityTypeConfiguration<VersionConvenio>
{
    public void Configure(EntityTypeBuilder<VersionConvenio> b)
    {
        b.ToTable("version_convenio");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.TcMinimo).HasPrecision(12, 4);
        b.Property(x => x.CreadoEn).HasDefaultValueSql(Sql.Ahora);
        b.HasIndex(x => new { x.ConvenioId, x.Numero }).IsUnique();
        b.HasIndex(x => new { x.ConvenioId, x.Estado, x.VigenciaDesde }, "ix_version_convenio_vigente");
        b.HasMany(x => x.Servicios).WithOne().HasForeignKey(s => s.VersionConvenioId);
    }
}

internal sealed class ServicioConvenioConfiguracion : IEntityTypeConfiguration<ServicioConvenio>
{
    public void Configure(EntityTypeBuilder<ServicioConvenio> b)
    {
        b.ToTable("servicio_convenio");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Tarifa).HasPrecision(18, 4);
        b.Property(x => x.Moneda).HasColumnType(Sql.Moneda);
        b.HasOne<TipoServicio>().WithMany().HasForeignKey(x => x.TipoServicioId);
    }
}
