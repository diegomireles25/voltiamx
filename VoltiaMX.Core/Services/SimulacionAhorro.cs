using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Todo lo que necesita el simulador, ya leído de la base.
/// </summary>
/// <param name="KwhMedidoPorMes">Consumo mensual medido del lugar (<see cref="ConsumoMedido"/>), o null.</param>
/// <param name="HistorialMensual">Meses para el DAC (<see cref="HistorialMensualDac"/>); vacío si no hay.</param>
public sealed record EntradaSimulacion(
    Aparato Aparato,
    EscenarioAhorro Escenario,
    Tarifa Tarifa,
    int MesesPorPeriodo,
    decimal? KwhMedidoPorMes,
    IReadOnlyList<decimal> HistorialMensual);

public enum EstadoSimulacion
{
    Normal,

    /// <summary>
    /// Sin lecturas ni periodos: solo kWh, sin pesos.
    /// </summary>
    SinConsumoMedido,

    /// <summary>
    /// El ahorro del aparato es mayor que lo que mide el medidor: se limita a lo medido.
    /// </summary>
    AhorroExcedeConsumo,

    /// <summary>
    /// Con el cambio el aparato consumiría más: los pesos salen negativos y no hay recuperación.
    /// </summary>
    ConsumiriasMas,
}

/// <summary>
/// Resultado de un escenario. Los montos son sin IVA ni DAP; null cuando no se pueden calcular.
/// </summary>
public sealed record ResultadoSimulacion(
    decimal KwhPorMesAntes,
    decimal KwhPorMesDespues,
    decimal AhorroKwhPorMes,
    EstadoSimulacion Estado,
    decimal? AhorroPesosPeriodo,
    decimal? AhorroPesosMes,
    decimal? AhorroPesosAnio,
    decimal? MesesRecuperacion,
    bool RecuperacionMasDe10Anios,
    EvaluacionDac? DacAntes,
    EvaluacionDac? DacDespues);
