using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

/// <summary>Catálogo normalizado de servicios (MasterData.TipoServicio de AEXSA, RN-032). Tabla <c>tipo_servicio</c>.</summary>
public class TipoServicio
{
    public int TipoServicioId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public UnidadCobro UnidadNormalizada { get; set; }
    public string? UnidadOrigen { get; set; }
    public bool Habilitado { get; set; } = true;
}
