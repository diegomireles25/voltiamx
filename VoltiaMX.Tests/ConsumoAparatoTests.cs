using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class ConsumoAparatoTests
{
    [Fact]
    public void KwhPorMes_DosFocosDe100W_CincoHorasDiarias()
    {
        // 2 × 100 W = 0.2 kW; × 5 h × 7 días = 7 kWh por semana; × 52/12 semanas = 30.3333 kWh al mes.
        var kwh = ConsumoAparato.KwhPorMes(cantidad: 2, watts: 100m, horasPorDia: 5m, diasPorSemana: 7, cicloDeTrabajo: 1m);

        Assert.Equal(30.3333m, Math.Round(kwh, 4));
    }

    [Fact]
    public void KwhPorMes_CicloMinimo_24HorasYSieteDias()
    {
        // 1 kW × 24 h × 7 días = 168 kWh por semana; × 52/12 = 728; × ciclo 0.05 = 36.4 kWh al mes.
        var kwh = ConsumoAparato.KwhPorMes(cantidad: 1, watts: 1000m, horasPorDia: 24m, diasPorSemana: 7, cicloDeTrabajo: 0.05m);

        Assert.Equal(36.4m, Math.Round(kwh, 4));
    }

    [Fact]
    public void KwhPorMes_UnDiaALaSemana_UnaHora()
    {
        // 1 kW × 1 h × 1 día = 1 kWh por semana; × 52/12 = 4.3333 kWh al mes.
        var kwh = ConsumoAparato.KwhPorMes(cantidad: 1, watts: 1000m, horasPorDia: 1m, diasPorSemana: 1, cicloDeTrabajo: 1m);

        Assert.Equal(4.3333m, Math.Round(kwh, 4));
    }

    [Fact]
    public void KwhPorMes_RefrigeradorCiclando()
    {
        // 250 W = 0.25 kW × 24 h × 7 días = 42 kWh por semana; × 52/12 = 182; × ciclo 0.5 = 91 kWh al mes.
        var aparato = new Aparato
        {
            Nombre = "Refrigerador",
            Categoria = CategoriaAparato.Refrigeracion,
            Cantidad = 1,
            Watts = 250m,
            HorasPorDia = 24m,
            DiasPorSemana = 7,
            CicloDeTrabajo = 0.5m,
        };

        Assert.Equal(91m, Math.Round(ConsumoAparato.KwhPorMes(aparato), 4));
    }

    [Fact]
    public void KwhPorMes_EditableYModelo_DanLoMismo()
    {
        var editable = new AparatoEditable(null, 1, "Tele", CategoriaAparato.Electronica, 3, 120m, 4.5m, 6, 1m);
        var aparato = new Aparato
        {
            Nombre = "Tele",
            Categoria = CategoriaAparato.Electronica,
            Cantidad = 3,
            Watts = 120m,
            HorasPorDia = 4.5m,
            DiasPorSemana = 6,
            CicloDeTrabajo = 1m,
        };

        Assert.Equal(ConsumoAparato.KwhPorMes(aparato), ConsumoAparato.KwhPorMes(editable));
    }

    [Fact]
    public void SemanasPorMes_Es52Entre12()
    {
        Assert.Equal(52m / 12m, ConsumoAparato.SemanasPorMes);
    }
}
