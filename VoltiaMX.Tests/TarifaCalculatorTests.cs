using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class TarifaCalculatorTests
{
    private readonly TarifaCalculator _calculadora = new();

    [Fact]
    public void Calcular_UnSoloRango_CobraTodoAlPrecioDelRango()
    {
        var resultado = _calculadora.Calcular(100m, TarifasDePrueba.RangoUnico());

        var rango = Assert.Single(resultado.Desglose);
        Assert.Equal("Único", rango.Nombre);
        Assert.Equal(100m, rango.Kwh);
        Assert.Equal(2m, rango.PrecioPorKwh);
        Assert.Equal(200m, rango.Subtotal);
        Assert.Equal(200m, resultado.Total);
    }

    [Fact]
    public void Calcular_VariosRangos_LlenaCadaRangoEnOrden()
    {
        var resultado = _calculadora.Calcular(100m, TarifasDePrueba.Escalonada());

        Assert.Collection(resultado.Desglose,
            r => { Assert.Equal("Básico", r.Nombre); Assert.Equal(75m, r.Kwh); Assert.Equal(75m, r.Subtotal); },
            r => { Assert.Equal("Intermedio", r.Nombre); Assert.Equal(25m, r.Kwh); Assert.Equal(50m, r.Subtotal); });
        Assert.Equal(125m, resultado.SubtotalEnergia);
        Assert.Equal(125m, resultado.Total);
    }

    [Fact]
    public void Calcular_RangosDesordenados_RespetaElCampoOrden()
    {
        var tarifa = TarifasDePrueba.Escalonada();
        tarifa.Rangos.Reverse();

        var resultado = _calculadora.Calcular(100m, tarifa);

        Assert.Equal(["Básico", "Intermedio"], resultado.Desglose.Select(r => r.Nombre));
        Assert.Equal(125m, resultado.Total);
    }

    [Theory]
    [InlineData(75, 1, 75)]   // justo en el tope del básico: nada pasa al intermedio
    [InlineData(76, 2, 77)]   // 1 kWh después del tope del básico
    [InlineData(140, 2, 205)] // justo en el tope del intermedio: nada pasa al excedente
    public void Calcular_ExactamenteEnElLimiteDeUnRango_NoDesbordaAlSiguiente(
        int kwh, int rangosConConsumo, int totalEsperado)
    {
        var resultado = _calculadora.Calcular(kwh, TarifasDePrueba.Escalonada());

        Assert.Equal(rangosConConsumo, resultado.Desglose.Count);
        Assert.Equal(totalEsperado, resultado.Total);
    }

    [Fact]
    public void Calcular_CeroKwh_TotalCeroYSinDesglose()
    {
        var resultado = _calculadora.Calcular(0m, TarifasDePrueba.Escalonada());

        Assert.Empty(resultado.Desglose);
        Assert.Equal(0m, resultado.Total);
    }

    [Fact]
    public void Calcular_ConsumoMuyAlto_CobraTodoElExcedenteSinLimite()
    {
        var resultado = _calculadora.Calcular(100_000m, TarifasDePrueba.Escalonada());

        var excedente = resultado.Desglose[^1];
        Assert.Equal("Excedente", excedente.Nombre);
        Assert.Equal(99_860m, excedente.Kwh);
        Assert.Equal(75m + 130m + 299_580m, resultado.Total);
    }

    [Fact]
    public void Calcular_PeriodoBimestral_DuplicaLosLimitesDeLosRangos()
    {
        var resultado = _calculadora.Calcular(200m, TarifasDePrueba.Escalonada(), mesesDelPeriodo: 2);

        Assert.Collection(resultado.Desglose,
            r => Assert.Equal(150m, r.Kwh),
            r => Assert.Equal(50m, r.Kwh));
        Assert.Equal(250m, resultado.Total);
    }

    [Theory]
    [InlineData(1, 50, 250)]
    [InlineData(2, 100, 300)]
    public void Calcular_ConCargoFijo_LoSumaUnaVezPorMesDelPeriodo(
        int meses, int cargoFijoEsperado, int totalEsperado)
    {
        var resultado = _calculadora.Calcular(100m, TarifasDePrueba.RangoUnico(cargoFijoMensual: 50m), meses);

        Assert.Equal(200m, resultado.SubtotalEnergia);
        Assert.Equal(cargoFijoEsperado, resultado.CargoFijo);
        Assert.Equal(totalEsperado, resultado.Total);
    }

    [Fact]
    public void Calcular_KwhNegativo_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculadora.Calcular(-1m, TarifasDePrueba.Escalonada()));
    }

    [Fact]
    public void Calcular_MesesMenorQueUno_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _calculadora.Calcular(10m, TarifasDePrueba.Escalonada(), mesesDelPeriodo: 0));
    }

    [Fact]
    public void Calcular_TarifaSinRangos_Lanza()
    {
        var tarifa = new Tarifa { Nombre = "Vacía" };

        Assert.Throws<InvalidOperationException>(() => _calculadora.Calcular(10m, tarifa));
    }

    [Fact]
    public void Calcular_RangosConHueco_Lanza()
    {
        var tarifa = TarifasDePrueba.Escalonada();
        tarifa.Rangos[1].KwhDesde = 80m; // el básico termina en 75

        Assert.Throws<InvalidOperationException>(() => _calculadora.Calcular(100m, tarifa));
    }
}
