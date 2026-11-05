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
/// negativa si se consumió más). <see cref="Monto"/> es su consumo con la tarifa actual (null sin tarifa).
/// </summary>
public sealed record FilaHistorialMeta(DateOnly Inicio, DateOnly Fin, decimal Kwh, decimal Reduccion, bool Cumplio, decimal? Monto = null);

/// <summary>
/// Progreso de la meta en el periodo actual, en kWh y, con tarifa, en pesos (con la tarifa actual, antes de
/// impuestos). Lo que no se puede calcular queda en null.
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
    int Racha,
    decimal? BasePesos = null,
    decimal? ObjetivoPesos = null,
    decimal? ConsumidoPesos = null,
    decimal? MontoARitmo = null)
{
    /// <summary>
    /// Cuánto te falta bajar para que el recibo quede en el objetivo (D8); nunca negativo. Null sin pesos.
    /// </summary>
    public decimal? FaltaPorAhorrar => MontoARitmo is decimal ritmo && ObjetivoPesos is decimal objetivo
        ? Math.Max(0m, ritmo - objetivo)
        : null;
}
