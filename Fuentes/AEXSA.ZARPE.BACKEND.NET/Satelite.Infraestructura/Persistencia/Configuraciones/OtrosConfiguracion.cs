using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia.Configuraciones;

internal sealed class ReglaConfigurableConfiguracion : IEntityTypeConfiguration<ReglaConfigurable>
{
    public void Configure(EntityTypeBuilder<ReglaConfigurable> b)
    {
        b.ToTable("regla_configurable");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Condicion).HasColumnType(Sql.Json);
        b.Property(x => x.Accion).HasColumnType(Sql.Json);
        b.Property(x => x.CreadoEn).HasDefaultValueSql(Sql.Ahora);
    }
}

internal sealed class OmisionDetectadaConfiguracion : IEntityTypeConfiguration<OmisionDetectada>
{
    public void Configure(EntityTypeBuilder<OmisionDetectada> b)
    {
        b.ToTable("omision_detectada");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Confianza).HasPrecision(5, 4);
        b.Property(x => x.DetectadaEn).HasDefaultValueSql(Sql.Ahora);
        b.Property(x => x.DecididaPor).HasMaxLength(120);
        b.HasIndex(x => x.OperacionId, "ix_omision_operacion");
        b.HasIndex(x => x.OperacionId, "ix_omision_sin_decidir").HasFilter("decision IS NULL");
        b.HasOne<Operacion>().WithMany().HasForeignKey(x => x.OperacionId);
        b.HasOne<Ofa>().WithMany().HasForeignKey(x => x.OfaId);
        b.HasOne<TipoServicio>().WithMany().HasForeignKey(x => x.TipoServicioId);
        b.HasOne<LineaOfa>().WithMany().HasForeignKey(x => x.LineaOfaId);
    }
}

internal sealed class AlertaConfiguracion : IEntityTypeConfiguration<Alerta>
{
    public void Configure(EntityTypeBuilder<Alerta> b)
    {
        b.ToTable("alerta");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Detalle).HasColumnType(Sql.Json);
        b.Property(x => x.CreadaEn).HasDefaultValueSql(Sql.Ahora);
        b.Property(x => x.AtendidaPor).HasMaxLength(120);
        b.HasIndex(x => new { x.Tipo, x.CreadaEn }, "ix_alerta_abierta").HasFilter("estado = 'ABIERTA'");

        // [DBA-10b] NULLS NOT DISTINCT: dos alertas abiertas del mismo tipo sin entidad_id cuentan como repetidas.
        b.HasIndex(x => new { x.Tipo, x.Entidad, x.EntidadId }, "ux_alerta_abierta_entidad")
            .IsUnique()
            .AreNullsDistinct(false)
            .HasFilter("estado = 'ABIERTA'");
    }
}

internal sealed class FacturaConfiguracion : IEntityTypeConfiguration<Factura>
{
    public void Configure(EntityTypeBuilder<Factura> b)
    {
        b.ToTable("factura");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Folio).HasMaxLength(40);
        b.Property(x => x.MontoNeto).HasPrecision(18, 4);
        b.Property(x => x.MontoTotal).HasPrecision(18, 4);
        b.Property(x => x.Moneda).HasColumnType(Sql.Moneda);
        b.HasIndex(x => x.Folio).IsUnique();
        b.HasIndex(x => x.OfaId, "ix_factura_ofa");
        b.HasOne<Ofa>().WithMany().HasForeignKey(x => x.OfaId);
    }
}

internal sealed class PagoConfiguracion : IEntityTypeConfiguration<Pago>
{
    public void Configure(EntityTypeBuilder<Pago> b)
    {
        b.ToTable("pago");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.ReferenciaSap).HasMaxLength(40);
        b.Property(x => x.PagadorRut).HasMaxLength(12);
        b.Property(x => x.Monto).HasPrecision(18, 4);
        b.Property(x => x.RegistradoEn).HasDefaultValueSql(Sql.Ahora);
    }
}

internal sealed class PagoFacturaConfiguracion : IEntityTypeConfiguration<PagoFactura>
{
    public void Configure(EntityTypeBuilder<PagoFactura> b)
    {
        b.ToTable("pago_factura");
        b.HasKey(x => new { x.PagoId, x.FacturaId });
        b.Property(x => x.MontoAplicado).HasPrecision(18, 4);
        b.HasOne<Pago>().WithMany().HasForeignKey(x => x.PagoId);
        b.HasOne<Factura>().WithMany().HasForeignKey(x => x.FacturaId);
    }
}

internal sealed class EventoAuditoriaConfiguracion : IEntityTypeConfiguration<EventoAuditoria>
{
    public void Configure(EntityTypeBuilder<EventoAuditoria> b)
    {
        b.ToTable("evento_auditoria");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseSerialColumn();
        b.Property(x => x.ValorAnterior).HasColumnType(Sql.Json);
        b.Property(x => x.ValorNuevo).HasColumnType(Sql.Json);
        b.Property(x => x.OcurridoEn).HasDefaultValueSql(Sql.Ahora);
        b.HasIndex(x => new { x.Entidad, x.EntidadId }, "ix_auditoria_entidad");
        b.HasIndex(x => x.OcurridoEn, "ix_auditoria_fecha");
    }
}

internal sealed class OutboxMessageConfiguracion : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        b.ToTable("outbox_message");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Payload).HasColumnType(Sql.Json);
        b.Property(x => x.ClaveIdempotencia).HasMaxLength(80);
        b.Property(x => x.CreadoEn).HasDefaultValueSql(Sql.Ahora);
        b.HasIndex(x => new { x.Estado, x.CreadoEn }, "ix_outbox_pendiente");
    }
}
