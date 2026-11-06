using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// "¿Cada cuánto te llega el recibo?" (Fase 9c, D3): qué opción se preselecciona.
/// </summary>
public sealed class PeriodicidadReciboTests
{
    [Fact]
    public void SinDatos_CadaDosMeses()
    {
        Assert.Equal(2, PeriodicidadRecibo.Sugerir(null, null, null));
        Assert.Equal(2, PeriodicidadRecibo.Sugerir(null, null, TipoTarifa.Domestica));
    }

    [Fact]
    public void TarifaComercial_CadaMes()
    {
        Assert.Equal(1, PeriodicidadRecibo.Sugerir(null, null, TipoTarifa.Comercial));
    }

    [Fact]
    public void PeriodoLeidoDe30Dias_CadaMes()
    {
        // 1 al 30 de septiembre: 30 días.
        Assert.Equal(1, PeriodicidadRecibo.Sugerir(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), TipoTarifa.Domestica));
    }

    [Fact]
    public void PeriodoLeidoDe61Dias_CadaDosMeses_AunqueLaTarifaSeaComercial()
    {
        // 27 de julio al 25 de septiembre: 61 días. Lo leído del recibo manda sobre la tarifa.
        Assert.Equal(2, PeriodicidadRecibo.Sugerir(new DateOnly(2026, 7, 27), new DateOnly(2026, 9, 25), TipoTarifa.Comercial));
    }

    [Fact]
    public void Limite_45DiasEsCadaMes_46EsCadaDosMeses()
    {
        var inicio = new DateOnly(2026, 8, 1);
        Assert.Equal(1, PeriodicidadRecibo.Sugerir(inicio, inicio.AddDays(44), null));
        Assert.Equal(2, PeriodicidadRecibo.Sugerir(inicio, inicio.AddDays(45), null));
    }

    [Fact]
    public void FechasAlReves_SeIgnoran()
    {
        Assert.Equal(1, PeriodicidadRecibo.Sugerir(new DateOnly(2026, 9, 30), new DateOnly(2026, 9, 1), TipoTarifa.Comercial));
        Assert.Equal(2, PeriodicidadRecibo.Sugerir(new DateOnly(2026, 9, 30), null, null));
    }
}
