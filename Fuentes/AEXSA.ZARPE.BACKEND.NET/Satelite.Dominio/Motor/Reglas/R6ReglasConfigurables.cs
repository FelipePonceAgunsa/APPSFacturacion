namespace Satelite.Dominio.Motor.Reglas;

/// <summary>
/// Regla 6 — Reglas configurables (ADR-008, nivel 2).
/// Paso vacío: nivel 2 fuera de alcance etapa 1. Se mantiene en el pipeline para respetar el orden de §1.
/// </summary>
internal static class R6ReglasConfigurables
{
    public static void Aplicar(ContextoCalculo ctx, EstadoLinea e)
    {
        // Nivel 2 fuera de alcance etapa 1: no evalúa reglas ni registra nada en la memoria.
    }
}
