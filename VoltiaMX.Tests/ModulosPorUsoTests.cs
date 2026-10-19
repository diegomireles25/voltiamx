using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class ModulosPorUsoTests
{
    [Fact]
    public void Calcular_Casa_LecturasPredictorPresupuestoYAparatos()
    {
        var modulos = ModulosPorUso.Calcular([UsoApp.Casa], modoAvanzado: false);

        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Presupuesto, ModuloApp.Aparatos],
            modulos.Order());
    }

    [Fact]
    public void Calcular_Negocio_LecturasPredictorAparatosYReportes()
    {
        var modulos = ModulosPorUso.Calcular([UsoApp.Negocio], modoAvanzado: false);

        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos, ModuloApp.Reportes],
            modulos.Order());
    }

    [Fact]
    public void Calcular_Rentas_LecturasInquilinosYReportes()
    {
        var modulos = ModulosPorUso.Calcular([UsoApp.Rentas], modoAvanzado: false);

        Assert.Equal([ModuloApp.Lecturas, ModuloApp.Inquilinos, ModuloApp.Reportes], modulos.Order());
    }

    [Fact]
    public void Calcular_Solar_SolarYReportes()
    {
        var modulos = ModulosPorUso.Calcular([UsoApp.Solar], modoAvanzado: false);

        Assert.Equal([ModuloApp.Solar, ModuloApp.Reportes], modulos.Order());
    }

    [Fact]
    public void Calcular_VariosUsos_UneLosModulosSinRepetir()
    {
        var modulos = ModulosPorUso.Calcular([UsoApp.Rentas, UsoApp.Solar], modoAvanzado: false);

        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Solar, ModuloApp.Inquilinos, ModuloApp.Reportes],
            modulos.Order());
    }

    [Fact]
    public void Calcular_ModoAvanzado_AgregaAparatosSolarEInquilinos()
    {
        var modulos = ModulosPorUso.Calcular([UsoApp.Casa], modoAvanzado: true);

        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Presupuesto, ModuloApp.Aparatos,
             ModuloApp.Solar, ModuloApp.Inquilinos],
            modulos.Order());
    }

    [Fact]
    public void Calcular_SinUsos_SeTomaComoCasa()
    {
        var modulos = ModulosPorUso.Calcular([], modoAvanzado: false);

        Assert.Equal(ModulosPorUso.Calcular([UsoApp.Casa], modoAvanzado: false).Order(), modulos.Order());
    }

    [Fact]
    public void Calcular_TodosLosUsosEnModoAvanzado_MetasNoSeActiva()
    {
        var modulos = ModulosPorUso.Calcular(Enum.GetValues<UsoApp>(), modoAvanzado: true);

        Assert.DoesNotContain(ModuloApp.Metas, modulos);
    }
}
