using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class PresupuestoInversoServiceTests
{
    private readonly TarifaCalculator _calculadora = new();
    private readonly PresupuestoInversoService _servicio;

    public PresupuestoInversoServiceTests()
    {
        _servicio = new PresupuestoInversoService(_calculadora);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(75, 1)]     // justo el tope del básico
    [InlineData(100, 1)]    // dentro del intermedio
    [InlineData(205, 1)]    // justo el tope del intermedio
    [InlineData(206.5, 1)]  // dentro del excedente, con centavos
    [InlineData(10_000, 1)]
    [InlineData(150, 2)]    // tope del básico bimestral
    [InlineData(500, 2)]
    public void Calcular_KwhDevueltos_SonElMaximoQueCabeEnElPresupuesto(double presupuesto, int meses)
    {
        var tarifa = TarifasDePrueba.Escalonada();
        var monto = (decimal)presupuesto;

        var resultado = _servicio.Calcular(monto, tarifa, diasDelPeriodo: 30 * meses, mesesDelPeriodo: meses);

        Assert.True(_calculadora.Calcular(resultado.KwhMaximosPeriodo, tarifa, meses).Total <= monto);
        Assert.True(_calculadora.Calcular(resultado.KwhMaximosPeriodo + 1m, tarifa, meses).Total > monto);
    }

    [Fact]
    public void Calcular_PresupuestoDentroDelIntermedio_DevuelveKwhEnteros()
    {
        // 75 kWh a $1 = 75 y 12 kWh a $2 = 24: 99 de 100; con 88 kWh serían 101.
        var resultado = _servicio.Calcular(100m, TarifasDePrueba.Escalonada(), diasDelPeriodo: 30);

        Assert.Equal(87m, resultado.KwhMaximosPeriodo);
        Assert.True(resultado.CubreCargoFijo);
    }

    [Fact]
    public void Calcular_ConCargoFijo_LoDescuentaAntesDeComprarKwh()
    {
        var tarifa = TarifasDePrueba.RangoUnico(cargoFijoMensual: 50m);

        var resultado = _servicio.Calcular(251m, tarifa, diasDelPeriodo: 30);

        Assert.Equal(100m, resultado.KwhMaximosPeriodo);
        Assert.True(_calculadora.Calcular(100m, tarifa).Total <= 251m);
        Assert.True(_calculadora.Calcular(101m, tarifa).Total > 251m);
    }

    [Theory]
    [InlineData(40, 1)]  // cargo fijo de 50
    [InlineData(80, 2)]  // cargo fijo de 100 en un bimestre
    public void Calcular_PresupuestoMenorQueElCargoFijo_DevuelveCeroKwh(int presupuesto, int meses)
    {
        var resultado = _servicio.Calcular(
            presupuesto, TarifasDePrueba.RangoUnico(cargoFijoMensual: 50m), diasDelPeriodo: 30 * meses, mesesDelPeriodo: meses);

        Assert.False(resultado.CubreCargoFijo);
        Assert.Equal(0m, resultado.KwhMaximosPeriodo);
        Assert.Equal(0m, resultado.KwhMaximosPorDia);
    }

    [Fact]
    public void Calcular_KwhPorDia_SeTruncaADosDecimalesParaNoPasarse()
    {
        // 87 kWh / 31 días = 2.806...
        var resultado = _servicio.Calcular(100m, TarifasDePrueba.Escalonada(), diasDelPeriodo: 31);

        Assert.Equal(2.80m, resultado.KwhMaximosPorDia);
    }

    [Fact]
    public void Calcular_SinConsumoNiDiasTranscurridos_LosRestantesSonElPeriodoCompleto()
    {
        var resultado = _servicio.Calcular(100m, TarifasDePrueba.Escalonada(), diasDelPeriodo: 31);

        Assert.Equal(resultado.KwhMaximosPeriodo, resultado.KwhRestantes);
        Assert.Equal(resultado.KwhMaximosPorDia, resultado.KwhPorDiaRestante);
    }

    [Fact]
    public void Calcular_ConConsumoYDiasTranscurridos_RepartePresupuestoRestanteEnDiasRestantes()
    {
        // Máximo 87 kWh; ya se usaron 40 en 10 de 31 días: quedan 47 kWh para 21 días = 2.238...
        var resultado = _servicio.Calcular(
            100m, TarifasDePrueba.Escalonada(), diasDelPeriodo: 31, kwhConsumidos: 40m, diasTranscurridos: 10);

        Assert.Equal(47m, resultado.KwhRestantes);
        Assert.Equal(2.23m, resultado.KwhPorDiaRestante);
    }

    [Fact]
    public void Calcular_ConsumoArribaDelMaximo_NoQuedanKwh()
    {
        var resultado = _servicio.Calcular(
            100m, TarifasDePrueba.Escalonada(), diasDelPeriodo: 31, kwhConsumidos: 90m, diasTranscurridos: 20);

        Assert.Equal(0m, resultado.KwhRestantes);
        Assert.Equal(0m, resultado.KwhPorDiaRestante);
    }

    [Fact]
    public void Calcular_UltimoDiaDelPeriodo_NoHayDiasParaRepartir()
    {
        var resultado = _servicio.Calcular(
            100m, TarifasDePrueba.Escalonada(), diasDelPeriodo: 31, kwhConsumidos: 50m, diasTranscurridos: 31);

        Assert.Equal(37m, resultado.KwhRestantes);
        Assert.Equal(0m, resultado.KwhPorDiaRestante);
    }

    [Fact]
    public void Calcular_MontoNegativo_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _servicio.Calcular(-1m, TarifasDePrueba.Escalonada(), diasDelPeriodo: 30));
    }

    [Theory]
    [InlineData(0, 0, 0)]   // periodo sin días
    [InlineData(30, -1, 0)] // días transcurridos negativos
    [InlineData(30, 31, 0)] // más días transcurridos que los del periodo
    [InlineData(30, 10, -1)] // consumo negativo
    public void Calcular_ArgumentosDelPeriodoInvalidos_Lanza(int diasDelPeriodo, int diasTranscurridos, int kwhConsumidos)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _servicio.Calcular(
            100m, TarifasDePrueba.Escalonada(), diasDelPeriodo, kwhConsumidos: kwhConsumidos, diasTranscurridos: diasTranscurridos));
    }

    [Fact]
    public void Calcular_RangoConPrecioCero_Lanza()
    {
        var tarifa = TarifasDePrueba.Escalonada();
        tarifa.Rangos[2].PrecioPorKwh = 0m; // el presupuesto alcanzaría para kWh infinitos

        Assert.Throws<InvalidOperationException>(() => _servicio.Calcular(100m, tarifa, diasDelPeriodo: 30));
    }
}
