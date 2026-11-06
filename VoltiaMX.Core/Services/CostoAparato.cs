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
/// Lo que Inicio, Aparatos y Metas necesitan de los aparatos de un lugar. <see cref="Tarifa"/>,
/// <see cref="MesesPorPeriodo"/> y <see cref="ConsumoMedidoMes"/> sirven para calcular el costo de un aparato
/// que todavía se está escribiendo (vista previa del formulario). <see cref="ConsumoAproximado"/>: el consumo de
/// referencia sale de un recibo por monto, así que los costos también son aproximados (Fase 9d, D4).
/// </summary>
public sealed record CostosDelLugar(
    RankingAparatos Ranking,
    CostosAparatos Costos,
    SugerenciasDeAhorro Sugerencias,
    Tarifa? Tarifa = null,
    int MesesPorPeriodo = 1,
    decimal? ConsumoMedidoMes = null,
    bool ConsumoAproximado = false)
{
    /// <summary>
    /// Costo mensual de un aparato con la tarifa y el consumo del lugar; null si no se puede calcular en pesos.
    /// </summary>
    public decimal? CostoDe(ITarifaCalculator calculadora, decimal kwhPorMes)
        => Tarifa is not null && ConsumoMedidoMes is > 0m and decimal consumo
            ? CostoAparato.CostoMensual(calculadora, Tarifa, MesesPorPeriodo, consumo, kwhPorMes)
            : null;
}

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
