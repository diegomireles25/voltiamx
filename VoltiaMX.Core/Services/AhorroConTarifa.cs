using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Cuánto vale en pesos (con la tarifa real, sin IVA ni DAP) y en riesgo DAC dejar de consumir unos kWh al mes.
/// Lo usan el simulador de ahorro y el simulador solar.
/// </summary>
public static class AhorroConTarifa
{
    /// <summary>
    /// El ahorro vale lo que cuestan los kWh de arriba del consumo: se calcula el periodo completo dos veces,
    /// con y sin el ahorro. Si el ahorro del periodo pasa del consumo, se limita al consumo.
    /// </summary>
    /// <returns>Pesos del periodo, el ahorro mensual que sí se aplicó y si hubo que limitarlo.</returns>
    public static (decimal PesosPeriodo, decimal AhorroAplicadoMes, bool Excede) Pesos(
        ITarifaCalculator calculadora, Tarifa tarifa, int mesesPorPeriodo, decimal consumoMes, decimal ahorroMes)
    {
        ArgumentNullException.ThrowIfNull(calculadora);
        ArgumentNullException.ThrowIfNull(tarifa);
        var meses = Math.Max(mesesPorPeriodo, 1);
        var consumoPeriodo = consumoMes * meses;
        var ahorroPeriodo = ahorroMes * meses;
        var aplicadoMes = ahorroMes;
        var excede = false;
        if (ahorroPeriodo > consumoPeriodo)
        {
            ahorroPeriodo = consumoPeriodo;
            aplicadoMes = consumoPeriodo / meses;
            excede = true;
        }

        var pesos = calculadora.Calcular(consumoPeriodo, tarifa, meses).Total
            - calculadora.Calcular(consumoPeriodo - ahorroPeriodo, tarifa, meses).Total;
        return (pesos, aplicadoMes, excede);
    }

    /// <summary>
    /// Mismo historial y misma evaluación que el Dashboard, con el ahorro restado a cada mes. Null si la tarifa
    /// no tiene límite DAC o no hay historial.
    /// </summary>
    public static (EvaluacionDac? Antes, EvaluacionDac? Despues) EfectoDac(
        IDacService dac, Tarifa tarifa, IReadOnlyList<decimal> historialMensual, decimal ahorroMes)
    {
        ArgumentNullException.ThrowIfNull(dac);
        ArgumentNullException.ThrowIfNull(tarifa);
        ArgumentNullException.ThrowIfNull(historialMensual);
        if (tarifa.LimiteDacKwhMensual is not decimal limite || historialMensual.Count == 0)
        {
            return (null, null);
        }

        var despues = historialMensual.Select(kwh => Math.Max(0m, kwh - ahorroMes)).ToList();
        return (dac.Evaluar(historialMensual, limite), dac.Evaluar(despues, limite));
    }
}
