using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class RankingAparatosTests
{
    private static int _siguienteId;

    // 1 kW × 24 h × 7 días × 52/12 = 728 kWh al mes con ciclo 1; el ciclo da números exactos:
    // 0.25 → 182 kWh, 0.125 → 91 kWh.
    private static Aparato Aparato(string nombre, decimal ciclo) => new()
    {
        Id = ++_siguienteId,
        Nombre = nombre,
        Categoria = CategoriaAparato.Otro,
        Cantidad = 1,
        Watts = 1_000m,
        HorasPorDia = 24m,
        DiasPorSemana = 7,
        CicloDeTrabajo = ciclo,
    };

    [Fact]
    public void Calcular_CuentaAMano_PorcentajeContraElMedidoYSinIdentificar()
    {
        // Medido 364 kWh al mes: 182 / 364 = 50 %, 91 / 364 = 25 %, sin identificar 100 - 75 = 25 %.
        var ranking = RankingAparatos.Calcular([Aparato("Lavadora", 0.125m), Aparato("Aire", 0.25m)], 364m);

        Assert.Equal(EstadoRanking.Normal, ranking.Estado);
        Assert.Equal(["Aire", "Lavadora"], ranking.Filas.Select(f => f.Nombre));
        Assert.Equal([182m, 91m], ranking.Filas.Select(f => Math.Round(f.KwhPorMes, 4)));
        Assert.Equal([0.5m, 0.25m], ranking.Filas.Select(f => Math.Round(f.Porcentaje!.Value, 4)));
        Assert.Equal(0.25m, Math.Round(ranking.SinIdentificar!.Value, 4));
        Assert.Equal(0.75m, Math.Round(ranking.Cobertura!.Value, 4));
        Assert.Equal(364m, ranking.KwhMedidoPorMes);
    }

    [Fact]
    public void Calcular_ElPorcentajeEsContraElMedidoNoContraLaSuma()
    {
        // Contra la suma (273) el aire sería 66.7 %; contra el medido (364) es 50 %.
        var ranking = RankingAparatos.Calcular([Aparato("Aire", 0.25m), Aparato("Lavadora", 0.125m)], 364m);

        Assert.Equal(0.5m, Math.Round(ranking.Filas[0].Porcentaje!.Value, 4));
    }

    [Fact]
    public void Calcular_OrdenaDeMayorAMenorYEmpataPorNombre()
    {
        var ranking = RankingAparatos.Calcular(
            [Aparato("Foco", 0.05m), Aparato("Refri", 0.125m), Aparato("Bomba", 0.125m), Aparato("Aire", 0.25m)], 1_000m);

        Assert.Equal(["Aire", "Bomba", "Refri", "Foco"], ranking.Filas.Select(f => f.Nombre));
    }

    [Fact]
    public void Calcular_SinAparatos_EstadoSinAparatos()
    {
        var ranking = RankingAparatos.Calcular([], 364m);

        Assert.Equal(EstadoRanking.SinAparatos, ranking.Estado);
        Assert.Empty(ranking.Filas);
    }

    [Fact]
    public void Calcular_SinAparatosNiConsumo_SinAparatosManda()
    {
        Assert.Equal(EstadoRanking.SinAparatos, RankingAparatos.Calcular([], null).Estado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Calcular_SinConsumoMedido_KwhSinPorcentajes(int? medido)
    {
        var ranking = RankingAparatos.Calcular([Aparato("Lavadora", 0.125m), Aparato("Aire", 0.25m)], medido);

        Assert.Equal(EstadoRanking.SinConsumoMedido, ranking.Estado);
        Assert.Equal(["Aire", "Lavadora"], ranking.Filas.Select(f => f.Nombre));
        Assert.All(ranking.Filas, f => Assert.Null(f.Porcentaje));
        Assert.Null(ranking.SinIdentificar);
        Assert.Null(ranking.Cobertura);
    }

    [Fact]
    public void Calcular_SumaMayorQue105PorCiento_SumaExcedeYSinIdentificarEnCero()
    {
        // 273 / 250 = 109.2 %.
        var ranking = RankingAparatos.Calcular([Aparato("Aire", 0.25m), Aparato("Lavadora", 0.125m)], 250m);

        Assert.Equal(EstadoRanking.SumaExcedeMedido, ranking.Estado);
        Assert.Equal(0m, ranking.SinIdentificar);
        Assert.Equal(1.092m, Math.Round(ranking.Cobertura!.Value, 4));
    }

    [Fact]
    public void Calcular_SumaJustoEn105PorCiento_SigueNormal()
    {
        // 273 / 260 = 105 % exacto: el aviso es solo para más de 105 %.
        var ranking = RankingAparatos.Calcular([Aparato("Aire", 0.25m), Aparato("Lavadora", 0.125m)], 260m);

        Assert.Equal(EstadoRanking.Normal, ranking.Estado);
        Assert.Equal(0m, ranking.SinIdentificar);
    }

    [Fact]
    public void Calcular_SumaEntre100Y105_NormalYSinIdentificarEnCero()
    {
        // 273 / 265 = 103 %.
        var ranking = RankingAparatos.Calcular([Aparato("Aire", 0.25m), Aparato("Lavadora", 0.125m)], 265m);

        Assert.Equal(EstadoRanking.Normal, ranking.Estado);
        Assert.Equal(0m, ranking.SinIdentificar);
    }
}
