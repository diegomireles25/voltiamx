using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class DacServiceTests
{
    private const decimal Limite = 250m;

    private readonly DacService _servicio = new();

    private static decimal[] Meses(decimal kwh, int cantidad = 12) => Enumerable.Repeat(kwh, cantidad).ToArray();

    [Fact]
    public void Evaluar_PromedioLejosDelLimite_RiesgoBajo()
    {
        var evaluacion = _servicio.Evaluar(Meses(150m), Limite);

        Assert.Equal(NivelRiesgoDac.Bajo, evaluacion.Nivel);
        Assert.Equal(150m, evaluacion.PromedioMensualKwh);
        Assert.Equal(100m, evaluacion.MargenKwh);
        Assert.False(evaluacion.ExcedeLimite);
    }

    [Fact]
    public void Evaluar_PromedioCercaDelLimite_RiesgoMedio()
    {
        var evaluacion = _servicio.Evaluar(Meses(210m), Limite);

        Assert.Equal(NivelRiesgoDac.Medio, evaluacion.Nivel);
        Assert.Equal(40m, evaluacion.MargenKwh);
        Assert.False(evaluacion.ExcedeLimite);
    }

    [Fact]
    public void Evaluar_PromedioArribaDelLimite_RiesgoAltoYMargenNegativo()
    {
        var evaluacion = _servicio.Evaluar(Meses(300m), Limite);

        Assert.Equal(NivelRiesgoDac.Alto, evaluacion.Nivel);
        Assert.Equal(-50m, evaluacion.MargenKwh);
        Assert.True(evaluacion.ExcedeLimite);
    }

    [Fact]
    public void Evaluar_PromedioDebajoPeroMuyCercaDelLimite_RiesgoAltoSinExceder()
    {
        var evaluacion = _servicio.Evaluar(Meses(240m), Limite);

        Assert.Equal(NivelRiesgoDac.Alto, evaluacion.Nivel);
        Assert.False(evaluacion.ExcedeLimite);
    }

    [Theory]
    [InlineData(199, NivelRiesgoDac.Bajo)]  // 79.6 % del límite
    [InlineData(200, NivelRiesgoDac.Medio)] // 80 % exacto
    [InlineData(237, NivelRiesgoDac.Medio)] // 94.8 %
    [InlineData(238, NivelRiesgoDac.Alto)]  // 95.2 %
    public void Evaluar_EnLosUmbrales_CambiaDeNivel(int promedio, NivelRiesgoDac nivelEsperado)
    {
        Assert.Equal(nivelEsperado, _servicio.Evaluar(Meses(promedio), Limite).Nivel);
    }

    [Fact]
    public void Evaluar_MasDe12Meses_UsaSoloLosUltimos12()
    {
        decimal[] historial = [.. Meses(1_000m, 6), .. Meses(100m)];

        var evaluacion = _servicio.Evaluar(historial, Limite);

        Assert.Equal(100m, evaluacion.PromedioMensualKwh);
        Assert.Equal(NivelRiesgoDac.Bajo, evaluacion.Nivel);
    }

    [Fact]
    public void Evaluar_MenosDe12Meses_PromediaLosDisponibles()
    {
        var evaluacion = _servicio.Evaluar([100m, 200m, 300m], Limite);

        Assert.Equal(200m, evaluacion.PromedioMensualKwh);
    }

    [Fact]
    public void Evaluar_UmbralesPersonalizados_LosUsaParaElNivel()
    {
        var servicio = new DacService(new UmbralesDac(Medio: 0.50m, Alto: 0.70m));

        Assert.Equal(NivelRiesgoDac.Medio, servicio.Evaluar(Meses(150m), Limite).Nivel);
    }

    [Fact]
    public void Evaluar_SinHistorial_Lanza()
    {
        Assert.Throws<ArgumentException>(() => _servicio.Evaluar([], Limite));
    }

    [Fact]
    public void Evaluar_LimiteNoPositivo_Lanza()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _servicio.Evaluar(Meses(150m), 0m));
    }
}
