using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

/// <summary>Mapeo de un terminal de AEXSA al grupo que usa la regla del directo (RN-079). Tabla <c>terminal_mapa</c>.</summary>
public class TerminalMapa
{
    public int TerminalAexsaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public GrupoTerminal Grupo { get; set; } = GrupoTerminal.SIN_MAPEAR;
    public string? ActualizadoPor { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
}

/// <summary>Mapeo de un tipo de inspección de AEXSA a su grupo ([E1-7]). Tabla <c>tipo_inspeccion_mapa</c>.</summary>
public class TipoInspeccionMapa
{
    public string CodigoAexsa { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public GrupoInspeccion Grupo { get; set; } = GrupoInspeccion.SIN_MAPEAR;
    public string? ActualizadoPor { get; set; }
    public DateTimeOffset ActualizadoEn { get; set; }
}
