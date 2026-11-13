using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia.Configuraciones;

internal sealed class CatalogoImportacionConfiguracion : IEntityTypeConfiguration<CatalogoImportacion>
{
    public void Configure(EntityTypeBuilder<CatalogoImportacion> b)
    {
        b.ToTable("catalogo_importacion");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.HashContenido).HasColumnType("character(64)");
        b.Property(x => x.CargadoPor).HasMaxLength(120);
        b.Property(x => x.CargadoEn).HasDefaultValueSql(Sql.Ahora);
        b.Property(x => x.ResueltoPor).HasMaxLength(120);
        b.HasOne<CatalogoSnapshot>().WithMany().HasForeignKey(x => x.SnapshotVersion)
            .HasConstraintName("fk_importacion_snapshot");
        b.HasMany(x => x.Errores).WithOne().HasForeignKey(e => e.ImportacionId);
    }
}

internal sealed class CatalogoImportacionErrorConfiguracion : IEntityTypeConfiguration<CatalogoImportacionError>
{
    public void Configure(EntityTypeBuilder<CatalogoImportacionError> b)
    {
        b.ToTable("catalogo_importacion_error");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseSerialColumn();
        b.Property(x => x.Columna).HasMaxLength(60);
        b.Property(x => x.Codigo).HasMaxLength(40);
        b.HasIndex(x => x.ImportacionId, "ix_importacion_error");
    }
}

internal sealed class CatalogoSnapshotConfiguracion : IEntityTypeConfiguration<CatalogoSnapshot>
{
    public void Configure(EntityTypeBuilder<CatalogoSnapshot> b)
    {
        b.ToTable("catalogo_snapshot");
        b.HasKey(x => x.Version);
        b.Property(x => x.Version).ValueGeneratedNever();
        b.Property(x => x.Fecha).HasDefaultValueSql(Sql.Ahora);
        b.Property(x => x.ConfirmadoPor).HasMaxLength(120);
        b.HasIndex(x => x.ImportacionId).IsUnique();
        b.HasOne<CatalogoImportacion>().WithOne().HasForeignKey<CatalogoSnapshot>(x => x.ImportacionId);
        b.HasOne<CatalogoSnapshot>().WithMany().HasForeignKey(x => x.BaseVersion);
    }
}

internal sealed class SapArticuloConfiguracion : IEntityTypeConfiguration<SapArticulo>
{
    public void Configure(EntityTypeBuilder<SapArticulo> b)
    {
        b.ToTable("sap_articulo");
        b.HasKey(x => new { x.SnapshotVersion, x.Codigo });
        b.Property(x => x.Codigo).HasMaxLength(40);
        b.Property(x => x.UnidadMedida).HasMaxLength(20);
        b.Property(x => x.Grupo).HasMaxLength(60);
        b.HasOne<CatalogoSnapshot>().WithMany().HasForeignKey(x => x.SnapshotVersion);
    }
}

internal sealed class SapListaPreciosConfiguracion : IEntityTypeConfiguration<SapListaPrecios>
{
    public void Configure(EntityTypeBuilder<SapListaPrecios> b)
    {
        b.ToTable("sap_lista_precios");
        b.HasKey(x => new { x.SnapshotVersion, x.Codigo });
        b.Property(x => x.Codigo).HasMaxLength(30);
        b.HasOne<CatalogoSnapshot>().WithMany().HasForeignKey(x => x.SnapshotVersion);
    }
}

internal sealed class SapKitConfiguracion : IEntityTypeConfiguration<SapKit>
{
    public void Configure(EntityTypeBuilder<SapKit> b)
    {
        b.ToTable("sap_kit");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.KitCodigo).HasMaxLength(40);
        b.HasIndex(x => new { x.SnapshotVersion, x.KitCodigo }).IsUnique();
        b.HasAlternateKey(x => new { x.Id, x.SnapshotVersion });
        b.HasOne<CatalogoSnapshot>().WithMany().HasForeignKey(x => x.SnapshotVersion);

        // [DBA-3b] La composición usa la misma versión que el kit.
        b.HasMany(x => x.Articulos).WithOne()
            .HasForeignKey(a => new { a.KitId, a.SnapshotVersion })
            .HasPrincipalKey(k => new { k.Id, k.SnapshotVersion });
    }
}

internal sealed class SapKitArticuloConfiguracion : IEntityTypeConfiguration<SapKitArticulo>
{
    public void Configure(EntityTypeBuilder<SapKitArticulo> b)
    {
        b.ToTable("sap_kit_articulo");
        b.HasKey(x => new { x.KitId, x.ArticuloId });
        b.Property(x => x.ArticuloId).HasMaxLength(40);
        b.Property(x => x.Cantidad).HasPrecision(18, 4);
        b.HasOne<SapArticulo>().WithMany()
            .HasForeignKey(x => new { x.SnapshotVersion, x.ArticuloId })
            .HasPrincipalKey(a => new { a.SnapshotVersion, a.Codigo });
    }
}

internal sealed class ServicioArticuloSapConfiguracion : IEntityTypeConfiguration<ServicioArticuloSap>
{
    public void Configure(EntityTypeBuilder<ServicioArticuloSap> b)
    {
        b.ToTable("servicio_articulo_sap");
        b.HasKey(x => new { x.TipoServicioId, x.ArticuloId });
        b.Property(x => x.ArticuloId).HasMaxLength(40);
        b.HasOne<TipoServicio>().WithMany().HasForeignKey(x => x.TipoServicioId);
    }
}

internal sealed class TipoCambioConfiguracion : IEntityTypeConfiguration<TipoCambio>
{
    public void Configure(EntityTypeBuilder<TipoCambio> b)
    {
        b.ToTable("tipo_cambio");
        b.HasKey(x => new { x.Fecha, x.Moneda });
        b.Property(x => x.Moneda).HasColumnType(Sql.Moneda);
        b.Property(x => x.Valor).HasPrecision(12, 4);
        b.HasOne<CatalogoImportacion>().WithMany().HasForeignKey(x => x.ImportacionId);
    }
}

internal sealed class TarifaSnapshotConfiguracion : IEntityTypeConfiguration<TarifaSnapshot>
{
    public void Configure(EntityTypeBuilder<TarifaSnapshot> b)
    {
        b.ToTable("tarifa_snapshot");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Fecha).HasDefaultValueSql(Sql.Ahora);
        b.Property(x => x.ArticuloId).HasMaxLength(40);
        b.Property(x => x.ListaPreciosId).HasMaxLength(30);
        b.Property(x => x.Precio).HasPrecision(18, 4);
        b.Property(x => x.Moneda).HasColumnType(Sql.Moneda);
        b.HasIndex(x => new { x.Version, x.ArticuloId, x.ListaPreciosId, x.VigenteDesde }).IsUnique();
        b.HasOne<CatalogoSnapshot>().WithMany().HasForeignKey(x => x.Version);
        b.HasOne<SapArticulo>().WithMany()
            .HasForeignKey(x => new { x.Version, x.ArticuloId })
            .HasPrincipalKey(a => new { a.SnapshotVersion, a.Codigo });
        b.HasOne<SapListaPrecios>().WithMany()
            .HasForeignKey(x => new { x.Version, x.ListaPreciosId })
            .HasPrincipalKey(l => new { l.SnapshotVersion, l.Codigo });
    }
}
