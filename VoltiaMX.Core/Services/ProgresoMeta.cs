namespace VoltiaMX.Core.Services;

public enum EstadoMeta
{
    /// <summary>
    /// La proyección al cierre queda dentro del objetivo.
    /// </summary>
    Cumpliendo,

    /// <summary>
    /// Todavía no se pasa del objetivo, pero la proyección al cierre sí.
    /// </summary>
    EnRiesgo,

    /// <summary>
    /// Lo consumido hasta hoy ya pasó el objetivo del periodo.
    /// </summary>
    Superada,

    /// <summary>
    /// No hay periodo cerrado contra el cual comparar.
    /// </summary>
    SinBase,

    /// <summary>
    /// El periodo actual no tiene lectura base: no se sabe cuánto va.
    /// </summary>
    SinConsumoMedido,

    Inactiva,
}

public enum CodigoErrorMeta
{
    PorcentajeFueraDeRango,
}

public sealed record ResultadoGuardarMeta(IReadOnlyList<CodigoErrorMeta> Errores);

/// <summary>
/// Un periodo cerrado contra el anterior. <see cref="Reduccion"/> es la fracción (0.10 = 10 % menos;
/// negativa si se consumió más).
/// </summary>
public sealed record FilaHistorialMeta(DateOnly Inicio, DateOnly Fin, decimal Kwh, decimal Reduccion, bool Cumplio);

/// <summary>
/// Progreso de la meta en el periodo actual, en kWh. Lo que no se puede calcular queda en null.
/// <see cref="AhorroPesosObjetivo"/> es sin IVA ni DAP. <see cref="Historial"/> va del más reciente al más antiguo.
/// </summary>
public sealed record ProgresoMeta(
    EstadoMeta Estado,
    int PorcentajeObjetivo,
    decimal? BaseKwh,
    bool BaseEstimada,
    decimal? ObjetivoKwh,
    decimal? ConsumidoKwh,
    decimal? ProyectadoKwh,
    decimal? KwhPorDiaPermitidos,
    decimal? AhorroPesosObjetivo,
    IReadOnlyList<FilaHistorialMeta> Historial,
    int Racha);
