using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// "Usa {aparato} 1 hora menos al día": cuánto ahorrarías al mes (antes de impuestos).
/// </summary>
public sealed record SugerenciaAhorro(int AparatoId, string Nombre, decimal HorasPorDia, decimal AhorroMensual);

/// <summary>
/// Hasta <see cref="SugerenciasAhorro.MaximoSugerencias"/> sugerencias de mayor a menor y el ahorro de hacerlas
/// todas juntas (null sin sugerencias).
/// </summary>
public sealed record SugerenciasDeAhorro(IReadOnlyList<SugerenciaAhorro> Sugerencias, decimal? AhorroCombinadoMensual)
{
    public static SugerenciasDeAhorro Ninguna { get; } = new([], null);
}

/// <summary>
/// Sugerencias calculadas con los aparatos y la tarifa del lugar (D5); nada de consejos genéricos.
/// </summary>
public static class SugerenciasAhorro
{
    /// <summary>
    /// Con menos de 2 horas al día, quitarle una hora es casi no usarlo.
    /// </summary>
    public const decimal HorasMinimas = 2m;

    public const decimal HorasMenos = 1m;

    /// <summary>
    /// Ahorros más chicos no valen la pena como sugerencia.
    /// </summary>
    public const decimal AhorroMinimoMensual = 10m;

    public const int MaximoSugerencias = 3;

    /// <summary>
    /// El mismo escenario del simulador de ahorro: una hora menos al día, los mismos días.
    /// </summary>
    public static EscenarioAhorro Escenario(Aparato aparato)
    {
        ArgumentNullException.ThrowIfNull(aparato);
        return EscenarioAhorro.MenosHoras(aparato.HorasPorDia - HorasMenos, aparato.DiasPorSemana);
    }

    public static SugerenciasDeAhorro Generar(
        IEnumerable<Aparato> aparatos, ITarifaCalculator calculadora, Tarifa? tarifa, int mesesPorPeriodo, decimal? consumoMes)
    {
        ArgumentNullException.ThrowIfNull(aparatos);
        ArgumentNullException.ThrowIfNull(calculadora);
        if (tarifa is null || consumoMes is not > 0m || consumoMes is not decimal consumo)
        {
            return SugerenciasDeAhorro.Ninguna;
        }

        var meses = Math.Max(mesesPorPeriodo, 1);
        var elegidas = aparatos
            .Where(a => a.HorasPorDia >= HorasMinimas && a.Categoria != CategoriaAparato.Refrigeracion)
            .Select(a =>
            {
                var menosKwh = ConsumoAparato.KwhPorMes(a) - ConsumoAparato.KwhPorMes(Escenario(a).Aplicar(a));
                var (pesos, _, _) = AhorroConTarifa.Pesos(calculadora, tarifa, meses, consumo, menosKwh);
                return (Sugerencia: new SugerenciaAhorro(a.Id, a.Nombre, a.HorasPorDia, pesos / meses), MenosKwh: menosKwh);
            })
            .Where(x => x.Sugerencia.AhorroMensual >= AhorroMinimoMensual)
            .OrderByDescending(x => x.Sugerencia.AhorroMensual)
            .ThenBy(x => x.Sugerencia.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaximoSugerencias)
            .ToList();

        if (elegidas.Count == 0)
        {
            return SugerenciasDeAhorro.Ninguna;
        }

        // Juntas, en un solo cálculo: los kWh ahorrados bajan por los tramos y valen menos que sumados sueltos.
        var (combinado, _, _) = AhorroConTarifa.Pesos(calculadora, tarifa, meses, consumo, elegidas.Sum(x => x.MenosKwh));
        return new SugerenciasDeAhorro([.. elegidas.Select(x => x.Sugerencia)], combinado / meses);
    }
}
