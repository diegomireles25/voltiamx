using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Un aparato con su costo mensual (antes de impuestos), o null si no se puede calcular en pesos.
/// </summary>
public sealed record CostoDeAparato(int Id, string Nombre, CategoriaAparato Categoria, decimal KwhPorMes, decimal? CostoMensual);

/// <summary>
/// Aparatos ordenados por lo que cuestan. Sin consumo medido o sin tarifa no hay pesos
/// (<see cref="ConPesos"/> = false) y el orden es por consumo estimado.
/// </summary>
public sealed record CostosAparatos(bool ConPesos, IReadOnlyList<CostoDeAparato> Filas);

/// <summary>
/// Lo que Inicio, Aparatos y Metas necesitan de los aparatos de un lugar.
/// </summary>
public sealed record CostosDelLugar(RankingAparatos Ranking, CostosAparatos Costos, SugerenciasDeAhorro Sugerencias);

/// <summary>
/// Cuánto cuesta cada aparato al mes (D4): lo que dejarías de pagar si no lo usaras. Es un costo marginal:
/// los kWh del aparato se cobran a los precios de arriba del consumo, así que la suma de los costos no es el recibo.
/// </summary>
public static class CostoAparato
{
    /// <summary>
    /// (Total(consumo del periodo) − Total(consumo del periodo − kWh del aparato)) / meses, con el aparato
    /// limitado al consumo. Es el mismo cálculo del simulador de ahorro (<see cref="AhorroConTarifa.Pesos"/>).
    /// </summary>
    public static decimal CostoMensual(ITarifaCalculator calculadora, Tarifa tarifa, int mesesPorPeriodo, decimal consumoMes, decimal kwhAparatoMes)
    {
        var (pesosPeriodo, _, _) = AhorroConTarifa.Pesos(calculadora, tarifa, mesesPorPeriodo, consumoMes, kwhAparatoMes);
        return pesosPeriodo / Math.Max(mesesPorPeriodo, 1);
    }

    public static CostosAparatos Calcular(
        IEnumerable<Aparato> aparatos, ITarifaCalculator calculadora, Tarifa? tarifa, int mesesPorPeriodo, decimal? consumoMes)
    {
        ArgumentNullException.ThrowIfNull(aparatos);
        ArgumentNullException.ThrowIfNull(calculadora);
        var conPesos = tarifa is not null && consumoMes is > 0m;

        var filas = aparatos
            .Select(a =>
            {
                var kwh = ConsumoAparato.KwhPorMes(a);
                decimal? costo = conPesos ? CostoMensual(calculadora, tarifa!, mesesPorPeriodo, consumoMes!.Value, kwh) : null;
                return new CostoDeAparato(a.Id, a.Nombre, a.Categoria, kwh, costo);
            })
            .OrderByDescending(f => f.CostoMensual ?? 0m)
            .ThenByDescending(f => f.KwhPorMes)
            .ThenBy(f => f.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new CostosAparatos(conPesos, filas);
    }
}
