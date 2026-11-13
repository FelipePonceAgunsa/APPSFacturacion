using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia.Configuraciones;

internal sealed class TerminalMapaConfiguracion : IEntityTypeConfiguration<TerminalMapa>
{
    public void Configure(EntityTypeBuilder<TerminalMapa> b)
    {
        b.ToTable("terminal_mapa");
        b.HasKey(x => x.TerminalAexsaId);
        b.Property(x => x.TerminalAexsaId).ValueGeneratedNever();
        b.Property(x => x.ActualizadoPor).HasMaxLength(80);
        b.Property(x => x.ActualizadoEn).HasDefaultValueSql(Sql.Ahora);
    }
}

internal sealed class TipoInspeccionMapaConfiguracion : IEntityTypeConfiguration<TipoInspeccionMapa>
{
    public void Configure(EntityTypeBuilder<TipoInspeccionMapa> b)
    {
        b.ToTable("tipo_inspeccion_mapa");
        b.HasKey(x => x.CodigoAexsa);
        b.Property(x => x.CodigoAexsa).HasMaxLength(40);
        b.Property(x => x.ActualizadoPor).HasMaxLength(80);
        b.Property(x => x.ActualizadoEn).HasDefaultValueSql(Sql.Ahora);
    }
}
