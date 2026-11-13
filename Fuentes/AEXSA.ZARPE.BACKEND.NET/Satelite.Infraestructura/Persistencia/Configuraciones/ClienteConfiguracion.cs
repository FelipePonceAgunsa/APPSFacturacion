using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Satelite.Dominio.Entidades;

namespace Satelite.Infraestructura.Persistencia.Configuraciones;

internal sealed class ClienteConfiguracion : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> b)
    {
        b.ToTable("cliente");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql(Sql.Uuid);
        b.Property(x => x.Rut).HasMaxLength(12);
        b.HasIndex(x => x.Rut).IsUnique();
        b.Property(x => x.CardCodeSap).HasMaxLength(30);
        b.Property(x => x.ListaPreciosId).HasMaxLength(30);
        b.Property(x => x.CreadoEn).HasDefaultValueSql(Sql.Ahora);
    }
}

internal sealed class TipoServicioConfiguracion : IEntityTypeConfiguration<TipoServicio>
{
    public void Configure(EntityTypeBuilder<TipoServicio> b)
    {
        b.ToTable("tipo_servicio");
        b.HasKey(x => x.TipoServicioId);
        b.Property(x => x.TipoServicioId).ValueGeneratedNever();
        b.Property(x => x.Codigo).HasMaxLength(20);
    }
}

/// <summary>Fragmentos SQL comunes de los valores por defecto de la base.</summary>
internal static class Sql
{
    public const string Uuid = "gen_random_uuid()";
    public const string Ahora = "now()";
    public const string Moneda = "character(3)";
    public const string Json = "jsonb";
}
