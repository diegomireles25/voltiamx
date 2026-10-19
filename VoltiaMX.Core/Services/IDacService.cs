namespace VoltiaMX.Core.Services;

public interface IDacService
{
    /// <summary>
    /// Evalúa el riesgo DAC con el promedio de los últimos 12 meses de
    /// <paramref name="consumosMensualesKwh"/> (del más antiguo al más reciente).
    /// Si hay menos de 12 meses, promedia los disponibles.
    /// </summary>
    EvaluacionDac Evaluar(IReadOnlyList<decimal> consumosMensualesKwh, decimal limiteKwhMensual);
}
