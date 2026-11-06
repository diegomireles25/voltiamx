using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Un grupo de aparatos con el mismo nombre base: "Aire acondicionado · 3" con 3 unidades, o el nombre de siempre si
/// es uno solo. <see cref="CostoMensual"/> es el costo marginal del grupo (null sin pesos).
/// </summary>
public sealed record GrupoAparatos(string Nombre, string NombreBase, int Registros, IReadOnlyList<int> Ids, decimal KwhPorMes, decimal? CostoMensual);

/// <summary>
/// Fase 9g (D3): agrupa las unidades de un aparato por su nombre base (el texto antes de " · "). El consumo del grupo
/// es la suma de sus unidades y su costo, el costo marginal de ese consumo total con
/// <see cref="CostoAparato.CostoMensual"/>: no es la suma de los costos de cada unidad, que no coincide por los tramos.
/// </summary>
public static class GruposDeAparatos
{
    public static IReadOnlyList<GrupoAparatos> Agrupar(
        IEnumerable<CostoDeAparato> filas, ITarifaCalculator calculadora, Tarifa? tarifa, int mesesPorPeriodo, decimal? consumoMes)
    {
        ArgumentNullException.ThrowIfNull(filas);
        ArgumentNullException.ThrowIfNull(calculadora);
        var conPesos = tarifa is not null && consumoMes is > 0m;

        return filas
            .GroupBy(f => NombreBase(f.Nombre), StringComparer.CurrentCultureIgnoreCase)
            .Select(g =>
            {
                var lista = g.ToList();
                var kwh = lista.Sum(f => f.KwhPorMes);
                if (lista.Count == 1)
                {
                    // Uno solo: su nombre y su cifra de siempre.
                    return new GrupoAparatos(lista[0].Nombre, g.Key, 1, [lista[0].Id], kwh, lista[0].CostoMensual);
                }

                decimal? costo = conPesos ? CostoAparato.CostoMensual(calculadora, tarifa!, mesesPorPeriodo, consumoMes!.Value, kwh) : null;
                return new GrupoAparatos(
                    NombreBase(lista[0].Nombre) + AparatosTipicos.SeparadorUnidad + lista.Count,
                    g.Key,
                    lista.Count,
                    lista.Select(f => f.Id).ToList(),
                    kwh,
                    costo);
            })
            .OrderByDescending(g => g.CostoMensual ?? 0m)
            .ThenByDescending(g => g.KwhPorMes)
            .ThenBy(g => g.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// El texto antes del primer " · " (o el nombre completo).
    /// </summary>
    public static string NombreBase(string nombre)
    {
        var i = nombre.IndexOf(AparatosTipicos.SeparadorUnidad, StringComparison.Ordinal);
        return (i < 0 ? nombre : nombre[..i]).Trim();
    }
}
