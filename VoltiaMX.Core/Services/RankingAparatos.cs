using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum EstadoRanking
{
    Normal,

    /// <summary>
    /// Sin lecturas ni periodos (o con consumo 0): solo kWh por aparato, sin porcentajes.
    /// </summary>
    SinConsumoMedido,

    /// <summary>
    /// Los aparatos suman más del 105 % de lo que mide el medidor: hay potencias u horas de más.
    /// </summary>
    SumaExcedeMedido,

    SinAparatos,
}

/// <summary>
/// Aparato en el ranking. <see cref="Porcentaje"/> es la fracción del consumo medido (0.46 = 46 %),
/// o null sin consumo medido.
/// </summary>
public sealed record FilaRanking(int Id, string Nombre, CategoriaAparato Categoria, decimal KwhPorMes, decimal? Porcentaje);

/// <summary>
/// Qué gasta más, contra lo que mide el medidor y no contra la suma de los aparatos.
/// <see cref="SinIdentificar"/> es lo medido que ningún aparato explica (nunca negativo) y
/// <see cref="Cobertura"/>, la suma de los porcentajes.
/// </summary>
public sealed record RankingAparatos(
    EstadoRanking Estado,
    IReadOnlyList<FilaRanking> Filas,
    decimal? KwhMedidoPorMes,
    decimal? SinIdentificar,
    decimal? Cobertura)
{
    /// <summary>
    /// Hasta 5 % de más se toma como diferencia normal de estimación; arriba de eso se avisa.
    /// </summary>
    public const decimal ToleranciaSobreMedido = 1.05m;

    public static RankingAparatos Calcular(IEnumerable<Aparato> aparatos, decimal? kwhMedidoPorMes)
    {
        ArgumentNullException.ThrowIfNull(aparatos);
        var medido = kwhMedidoPorMes is > 0m ? kwhMedidoPorMes : null;

        var filas = aparatos
            .Select(a => (Aparato: a, Kwh: ConsumoAparato.KwhPorMes(a)))
            .OrderByDescending(x => x.Kwh)
            .ThenBy(x => x.Aparato.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .Select(x => new FilaRanking(x.Aparato.Id, x.Aparato.Nombre, x.Aparato.Categoria, x.Kwh, x.Kwh / medido))
            .ToList();

        if (filas.Count == 0)
        {
            return new RankingAparatos(EstadoRanking.SinAparatos, filas, kwhMedidoPorMes, null, null);
        }

        if (medido is null)
        {
            return new RankingAparatos(EstadoRanking.SinConsumoMedido, filas, kwhMedidoPorMes, null, null);
        }

        var cobertura = filas.Sum(f => f.Porcentaje!.Value);
        return new RankingAparatos(
            cobertura > ToleranciaSobreMedido ? EstadoRanking.SumaExcedeMedido : EstadoRanking.Normal,
            filas,
            kwhMedidoPorMes,
            Math.Max(0m, 1m - cobertura),
            cobertura);
    }
}
