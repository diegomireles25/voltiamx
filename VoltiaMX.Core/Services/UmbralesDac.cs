namespace VoltiaMX.Core.Services;

/// <summary>
/// Fracción del límite DAC (promedio / límite) a partir de la cual el riesgo es Medio o Alto.
/// Es una heurística de la app, no un dato de CFE.
/// </summary>
public sealed record UmbralesDac(decimal Medio = 0.80m, decimal Alto = 0.95m);
