using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class PresupuestoInversoService(ITarifaCalculator calculadora) : IPresupuestoInversoService
{
    public ResultadoPresupuesto Calcular(
        decimal montoMaximo,
        Tarifa tarifa,
        int diasDelPeriodo,
        int mesesDelPeriodo = 1,
        decimal kwhConsumidos = 0m,
        int diasTranscurridos = 0)
    {
        ArgumentNullException.ThrowIfNull(tarifa);
        ArgumentOutOfRangeException.ThrowIfNegative(montoMaximo);
        ArgumentOutOfRangeException.ThrowIfLessThan(diasDelPeriodo, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(mesesDelPeriodo, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(kwhConsumidos);
        ArgumentOutOfRangeException.ThrowIfNegative(diasTranscurridos);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(diasTranscurridos, diasDelPeriodo);

        var rangos = RangosDeTarifa.Validados(tarifa);
        if (rangos.Any(r => r.PrecioPorKwh <= 0m))
        {
            throw new InvalidOperationException(
                $"La tarifa '{tarifa.Nombre}' tiene rangos sin precio; el presupuesto no tendría tope de kWh.");
        }

        var cubreCargoFijo = montoMaximo >= tarifa.CargoFijoMensual * mesesDelPeriodo;
        var kwhMaximos = cubreCargoFijo ? KwhMaximos(montoMaximo, tarifa, rangos, mesesDelPeriodo) : 0m;
        var kwhRestantes = Math.Max(0m, kwhMaximos - kwhConsumidos);
        var diasRestantes = diasDelPeriodo - diasTranscurridos;

        return new ResultadoPresupuesto(
            kwhMaximos,
            PorDia(kwhMaximos, diasDelPeriodo),
            cubreCargoFijo,
            kwhRestantes,
            diasRestantes > 0 ? PorDia(kwhRestantes, diasRestantes) : 0m);
    }

    private decimal KwhMaximos(decimal montoMaximo, Tarifa tarifa, IReadOnlyList<TarifaRango> rangos, int meses)
    {
        // Compra kWh rango por rango con lo que queda después del cargo fijo.
        var disponible = montoMaximo - tarifa.CargoFijoMensual * meses;
        var kwh = 0m;
        foreach (var rango in rangos)
        {
            if (rango.KwhHasta is { } hasta)
            {
                var capacidad = (hasta - rango.KwhDesde) * meses;
                var costo = capacidad * rango.PrecioPorKwh;
                if (disponible >= costo)
                {
                    kwh += capacidad;
                    disponible -= costo;
                    continue;
                }
            }

            kwh += disponible / rango.PrecioPorKwh;
            break;
        }

        // La división puede dejar residuos de redondeo; se ajusta con el calculador para que
        // k quepa en el presupuesto y k + 1 ya no.
        var k = Math.Floor(kwh);
        while (k > 0m && calculadora.Calcular(k, tarifa, meses).Total > montoMaximo)
        {
            k--;
        }

        while (calculadora.Calcular(k + 1m, tarifa, meses).Total <= montoMaximo)
        {
            k++;
        }

        return k;
    }

    private static decimal PorDia(decimal kwh, int dias) => Math.Round(kwh / dias, 2, MidpointRounding.ToZero);
}
