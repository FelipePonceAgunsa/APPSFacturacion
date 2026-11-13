using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia.Configuraciones;

internal sealed class OfaConfiguracion : IEntityTypeConfiguration<Ofa>
{
    public void Configure(EntityTypeBuilder<Ofa> b)
    {
        b.ToTable("ofa");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.FolioIdempotencia).HasMaxLength(80);
        b.Property(x => x.FechaCalculo).HasDefaultValueSql(Sql.Ahora);
        b.HasIndex(x => x.FolioIdempotencia).IsUnique();
        b.HasIndex(x => x.OperacionId, "ix_ofa_operacion");
        b.HasIndex(x => x.OperacionId, "ux_ofa_principal").IsUnique().HasFilter("tipo = 'PRINCIPAL'");
        b.HasIndex(x => x.Estado, "ix_ofa_estado");
        b.HasOne<Operacion>().WithMany().HasForeignKey(x => x.OperacionId);
        b.HasMany(x => x.Lineas).WithOne().HasForeignKey(l => l.OfaId);
    }
}

internal sealed class AsignacionCobroConfiguracion : IEntityTypeConfiguration<AsignacionCobro>
{
    public void Configure(EntityTypeBuilder<AsignacionCobro> b)
    {
        b.ToTable("asignacion_cobro");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.DestinatarioRut).HasMaxLength(12);
        b.HasOne<Ofa>().WithMany().HasForeignKey(x => x.OfaId);
    }
}

internal sealed class LineaOfaConfiguracion : IEntityTypeConfiguration<LineaOfa>
{
    public void Configure(EntityTypeBuilder<LineaOfa> b)
    {
        b.ToTable("linea_ofa");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.ArticuloSapId).HasMaxLength(40);
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.Property(x => x.Tarifa).HasPrecision(18, 4);
        b.Property(x => x.Moneda).HasColumnType(Sql.Moneda);
        b.Property(x => x.Monto).HasPrecision(18, 4);
        b.Property(x => x.AgregadaPor).HasMaxLength(120);
        b.HasIndex(x => new { x.OfaId, x.VersionCalculo }, "ix_linea_ofa_ofa");
        b.HasIndex(x => new { x.OfaId, x.Origen }, "ix_linea_ofa_origen");
        b.HasOne<AsignacionCobro>().WithMany().HasForeignKey(x => x.AsignacionCobroId);
        b.HasOne<TipoServicio>().WithMany().HasForeignKey(x => x.TipoServicioId);
        b.HasOne<LineaOfa>().WithMany().HasForeignKey(x => x.LineaOrigenId);
        b.HasMany(x => x.Memorias).WithOne().HasForeignKey(m => m.LineaOfaId);
    }
}

internal sealed class MemoriaCalculoConfiguracion : IEntityTypeConfiguration<MemoriaCalculoRegistro>
{
    public void Configure(EntityTypeBuilder<MemoriaCalculoRegistro> b)
    {
        b.ToTable("memoria_calculo");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.ReglasAplicadas).HasColumnType(Sql.Json);
        b.Property(x => x.TipoCambio).HasPrecision(12, 4);
        b.Property(x => x.AjusteManual).HasColumnType(Sql.Json);
        b.Property(x => x.CalculadoEn).HasDefaultValueSql(Sql.Ahora);
        b.HasIndex(x => new { x.LineaOfaId, x.CalculadoEn }, "ix_memoria_linea").IsDescending(false, true);
        b.HasOne<VersionConvenio>().WithMany().HasForeignKey(x => x.VersionConvenioId);
        b.HasOne<CatalogoSnapshot>().WithMany().HasForeignKey(x => x.TarifaSnapshotVer);
        b.HasOne<ReglaConfigurable>().WithMany().HasForeignKey(x => x.ReglaConfigId);
    }
}
