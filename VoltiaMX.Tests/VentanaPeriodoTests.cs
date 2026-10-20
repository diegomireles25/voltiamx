using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class VentanaPeriodoTests
{
    private static DateOnly F(int anio, int mes, int dia) => new(anio, mes, dia);

    // ---- Con un periodo cerrado: empieza al día siguiente del último corte ----

    [Fact]
    public void Calcular_Mensual_TerminaElMismoDiaDelMesSiguiente()
    {
        var ventana = VentanaPeriodo.Calcular(15, 1, F(2026, 3, 15), F(2026, 3, 20));

        Assert.Equal(new Ventana(F(2026, 3, 16), F(2026, 4, 15)), ventana);
        Assert.Equal(31, ventana.Dias);
    }

    [Fact]
    public void Calcular_Bimestral_TerminaDosMesesDespues()
    {
        var ventana = VentanaPeriodo.Calcular(15, 2, F(2026, 3, 15), F(2026, 3, 20));

        Assert.Equal(new Ventana(F(2026, 3, 16), F(2026, 5, 15)), ventana);
    }

    [Theory]
    [InlineData(2027, 28)] // año normal
    [InlineData(2028, 29)] // bisiesto
    public void Calcular_DiaCorte31EnFebrero_UsaElUltimoDiaDelMes(int anio, int ultimoDia)
    {
        var ventana = VentanaPeriodo.Calcular(31, 1, F(anio, 1, 31), F(anio, 2, 1));

        Assert.Equal(new Ventana(F(anio, 2, 1), F(anio, 2, ultimoDia)), ventana);
    }

    [Theory]
    [InlineData(29, 2027, 28)]
    [InlineData(30, 2027, 28)]
    [InlineData(29, 2028, 29)]
    [InlineData(30, 2028, 29)]
    public void Calcular_DiaCorte29y30EnFebrero_UsaElUltimoDiaDelMes(int diaCorte, int anio, int ultimoDia)
    {
        var ventana = VentanaPeriodo.Calcular(diaCorte, 1, F(anio, 1, diaCorte), F(anio, 2, 1));

        Assert.Equal(F(anio, 2, ultimoDia), ventana.Fin);
    }

    [Fact]
    public void Calcular_DespuesDeFebrero_VuelveAlDiaDeCorte()
    {
        // El corte de febrero cayó el 28 por ser mes corto; marzo vuelve al 31.
        var ventana = VentanaPeriodo.Calcular(31, 1, F(2027, 2, 28), F(2027, 3, 5));

        Assert.Equal(new Ventana(F(2027, 3, 1), F(2027, 3, 31)), ventana);
    }

    [Fact]
    public void Calcular_BimestralQueCruzaElAnioYFebrero()
    {
        var ventana = VentanaPeriodo.Calcular(31, 2, F(2026, 12, 31), F(2027, 1, 10));

        Assert.Equal(new Ventana(F(2027, 1, 1), F(2027, 2, 28)), ventana);
    }

    [Fact]
    public void Calcular_CruceDeAnio()
    {
        var ventana = VentanaPeriodo.Calcular(10, 2, F(2026, 11, 10), F(2026, 12, 1));

        Assert.Equal(new Ventana(F(2026, 11, 11), F(2027, 1, 10)), ventana);
    }

    // ---- Sin periodos cerrados: ancla en el corte más reciente en o antes de la referencia ----

    [Fact]
    public void Calcular_SinCerrados_ReferenciaDespuesDelCorteDelMes()
    {
        var ventana = VentanaPeriodo.Calcular(15, 2, null, F(2026, 10, 20));

        Assert.Equal(new Ventana(F(2026, 10, 16), F(2026, 12, 15)), ventana);
    }

    [Fact]
    public void Calcular_SinCerrados_ReferenciaEnElDiaDeCorte_EseDiaEsElAncla()
    {
        var ventana = VentanaPeriodo.Calcular(15, 1, null, F(2026, 10, 15));

        Assert.Equal(new Ventana(F(2026, 10, 16), F(2026, 11, 15)), ventana);
    }

    [Fact]
    public void Calcular_SinCerrados_ReferenciaAntesDelCorteDelMes_UsaElCorteDelMesAnterior()
    {
        var ventana = VentanaPeriodo.Calcular(15, 2, null, F(2026, 10, 5));

        Assert.Equal(new Ventana(F(2026, 9, 16), F(2026, 11, 15)), ventana);
    }

    [Fact]
    public void Calcular_SinCerrados_AnclaEnFebreroConDiaCorte31()
    {
        // Corte más reciente en o antes del 10 de marzo de 2027: 28 de febrero.
        var ventana = VentanaPeriodo.Calcular(31, 2, null, F(2027, 3, 10));

        Assert.Equal(new Ventana(F(2027, 3, 1), F(2027, 4, 30)), ventana);
    }

    [Fact]
    public void Calcular_SinCerrados_CruceDeAnio()
    {
        var ventana = VentanaPeriodo.Calcular(20, 2, null, F(2027, 1, 5));

        Assert.Equal(new Ventana(F(2026, 12, 21), F(2027, 2, 20)), ventana);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(32, 1)]
    [InlineData(10, 0)]
    [InlineData(10, 3)]
    public void Calcular_ParametrosFueraDeRango_Lanza(int diaCorte, int meses)
        => Assert.Throws<ArgumentOutOfRangeException>(() => VentanaPeriodo.Calcular(diaCorte, meses, null, F(2026, 10, 1)));
}
