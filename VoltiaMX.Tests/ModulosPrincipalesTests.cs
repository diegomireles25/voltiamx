using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class ModulosPrincipalesTests
{
    private static MenuModulos Menu(bool avanzado, params UsoApp[] usos)
        => ModulosPrincipales.Calcular(ModulosPorUso.Calcular(usos, avanzado), usos);

    [Fact]
    public void Casa_LecturasYAparatos_ElRestoEnMas()
    {
        // Casa: Lecturas, Predictor, Aparatos y Metas.
        var menu = Menu(false, UsoApp.Casa);

        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Aparatos], menu.Principales);
        Assert.Equal([ModuloApp.Predictor, ModuloApp.Metas], menu.Secundarios);
    }

    [Fact]
    public void Negocio_LecturasYAparatos()
    {
        var menu = Menu(false, UsoApp.Negocio);

        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Aparatos], menu.Principales);
        Assert.Equal([ModuloApp.Predictor, ModuloApp.Metas, ModuloApp.Reportes], menu.Secundarios);
    }

    [Fact]
    public void Rentas_LecturasEInquilinos()
    {
        var menu = Menu(false, UsoApp.Rentas);

        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Inquilinos], menu.Principales);
        Assert.Equal([ModuloApp.Reportes], menu.Secundarios);
    }

    [Fact]
    public void Solar_SoloSolar()
    {
        var menu = Menu(false, UsoApp.Solar);

        Assert.Equal([ModuloApp.Solar], menu.Principales);
        Assert.Equal([ModuloApp.Reportes], menu.Secundarios);
    }

    [Fact]
    public void CasaYRentas_PorPrioridadLecturasYAparatos_InquilinosEnMas()
    {
        var menu = Menu(false, UsoApp.Casa, UsoApp.Rentas);

        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Aparatos], menu.Principales);
        Assert.Contains(ModuloApp.Inquilinos, menu.Secundarios);
    }

    [Fact]
    public void ModoAvanzado_LosQueAgregaVanAMas()
    {
        // Casa avanzado: además Solar e Inquilinos, que no son principales de Casa.
        var menu = Menu(true, UsoApp.Casa);

        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Aparatos], menu.Principales);
        Assert.Equal([ModuloApp.Predictor, ModuloApp.Metas, ModuloApp.Solar, ModuloApp.Inquilinos], menu.Secundarios);
    }

    [Fact]
    public void SolarConModoAvanzado_SolarPrincipal_AparatosEInquilinosEnMas()
    {
        var menu = Menu(true, UsoApp.Solar);

        Assert.Equal([ModuloApp.Solar], menu.Principales);
        Assert.Equal([ModuloApp.Reportes, ModuloApp.Inquilinos, ModuloApp.Aparatos], menu.Secundarios);
    }

    [Fact]
    public void SinUsos_SeTomaComoCasa()
    {
        var menu = ModulosPrincipales.Calcular(ModulosPorUso.Calcular([], false), []);

        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Aparatos], menu.Principales);
    }

    [Fact]
    public void UnModuloNoActivo_NuncaAparece()
    {
        // Casa con solo Lecturas activa: Aparatos no está activo y no aparece en ningún lado.
        var menu = ModulosPrincipales.Calcular([ModuloApp.Lecturas], [UsoApp.Casa]);

        Assert.Equal([ModuloApp.Lecturas], menu.Principales);
        Assert.Empty(menu.Secundarios);
    }

    [Fact]
    public void Presupuesto_AunqueVengaActivo_NoAparece()
    {
        // Una lista vieja que todavía trajera Presupuesto.
        var menu = ModulosPrincipales.Calcular([ModuloApp.Lecturas, ModuloApp.Presupuesto, ModuloApp.Aparatos], [UsoApp.Casa]);

        Assert.DoesNotContain(ModuloApp.Presupuesto, menu.Principales.Concat(menu.Secundarios));
    }

    [Fact]
    public void NuncaMasDeDosPrincipales_YSinRepetidos()
    {
        foreach (var avanzado in new[] { false, true })
        {
            foreach (var usos in new[] { new[] { UsoApp.Casa, UsoApp.Negocio, UsoApp.Rentas, UsoApp.Solar }, [UsoApp.Rentas, UsoApp.Solar] })
            {
                var activos = ModulosPorUso.Calcular(usos, avanzado);
                var menu = ModulosPrincipales.Calcular(activos, usos);

                Assert.True(menu.Principales.Count <= ModulosPrincipales.MaximoPrincipales);
                Assert.Empty(menu.Principales.Intersect(menu.Secundarios));
                Assert.Equal(activos.Where(m => m != ModuloApp.Presupuesto).Order(), menu.Principales.Concat(menu.Secundarios).Order());
            }
        }
    }
}
