using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Sugerencias "usarlo 1 hora menos al día" (D5). Tarifa de tres tramos (0–100 a $1, 100–200 a $2,
/// 200+ a $4) y 250 kWh al mes: los primeros 50 kWh que se ahorran valen $4 cada uno y los siguientes $2.
/// Con 3 días a la semana (13 días al mes), 1,000 W una hora menos al día son 13 kWh menos al mes.
/// </summary>
public sealed class SugerenciasAhorroTests
{
    private static readonly TarifaCalculator Calculadora = new();
    private static readonly Tarifa Tarifa = TarifasDePrueba.TresTramos();

    private static Aparato Aparato(int id, string nombre, decimal watts, decimal horas, CategoriaAparato categoria = CategoriaAparato.Otro)
        => new() { Id = id, Nombre = nombre, Categoria = categoria, Watts = watts, HorasPorDia = horas, DiasPorSemana = 3 };

    // Aire 2,500 W: 32.5 kWh menos → $130. Lavadora 1,500 W: 19.5 kWh → $78. Bomba 400 W: 5.2 kWh → $20.80.
    // Tele 300 W: 3.9 kWh → $15.60. Foco 100 W: 1.3 kWh → $5.20 (menos de $10).
    private static readonly Aparato[] Casa =
    [
        Aparato(1, "Tele", 300m, 5m),
        Aparato(2, "Aire", 2_500m, 8m),
        Aparato(3, "Foco", 100m, 4m),
        Aparato(4, "Lavadora", 1_500m, 3m),
        Aparato(5, "Bomba", 400m, 2m),
        Aparato(6, "Refrigerador", 1_000m, 24m, CategoriaAparato.Refrigeracion),
        Aparato(7, "Plancha", 2_000m, 1m),
    ];

    private static SugerenciasDeAhorro Generar(IEnumerable<Aparato> aparatos, decimal? consumo = 250m, Tarifa? tarifa = null)
        => SugerenciasAhorro.Generar(aparatos, Calculadora, tarifa ?? Tarifa, 1, consumo);

    [Fact]
    public void SinAparatos_NoHaySugerencias()
    {
        var r = Generar([]);

        Assert.Empty(r.Sugerencias);
        Assert.Null(r.AhorroCombinadoMensual);
    }

    [Fact]
    public void TresMejores_DeMayorAMenor_SinRefrigeradorNiAparatosDeUnaHoraNiAhorrosChicos()
    {
        var r = Generar(Casa);

        // La tele ($15.60) queda fuera por el máximo de 3; el foco ($5.20) por el mínimo de $10; el refrigerador
        // por su categoría y la plancha porque solo se usa 1 hora.
        Assert.Equal(["Aire", "Lavadora", "Bomba"], r.Sugerencias.Select(s => s.Nombre));
        Assert.Equal([130m, 78m, 20.8m], r.Sugerencias.Select(s => Math.Round(s.AhorroMensual, 2)));
        Assert.Equal([8m, 3m, 2m], r.Sugerencias.Select(s => s.HorasPorDia));
    }

    [Fact]
    public void AhorroCombinado_SeCalculaJunto_YNoEsLaSumaDeLosTres()
    {
        var r = Generar(Casa);

        // Juntos: 32.5 + 19.5 + 5.2 = 57.2 kWh. Los primeros 50 a $4 = $200 y 7.2 a $2 = $14.40 → $214.40.
        // Sumados sueltos darían 130 + 78 + 20.80 = $228.80: cada uno cuenta como si fuera el primero en ahorrarse.
        Assert.Equal(214.4m, Math.Round(r.AhorroCombinadoMensual!.Value, 2));
        Assert.NotEqual(Math.Round(r.Sugerencias.Sum(s => s.AhorroMensual), 2), Math.Round(r.AhorroCombinadoMensual.Value, 2));
    }

    [Fact]
    public void AparatoDeUnaHora_NoTieneSugerencia()
    {
        Assert.Empty(Generar([Aparato(1, "Plancha", 2_000m, 1m)]).Sugerencias);
    }

    [Fact]
    public void Refrigerador_QuedaFueraAunqueAhorrariaMucho()
    {
        Assert.Empty(Generar([Aparato(1, "Refrigerador", 2_000m, 24m, CategoriaAparato.Refrigeracion)]).Sugerencias);
    }

    [Fact]
    public void AhorroMenorAlMinimo_QuedaFuera()
    {
        // Foco: 1.3 kWh × $4 = $5.20 < $10.
        Assert.Empty(Generar([Aparato(1, "Foco", 100m, 4m)]).Sugerencias);
        Assert.Equal(10m, SugerenciasAhorro.AhorroMinimoMensual);
    }

    [Fact]
    public void SinConsumoMedidoOSinTarifa_NoHaySugerencias()
    {
        Assert.Empty(Generar(Casa, consumo: null).Sugerencias);
        Assert.Empty(SugerenciasAhorro.Generar(Casa, Calculadora, null, 1, 250m).Sugerencias);
    }

    [Fact]
    public void CadaAhorro_EsElMismoQueSimularUnaHoraMenos()
    {
        var simulador = new SimuladorAhorroService(null!, Calculadora, new DacService());
        foreach (var sugerencia in Generar(Casa).Sugerencias)
        {
            var aparato = Casa.Single(a => a.Id == sugerencia.AparatoId);
            var simulado = simulador.Simular(new EntradaSimulacion(
                aparato, EscenarioAhorro.MenosHoras(aparato.HorasPorDia - 1m, aparato.DiasPorSemana), Tarifa, 1, 250m, []));

            Assert.Equal(simulado.AhorroPesosMes, sugerencia.AhorroMensual);
        }
    }
}
