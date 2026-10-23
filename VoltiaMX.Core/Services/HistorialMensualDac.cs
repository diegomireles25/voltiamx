using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Consumo mensual con el que se evalúa el DAC: cada periodo cerrado se reparte en partes iguales
/// entre sus meses y cuentan los últimos 12. Lo usan el Dashboard y el simulador de ahorro.
/// </summary>
public static class HistorialMensualDac
{
    public const int MesesDelHistorial = 12;

    /// <returns>Del mes más antiguo al más reciente; vacío si no hay periodos cerrados.</returns>
    public static IReadOnlyList<decimal> Desde(IEnumerable<Periodo> cerrados, int meses)
    {
        ArgumentNullException.ThrowIfNull(cerrados);
        ArgumentOutOfRangeException.ThrowIfLessThan(meses, 1);

        return cerrados
            .OrderBy(p => p.Inicio)
            .SelectMany(p => Enumerable.Repeat(p.KwhTotales / meses, meses))
            .TakeLast(MesesDelHistorial)
            .ToList();
    }
}
