using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class ComparacionReciboTests
{
    [Fact]
    public void SeisDias_AunEsPronto()
    {
        // 6 < DiasMinimos (7): aunque 1,049 contra 718 sea +46 %, todavía no se compara.
        var r = ComparacionRecibo.Evaluar(1_049m, 718m, 6);

        Assert.Equal(EstadoComparacion.AunEsPronto, r.Estado);
        Assert.Equal(6, r.Dias);
    }

    [Fact]
    public void SieteDias_YaCompara()
    {
        var r = ComparacionRecibo.Evaluar(1_049m, 718m, 7);
        Assert.Equal(EstadoComparacion.MasAlto, r.Estado);
    }

    [Fact]
    public void CuarentaYSeisPorCientoArriba()
    {
        // 1,049 / 718 = 1.4610 → +46.10 % → 46.
        var r = ComparacionRecibo.Evaluar(1_049m, 718m, 24);

        Assert.Equal(EstadoComparacion.MasAlto, r.Estado);
        Assert.Equal(46m, r.Porcentaje);
    }

    [Fact]
    public void OchoPorCientoAbajo()
    {
        // 920 / 1,000 = 0.92 → −8 %.
        var r = ComparacionRecibo.Evaluar(920m, 1_000m, 30);

        Assert.Equal(EstadoComparacion.MasBajo, r.Estado);
        Assert.Equal(8m, r.Porcentaje);
    }

    [Theory]
    [InlineData(1_010)] // +1 %: dentro del margen.
    [InlineData(1_020)] // +2 %: justo en el borde, cuenta como parecido.
    [InlineData(980)] // −2 %: el borde por abajo.
    [InlineData(1_000)] // Igual exacto.
    public void DentroDelMargen_Igual(int proyectado)
    {
        var r = ComparacionRecibo.Evaluar(proyectado, 1_000m, 30);
        Assert.Equal(EstadoComparacion.Igual, r.Estado);
    }

    [Fact]
    public void JustoFueraDelMargen_Compara()
    {
        // 1,021 / 1,000 = +2.1 % > 2 % → más alto, redondeado a 2.
        var r = ComparacionRecibo.Evaluar(1_021m, 1_000m, 30);

        Assert.Equal(EstadoComparacion.MasAlto, r.Estado);
        Assert.Equal(2m, r.Porcentaje);
    }

    [Fact]
    public void SinReciboAnterior()
    {
        var r = ComparacionRecibo.Evaluar(500m, null, 30);
        Assert.Equal(EstadoComparacion.SinReciboAnterior, r.Estado);
    }

    [Fact]
    public void ReciboAnteriorConConsumoCero_NoDividePorCero()
    {
        var r = ComparacionRecibo.Evaluar(500m, 0m, 30);
        Assert.Equal(EstadoComparacion.SinReciboAnterior, r.Estado);
    }

    [Fact]
    public void SinProyeccion_AunEsPronto()
    {
        var r = ComparacionRecibo.Evaluar(null, 718m, 20);
        Assert.Equal(EstadoComparacion.AunEsPronto, r.Estado);
    }

    [Fact]
    public void Constantes()
    {
        Assert.Equal(7, ComparacionRecibo.DiasMinimos);
        Assert.Equal(2m, ComparacionRecibo.MargenIgual);
    }
}
