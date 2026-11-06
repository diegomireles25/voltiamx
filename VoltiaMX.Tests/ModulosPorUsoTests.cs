using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9a (D2): la app es para hogares. Sin modo avanzado, todo perfil ve lo mismo (Mis recibos, Aparatos y
/// "Cómo se calcula tu recibo"); con modo avanzado se suman los módulos que da cada uso más Solar e Inquilinos.
/// </summary>
public class ModulosPorUsoTests
{
    private static readonly ModuloApp[] Basicos = [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos];

    [Theory]
    [InlineData(UsoApp.Casa)]
    [InlineData(UsoApp.Negocio)]
    [InlineData(UsoApp.Rentas)]
    [InlineData(UsoApp.Solar)]
    public void SinModoAvanzado_CualquierUso_SoloLosBasicos(UsoApp uso)
    {
        Assert.Equal(Basicos, ModulosPorUso.Calcular([uso], modoAvanzado: false).Order());
    }

    [Fact]
    public void SinModoAvanzado_VariosUsosOSinUsos_SoloLosBasicos()
    {
        Assert.Equal(Basicos, ModulosPorUso.Calcular([UsoApp.Negocio, UsoApp.Rentas, UsoApp.Solar], false).Order());
        Assert.Equal(Basicos, ModulosPorUso.Calcular([], false).Order());
    }

    [Fact]
    public void ModoAvanzado_Casa_AgregaMetasSolarEInquilinos()
    {
        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos, ModuloApp.Solar, ModuloApp.Inquilinos, ModuloApp.Metas],
            ModulosPorUso.Calcular([UsoApp.Casa], modoAvanzado: true).Order());
    }

    [Fact]
    public void ModoAvanzado_Negocio_AgregaMetasReportesSolarEInquilinos()
    {
        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos, ModuloApp.Solar, ModuloApp.Inquilinos, ModuloApp.Metas, ModuloApp.Reportes],
            ModulosPorUso.Calcular([UsoApp.Negocio], modoAvanzado: true).Order());
    }

    [Theory]
    [InlineData(UsoApp.Rentas)]
    [InlineData(UsoApp.Solar)]
    public void ModoAvanzado_RentasOSolar_AgregaReportesSolarEInquilinos_SinMetas(UsoApp uso)
    {
        // Como antes de la Fase 9a: Metas solo llega con Casa o Negocio.
        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos, ModuloApp.Solar, ModuloApp.Inquilinos, ModuloApp.Reportes],
            ModulosPorUso.Calcular([uso], modoAvanzado: true).Order());
    }

    [Fact]
    public void ModoAvanzado_SinUsos_SeTomaComoCasa()
    {
        Assert.Equal(ModulosPorUso.Calcular([UsoApp.Casa], true).Order(), ModulosPorUso.Calcular([], true).Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presupuesto_NuncaSeActiva(bool avanzado)
    {
        foreach (var uso in Enum.GetValues<UsoApp>())
        {
            Assert.DoesNotContain(ModuloApp.Presupuesto, ModulosPorUso.Calcular([uso], avanzado));
        }
    }
}
