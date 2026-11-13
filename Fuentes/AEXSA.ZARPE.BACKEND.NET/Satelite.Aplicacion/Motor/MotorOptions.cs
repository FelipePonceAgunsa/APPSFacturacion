namespace Satelite.Aplicacion.Motor;

/// <summary>
/// Configuración del cálculo (sección <c>Motor</c>). La lee el cálculo de la OFA (bloque 5).
/// No tiene valores en appsettings.json: los ids de tipo_servicio dependen de cada ambiente.
/// </summary>
public sealed class MotorOptions
{
    public const string Seccion = "Motor";

    public ConceptosDirectoOptions ConceptosDirecto { get; set; } = new();
}

/// <summary>
/// tipo_servicio_id de los conceptos de la Regla 0 (directo sin convenio, RN-079).
/// [SUPUESTO: en Development son los IDs de los datos de prueba (db/semillas/01-tipo-servicio.sql)]
/// </summary>
public sealed class ConceptosDirectoOptions
{
    public int? Visacion { get; set; }
    public int? SecurityFee { get; set; }
    public int? Refacturacion { get; set; }
    public int? Recepcion { get; set; }
    public int? Despacho { get; set; }
    public int? Movilizacion { get; set; }
}
