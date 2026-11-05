using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Recibo por monto (D9): con el total pagado y los impuestos y cargos en %, los kWh que caben en lo que cobra la
/// tarifa. Monto sin impuestos = total / (1 + p/100); kWh = el mayor entero k con Total(k) ≤ ese monto (el cargo
/// fijo va dentro de Total, así que la energía es lo que queda después del cargo fijo).
/// </summary>
public sealed class EstimadorPorMontoTests
{
    private static readonly TarifaCalculator Calculadora = new();
    private static readonly PresupuestoInversoService Inverso = new(Calculadora);

    private static EstimacionPorMonto Estimar(Tarifa tarifa, decimal total, decimal porcentaje, int meses = 1, int dias = 30)
        => EstimadorPorMonto.Estimar(Inverso, Calculadora, tarifa, meses, dias, total, porcentaje);

    [Fact]
    public void MilPesosConDieciseisPorCiento_TresTramos()
    {
        // 1,000 / 1.16 = $862.07. Total(200) = 100 + 200 = $300; quedan 562.07 / 4 = 140.5 kWh → 340 kWh.
        // Total(340) = 300 + 140 × 4 = $860 ≤ 862.07 y Total(341) = $864 ya no cabe.
        var r = Estimar(TarifasDePrueba.TresTramos(), 1_000m, 16m);

        Assert.Null(r.Error);
        Assert.Equal(340m, r.Kwh);
        Assert.Equal(860m, r.Monto);
        Assert.Equal(862.07m, Math.Round(r.MontoSinImpuestos, 2));
    }

    [Fact]
    public void SinImpuestos_TodoElTotalEsEnergia()
    {
        // p = 0: $1,000 = 300 + 175 × 4 → 375 kWh exactos.
        var r = Estimar(TarifasDePrueba.TresTramos(), 1_000m, 0m);

        Assert.Equal(375m, r.Kwh);
        Assert.Equal(1_000m, r.Monto);
    }

    [Fact]
    public void ConCargoFijo_ComercialMensual()
    {
        // $1,160 / 1.16 = $1,000; menos $60 de cargo fijo quedan $940 de energía a $2 → 470 kWh.
        var r = Estimar(TarifasDePrueba.RangoUnico(cargoFijoMensual: 60m), 1_160m, 16m);

        Assert.Equal(470m, r.Kwh);
        Assert.Equal(1_000m, r.Monto);
    }

    [Fact]
    public void Bimestral_LosTramosSeDuplican()
    {
        // Bimestre: 0–200 a $1, 200–400 a $2 y 400+ a $4. $1,000 = 200 + 400 + 100 × 4 → 500 kWh.
        Assert.Equal(500m, Estimar(TarifasDePrueba.TresTramos(), 1_000m, 0m, meses: 2, dias: 61).Kwh);
    }

    [Fact]
    public void MontoMenorAlCargoFijo_NoHayKwh()
    {
        var r = Estimar(TarifasDePrueba.RangoUnico(cargoFijoMensual: 60m), 58m, 0m);

        Assert.Equal(ErrorReciboPorMonto.MontoNoAlcanza, r.Error);
        Assert.Equal(0m, r.Kwh);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(537.5, 16)]
    [InlineData(1_234.56, 16)]
    [InlineData(99_999, 40)]
    public void IdaYVuelta_ElTotalDeLosKwhNuncaRebasaYConUnoMasSi(double total, double porcentaje)
    {
        var tarifa = TarifasDePrueba.TresTramos();
        var r = Estimar(tarifa, (decimal)total, (decimal)porcentaje, meses: 2, dias: 61);

        Assert.True(Calculadora.Calcular(r.Kwh, tarifa, 2).Total <= r.MontoSinImpuestos);
        Assert.True(Calculadora.Calcular(r.Kwh + 1m, tarifa, 2).Total > r.MontoSinImpuestos);
        Assert.Equal(Calculadora.Calcular(r.Kwh, tarifa, 2).Total, r.Monto);
    }

    [Theory]
    [InlineData(0, 16)]
    [InlineData(-1, 16)]
    [InlineData(1_000_001, 16)]
    public void TotalFueraDeRango(double total, double porcentaje)
    {
        Assert.Equal(ErrorReciboPorMonto.TotalFueraDeRango, Estimar(TarifasDePrueba.TresTramos(), (decimal)total, (decimal)porcentaje).Error);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(40.5)]
    public void CargosFueraDeRango(double porcentaje)
    {
        Assert.Equal(ErrorReciboPorMonto.CargosFueraDeRango, Estimar(TarifasDePrueba.TresTramos(), 1_000m, (decimal)porcentaje).Error);
    }

    [Fact]
    public void Limites_SonValidos()
    {
        Assert.Null(Estimar(TarifasDePrueba.TresTramos(), 1_000_000m, 40m).Error);
        Assert.Equal(16m, EstimadorPorMonto.CargosPorDefecto);
    }
}
