using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

internal static class RangosDeTarifa
{
    /// <summary>
    /// Devuelve los rangos ordenados por <see cref="TarifaRango.Orden"/> y verifica que cubran
    /// todo el consumo sin huecos ni traslapes con la misma regla del editor de tarifas
    /// (<see cref="TarifaValidador.RevisarRangos"/>): el primero empieza en 0, cada uno empieza donde
    /// termina el anterior y solo el último queda sin límite.
    /// </summary>
    public static IReadOnlyList<TarifaRango> Validados(Tarifa tarifa)
    {
        var rangos = tarifa.Rangos.OrderBy(r => r.Orden).ToList();
        if (rangos.Count == 0)
        {
            throw new InvalidOperationException($"La tarifa '{tarifa.Nombre}' no tiene rangos.");
        }

        if (TarifaValidador.RevisarRangos(rangos) is [var error, ..])
        {
            throw new InvalidOperationException(
                $"Los rangos de la tarifa '{tarifa.Nombre}' no son válidos: {error.Codigo} en el rango {error.Rango}.");
        }

        return rangos;
    }
}
