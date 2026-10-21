using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class TarifaValidadorTests
{
    private static readonly DateOnly Vigencia = new(2026, 1, 1);

    private static TarifaEditable Valida(params RangoEditable[] rangos) => new(
        null, "Mi tarifa", TipoTarifa.Domestica, 0m, 850m, Vigencia,
        rangos.Length > 0
            ? rangos
            : [new("Básico", 150m, 1.0m), new("Intermedio", 450m, 1.25m), new("Excedente", null, 3.5m)]);

    private static IReadOnlyList<ErrorTarifa> Validar(TarifaEditable tarifa, params string[] otros)
        => TarifaValidador.Validar(tarifa, otros);

    [Fact]
    public void Validar_TarifaCorrecta_SinErrores()
        => Assert.Empty(Validar(Valida(), "1", "1C"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validar_NombreVacio(string nombre)
        => Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.NombreObligatorio), Validar(Valida() with { Nombre = nombre }));

    [Fact]
    public void Validar_NombreDeMasDe40Caracteres()
    {
        Assert.Empty(Validar(Valida() with { Nombre = new string('A', 40) }));
        Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.NombreMuyLargo), Validar(Valida() with { Nombre = new string('A', 41) }));
    }

    [Fact]
    public void Validar_NombreDuplicadoSinDistinguirMayusculas()
        => Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.NombreDuplicado), Validar(Valida() with { Nombre = " mi TARIFA " }, "Mi tarifa"));

    [Fact]
    public void Validar_AlEditar_SuPropioNombreNoCuentaComoDuplicado()
    {
        // El servicio pasa los nombres de las DEMÁS tarifas; la que se edita no está en la lista.
        Assert.Empty(Validar(Valida() with { Id = 7 }, "1", "1C"));
    }

    [Fact]
    public void Validar_SinRangos()
        => Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.SinRangos), Validar(Valida() with { Rangos = [] }));

    [Fact]
    public void Validar_NombreDeRangoVacio()
        => Assert.Contains(
            new ErrorTarifa(CodigoErrorTarifa.NombreDeRangoObligatorio, 2),
            Validar(Valida(new("Básico", 150m, 1m), new(" ", null, 2m))));

    [Fact]
    public void Validar_DosRangosSinLimite()
        => Assert.Contains(
            new ErrorTarifa(CodigoErrorTarifa.RangoDespuesDeUnoSinLimite, 2),
            Validar(Valida(new("Básico", null, 1m), new("Excedente", null, 3m))));

    [Fact]
    public void Validar_UltimoRangoConLimite()
        => Assert.Contains(
            new ErrorTarifa(CodigoErrorTarifa.UltimoRangoConLimite, 2),
            Validar(Valida(new("Básico", 150m, 1m), new("Excedente", 300m, 3m))));

    [Theory]
    [InlineData(150)]
    [InlineData(100)]
    public void Validar_HastaNoMayorQueDesde(int hasta)
        => Assert.Contains(
            new ErrorTarifa(CodigoErrorTarifa.HastaNoMayorQueDesde, 2),
            Validar(Valida(new("Básico", 150m, 1m), new("Intermedio", hasta, 1.25m), new("Excedente", null, 3m))));

    [Fact]
    public void Validar_PrecioCeroONegativo()
    {
        var errores = Validar(Valida(new("Básico", 150m, 0m), new("Excedente", null, -1m)));

        Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.PrecioNoPositivo, 1), errores);
        Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.PrecioNoPositivo, 2), errores);
    }

    [Fact]
    public void Validar_PrecioConMasDeCuatroDecimales()
    {
        Assert.Empty(Validar(Valida(new("Básico", 150m, 1.2345m), new("Excedente", null, 3m))));
        Assert.Contains(
            new ErrorTarifa(CodigoErrorTarifa.PrecioConMasDeCuatroDecimales, 1),
            Validar(Valida(new("Básico", 150m, 1.23456m), new("Excedente", null, 3m))));
    }

    [Fact]
    public void Validar_CargoFijoNegativo()
    {
        Assert.Empty(Validar(Valida() with { CargoFijoMensual = 0m }));
        Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.CargoFijoNegativo), Validar(Valida() with { CargoFijoMensual = -0.01m }));
    }

    [Fact]
    public void Validar_LimiteDacSoloEnDomestica()
    {
        Assert.Contains(
            new ErrorTarifa(CodigoErrorTarifa.LimiteDacSoloDomestica),
            Validar(Valida() with { Tipo = TipoTarifa.Comercial, LimiteDacKwhMensual = 500m }));
        Assert.Empty(Validar(Valida() with { Tipo = TipoTarifa.Comercial, LimiteDacKwhMensual = null }));
        Assert.Empty(Validar(Valida() with { LimiteDacKwhMensual = null }));
    }

    [Fact]
    public void Validar_LimiteDacCeroONegativo()
        => Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.LimiteDacNoPositivo), Validar(Valida() with { LimiteDacKwhMensual = 0m }));

    [Fact]
    public void Validar_SinVigencia()
        => Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.VigenciaObligatoria), Validar(Valida() with { Vigencia = null }));

    // ---- Continuidad de rangos ya armados (la misma regla de RangosDeTarifa) ----

    private static TarifaRango R(decimal desde, decimal? hasta, int orden) => new() { Nombre = "R", KwhDesde = desde, KwhHasta = hasta, PrecioPorKwh = 1m, Orden = orden };

    [Fact]
    public void RevisarRangos_HuecoEntreRangos()
        => Assert.Equal(
            [new ErrorTarifa(CodigoErrorTarifa.HuecoEntreRangos, 2)],
            TarifaValidador.RevisarRangos([R(0m, 100m, 1), R(120m, null, 2)]));

    [Fact]
    public void RevisarRangos_PrimeroNoEmpiezaEnCero()
        => Assert.Equal(
            [new ErrorTarifa(CodigoErrorTarifa.PrimerRangoNoEmpiezaEnCero, 1)],
            TarifaValidador.RevisarRangos([R(10m, 100m, 1), R(100m, null, 2)]));

    [Fact]
    public void RevisarRangos_Continuos_SinErrores()
        => Assert.Empty(TarifaValidador.RevisarRangos([R(0m, 100m, 1), R(100m, 200m, 2), R(200m, null, 3)]));
}
