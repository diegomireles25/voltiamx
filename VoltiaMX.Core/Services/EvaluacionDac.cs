namespace VoltiaMX.Core.Services;

public enum NivelRiesgoDac
{
    Bajo,
    Medio,
    Alto
}

/// <summary>
/// Qué tan cerca está un lugar de pasar a tarifa DAC.
/// </summary>
/// <param name="MargenKwh">
/// kWh mensuales promedio que faltan para el límite; negativo si ya se rebasó.
/// </param>
public sealed record EvaluacionDac(
    NivelRiesgoDac Nivel,
    decimal PromedioMensualKwh,
    decimal MargenKwh,
    bool ExcedeLimite);
