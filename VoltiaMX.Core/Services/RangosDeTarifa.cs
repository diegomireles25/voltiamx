using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

internal static class RangosDeTarifa
{
    /// <summary>
    /// Devuelve los rangos ordenados por <see cref="TarifaRango.Orden"/> y verifica que cubran
    /// todo el consumo sin huecos ni traslapes: el primero empieza en 0, cada uno empieza donde
    /// termina el anterior y solo el último queda sin límite.
    /// </summary>
    public static IReadOnlyList<TarifaRango> Validados(Tarifa tarifa)
    {
        var rangos = tarifa.Rangos.OrderBy(r => r.Orden).ToList();
        if (rangos.Count == 0)
        {
            throw new InvalidOperationException($"La tarifa '{tarifa.Nombre}' no tiene rangos.");
        }

        decimal? finAnterior = 0m;
        foreach (var rango in rangos)
        {
            if (finAnterior is null)
            {
                throw new InvalidOperationException(
                    $"La tarifa '{tarifa.Nombre}' tiene rangos después de uno sin límite.");
            }

            if (rango.KwhDesde != finAnterior || rango.KwhHasta <= rango.KwhDesde)
            {
                throw new InvalidOperationException(
                    $"El rango '{rango.Nombre}' de la tarifa '{tarifa.Nombre}' no continúa al anterior.");
            }

            finAnterior = rango.KwhHasta;
        }

        if (finAnterior is not null)
        {
            throw new InvalidOperationException(
                $"El último rango de la tarifa '{tarifa.Nombre}' debe quedar sin límite.");
        }

        return rangos;
    }
}
