using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface ITarifaCalculator
{
    /// <summary>
    /// Calcula el importe de <paramref name="kwh"/> con los rangos de la tarifa.
    /// Los límites de los rangos y el cargo fijo son mensuales y se multiplican por
    /// <paramref name="mesesDelPeriodo"/> (1 = mensual, 2 = bimestral).
    /// </summary>
    ResultadoCalculo Calcular(decimal kwh, Tarifa tarifa, int mesesDelPeriodo = 1);
}
