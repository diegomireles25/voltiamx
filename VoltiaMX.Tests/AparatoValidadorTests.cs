using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class AparatoValidadorTests
{
    private static AparatoEditable Valido() => new(
        Id: null,
        LugarId: 1,
        Nombre: "Refrigerador",
        Categoria: CategoriaAparato.Refrigeracion,
        Cantidad: 1,
        Watts: 250m,
        HorasPorDia: 24m,
        DiasPorSemana: 7,
        CicloDeTrabajo: 0.5m);

    [Fact]
    public void Validar_AparatoValido_SinErrores()
    {
        Assert.Empty(AparatoValidador.Validar(Valido()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_NombreVacio_NombreVacio(string nombre)
    {
        Assert.Equal([CodigoErrorAparato.NombreVacio], AparatoValidador.Validar(Valido() with { Nombre = nombre }));
    }

    [Fact]
    public void Validar_NombreDe41_NombreMuyLargo()
    {
        Assert.Empty(AparatoValidador.Validar(Valido() with { Nombre = new string('a', 40) }));
        Assert.Equal(
            [CodigoErrorAparato.NombreMuyLargo],
            AparatoValidador.Validar(Valido() with { Nombre = new string('a', 41) }));
    }

    [Fact]
    public void Validar_CategoriaFueraDelEnum_CategoriaInvalida()
    {
        Assert.Equal(
            [CodigoErrorAparato.CategoriaInvalida],
            AparatoValidador.Validar(Valido() with { Categoria = (CategoriaAparato)99 }));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(99, true)]
    [InlineData(100, false)]
    public void Validar_Cantidad_De1A99(int cantidad, bool valida)
    {
        var errores = AparatoValidador.Validar(Valido() with { Cantidad = cantidad });

        Assert.Equal(valida ? [] : [CodigoErrorAparato.CantidadFueraDeRango], errores);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("-5", false)]
    [InlineData("0.5", true)]
    [InlineData("20000", true)]
    [InlineData("20000.01", false)]
    public void Validar_Watts_MayorQueCeroHasta20000(string watts, bool valida)
    {
        var errores = AparatoValidador.Validar(Valido() with { Watts = decimal.Parse(watts, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Equal(valida ? [] : [CodigoErrorAparato.WattsFueraDeRango], errores);
    }

    [Theory]
    [InlineData("0", false)]
    [InlineData("0.25", true)]
    [InlineData("24", true)]
    [InlineData("24.01", false)]
    public void Validar_Horas_MayorQueCeroHasta24(string horas, bool valida)
    {
        var errores = AparatoValidador.Validar(Valido() with { HorasPorDia = decimal.Parse(horas, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Equal(valida ? [] : [CodigoErrorAparato.HorasFueraDeRango], errores);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    [InlineData(8, false)]
    public void Validar_Dias_De1A7(int dias, bool valida)
    {
        var errores = AparatoValidador.Validar(Valido() with { DiasPorSemana = dias });

        Assert.Equal(valida ? [] : [CodigoErrorAparato.DiasFueraDeRango], errores);
    }

    [Theory]
    [InlineData("0.04", false)]
    [InlineData("0.05", true)]
    [InlineData("1", true)]
    [InlineData("1.01", false)]
    public void Validar_Ciclo_De005A1(string ciclo, bool valido)
    {
        var errores = AparatoValidador.Validar(Valido() with { CicloDeTrabajo = decimal.Parse(ciclo, System.Globalization.CultureInfo.InvariantCulture) });

        Assert.Equal(valido ? [] : [CodigoErrorAparato.CicloFueraDeRango], errores);
    }

    [Fact]
    public void Validar_VariosErrores_LosDevuelveTodosEnOrden()
    {
        var errores = AparatoValidador.Validar(Valido() with { Nombre = "", Cantidad = 0, DiasPorSemana = 9 });

        Assert.Equal(
            [CodigoErrorAparato.NombreVacio, CodigoErrorAparato.CantidadFueraDeRango, CodigoErrorAparato.DiasFueraDeRango],
            errores);
    }
}
