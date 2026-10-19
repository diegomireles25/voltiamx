using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class TarifaCalculator : ITarifaCalculator
{
    public ResultadoCalculo Calcular(decimal kwh, Tarifa tarifa, int mesesDelPeriodo = 1)
    {
        ArgumentNullException.ThrowIfNull(tarifa);
        ArgumentOutOfRangeException.ThrowIfNegative(kwh);
        ArgumentOutOfRangeException.ThrowIfLessThan(mesesDelPeriodo, 1);

        var desglose = new List<DesgloseRango>();
        foreach (var rango in RangosDeTarifa.Validados(tarifa))
        {
            var desde = rango.KwhDesde * mesesDelPeriodo;
            if (kwh <= desde)
            {
                break;
            }

            var hasta = rango.KwhHasta is { } tope ? Math.Min(kwh, tope * mesesDelPeriodo) : kwh;
            var kwhEnRango = hasta - desde;
            desglose.Add(new DesgloseRango(rango.Nombre, kwhEnRango, rango.PrecioPorKwh, kwhEnRango * rango.PrecioPorKwh));
        }

        var subtotalEnergia = desglose.Sum(r => r.Subtotal);
        var cargoFijo = tarifa.CargoFijoMensual * mesesDelPeriodo;
        return new ResultadoCalculo(desglose, subtotalEnergia, cargoFijo, subtotalEnergia + cargoFijo);
    }
}
