using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class SimuladorValidadorTests
{
    private static readonly Aparato Refri = new()
    {
        Nombre = "Refrigerador",
        Categoria = CategoriaAparato.Refrigeracion,
        Cantidad = 1,
        Watts = 250m,
        HorasPorDia = 24m,
        DiasPorSemana = 7,
        CicloDeTrabajo = 0.5m,
    };

    [Fact]
    public void Validar_EscenariosValidos_SinErrores()
    {
        Assert.Empty(SimuladorValidador.Validar(Refri, EscenarioAhorro.MenosHoras(12m, 5)));
        Assert.Empty(SimuladorValidador.Validar(Refri, EscenarioAhorro.EquipoEficiente(150m, 0.4m, 8_000m)));
        Assert.Empty(SimuladorValidador.Validar(Refri, EscenarioAhorro.EquipoEficiente(150m, null, 0m)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("24.5")]
    public void Validar_HorasFueraDeRango(string horas)
    {
        var escenario = EscenarioAhorro.MenosHoras(decimal.Parse(horas, System.Globalization.CultureInfo.InvariantCulture), 7);

        Assert.Equal([CodigoErrorSimulacion.HorasFueraDeRango], SimuladorValidador.Validar(Refri, escenario));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    public void Validar_DiasFueraDeRango(int dias)
    {
        Assert.Equal(
            [CodigoErrorSimulacion.DiasFueraDeRango],
            SimuladorValidador.Validar(Refri, EscenarioAhorro.MenosHoras(5m, dias)));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("20001")]
    public void Validar_WattsFueraDeRango(string watts)
    {
        var escenario = EscenarioAhorro.EquipoEficiente(decimal.Parse(watts, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal([CodigoErrorSimulacion.WattsFueraDeRango], SimuladorValidador.Validar(Refri, escenario));
    }

    [Theory]
    [InlineData("0.04")]
    [InlineData("1.2")]
    public void Validar_CicloFueraDeRango(string ciclo)
    {
        var escenario = EscenarioAhorro.EquipoEficiente(150m, decimal.Parse(ciclo, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal([CodigoErrorSimulacion.CicloFueraDeRango], SimuladorValidador.Validar(Refri, escenario));
    }

    [Fact]
    public void Validar_PrecioNegativo()
    {
        Assert.Equal(
            [CodigoErrorSimulacion.PrecioNegativo],
            SimuladorValidador.Validar(Refri, EscenarioAhorro.EquipoEficiente(150m, null, -1m)));
    }

    [Fact]
    public void Aplicar_LaCantidadNoCambia()
    {
        var doce = new Aparato
        {
            Nombre = "Focos",
            Categoria = CategoriaAparato.Iluminacion,
            Cantidad = 12,
            Watts = 60m,
            HorasPorDia = 5m,
            DiasPorSemana = 7,
            CicloDeTrabajo = 1m,
        };

        var cambiado = EscenarioAhorro.EquipoEficiente(9m).Aplicar(doce);

        Assert.Equal(12, cambiado.Cantidad);
        Assert.Equal(9m, cambiado.Watts);
        Assert.Equal(5m, cambiado.HorasPorDia);
        Assert.Equal(1m, cambiado.CicloDeTrabajo);
    }
}
