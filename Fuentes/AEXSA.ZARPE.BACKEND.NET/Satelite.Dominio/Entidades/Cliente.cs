using Satelite.Dominio.Comun;

namespace Satelite.Dominio.Entidades;

/// <summary>Cliente identificado por RUT (BR-020). Tabla <c>cliente</c>.</summary>
public class Cliente
{
    public Guid Id { get; set; }
    public string Rut { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? CardCodeSap { get; set; }
    public CondicionPago? CondicionPago { get; set; }
    public int? DiasCredito { get; set; }
    public string? ListaPreciosId { get; set; }
    public DateTimeOffset CreadoEn { get; set; }
}
