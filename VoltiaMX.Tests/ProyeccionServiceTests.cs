using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class ProyeccionServiceTests
{
    private readonly ProyeccionService _servicio = new(new TarifaCalculator());

    [Fact]
    public void Proyectar_MitadDelPeriodo_ExtrapolaLinealmente()
    {
        var resultado = _servicio.Proyectar(150m, diasTranscurridos: 30, diasDelPeriodo: 60, TarifasDePrueba.RangoUnico());

        Assert.Equal(5m, resultado.PromedioDiarioKwh);
        Assert.Equal(300m, resultado.KwhProyectados);
        Assert.Equal(600m, resultado.MontoProyectado);
    }

    [Fact]
    public void Proyectar_PrimerDiaDelPeriodo_ExtrapolaElConsumoDeUnDia()
    {
        var resultado = _servicio.Proyectar(5m, diasTranscurridos: 1, diasDelPeriodo: 60, TarifasDePrueba.RangoUnico());

        Assert.Equal(5m, resultado.PromedioDiarioKwh);
        Assert.Equal(300m, resultado.KwhProyectados);
    }

    [Fact]
    public void Proyectar_CeroDiasTranscurridos_DevuelveElAcumuladoSinExtrapolar()
    {
        var resultado = _servicio.Proyectar(12m, diasTranscurridos: 0, diasDelPeriodo: 60, TarifasDePrueba.RangoUnico());

        Assert.Equal(0m, resultado.PromedioDiarioKwh);
        Assert.Equal(12m, resultado.KwhProyectados);
        Assert.Equal(24m, resultado.MontoProyectado);
    }

    [Fact]
    public void Proyectar_UltimoDiaDelPeriodo_DevuelveElAcumulado()
    {
        var resultado = _servicio.Proyectar(290m, diasTranscurridos: 60, diasDelPeriodo: 60, TarifasDePrueba.RangoUnico());

        Assert.Equal(290m, resultado.KwhProyectados);
    }

    [Fact]
    public void Proyectar_PeriodoBimestral_CalculaElMontoConLosRangosDuplicados()
    {
        var resultado = _servicio.Proyectar(
            200m, diasTranscurridos: 30, diasDelPeriodo: 60, TarifasDePrueba.Escalonada(), mesesDelPeriodo: 2);

        // 400 kWh bimestrales: 150 a $1 + 130 a $2 + 120 a $3
        Assert.Equal(400m, resultado.KwhProyectados);
        Assert.Equal(150m + 260m + 360m, resultado.MontoProyectado);
        Assert.Equal(3, resultado.Calculo.Desglose.Count);
    }

    [Theory]
    [InlineData(61, 60)] // más días transcurridos que los del periodo
    [InlineData(-1, 60)]
    [InlineData(0, 0)]   // periodo sin días
    public void Proyectar_DiasInvalidos_Lanza(int diasTranscurridos, int diasDelPeriodo)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _servicio.Proyectar(10m, diasTranscurridos, diasDelPeriodo, TarifasDePrueba.RangoUnico()));
    }

    [Fact]
    public void Proyectar_KwhNegativos_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _servicio.Proyectar(-1m, diasTranscurridos: 10, diasDelPeriodo: 60, TarifasDePrueba.RangoUnico()));
    }
}
