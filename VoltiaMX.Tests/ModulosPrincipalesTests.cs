using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9a (D1): el menú es siempre Inicio, Mis recibos (Lecturas), Aparatos y Configuración. Aquí se prueban
/// las dos pestañas de módulos y el orden de los que no tienen pestaña; Inicio y Configuración los pone el Shell.
/// </summary>
public sealed class ModulosPrincipalesTests
{
    private static MenuModulos Menu(bool avanzado, params UsoApp[] usos)
        => ModulosPrincipales.Calcular(ModulosPorUso.Calcular(usos, avanzado));

    [Theory]
    [InlineData(false, UsoApp.Casa)]
    [InlineData(false, UsoApp.Negocio)]
    [InlineData(false, UsoApp.Rentas)]
    [InlineData(false, UsoApp.Solar)]
    [InlineData(true, UsoApp.Casa)]
    [InlineData(true, UsoApp.Negocio)]
    [InlineData(true, UsoApp.Rentas)]
    [InlineData(true, UsoApp.Solar)]
    public void CualquierPerfil_LasPestanasSonMisRecibosYAparatos_EnEseOrden(bool avanzado, UsoApp uso)
    {
        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Aparatos], Menu(avanzado, uso).Principales);
    }

    [Theory]
    [InlineData(UsoApp.Casa)]
    [InlineData(UsoApp.Rentas)]
    [InlineData(UsoApp.Solar)]
    public void SinModoAvanzado_SinPestana_SoloComoSeCalculaTuRecibo(UsoApp uso)
    {
        Assert.Equal([ModuloApp.Predictor], Menu(false, uso).Secundarios);
    }

    [Fact]
    public void ModoAvanzado_Negocio_LosDemasEnOrdenFijo()
    {
        Assert.Equal(
            [ModuloApp.Predictor, ModuloApp.Metas, ModuloApp.Reportes, ModuloApp.Solar, ModuloApp.Inquilinos],
            Menu(true, UsoApp.Negocio).Secundarios);
    }

    [Fact]
    public void ModoAvanzado_Rentas_SinMetas()
    {
        Assert.Equal(
            [ModuloApp.Predictor, ModuloApp.Reportes, ModuloApp.Solar, ModuloApp.Inquilinos],
            Menu(true, UsoApp.Rentas).Secundarios);
    }

    [Fact]
    public void ModuloNoActivo_NuncaAparece()
    {
        // Lista armada a mano (sin Aparatos): no se inventa la pestaña.
        var menu = ModulosPrincipales.Calcular([ModuloApp.Lecturas, ModuloApp.Predictor]);

        Assert.Equal([ModuloApp.Lecturas], menu.Principales);
        Assert.Equal([ModuloApp.Predictor], menu.Secundarios);
    }

    [Fact]
    public void ListaViejaConPresupuesto_NoLoMuestra()
    {
        var menu = ModulosPrincipales.Calcular([ModuloApp.Lecturas, ModuloApp.Presupuesto, ModuloApp.Aparatos, ModuloApp.Predictor]);

        Assert.DoesNotContain(ModuloApp.Presupuesto, menu.Principales);
        Assert.DoesNotContain(ModuloApp.Presupuesto, menu.Secundarios);
    }

    [Fact]
    public void SinRepetidos_YNuncaMasDeDosPestanas()
    {
        var menu = Menu(true, UsoApp.Casa, UsoApp.Negocio, UsoApp.Rentas, UsoApp.Solar);

        Assert.Equal(ModulosPrincipales.MaximoPrincipales, menu.Principales.Count);
        Assert.Empty(menu.Principales.Intersect(menu.Secundarios));
        Assert.Equal(menu.Secundarios.Distinct().Count(), menu.Secundarios.Count);
    }
}
