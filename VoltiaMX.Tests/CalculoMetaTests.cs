using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class CalculoMetaTests
{
    private static readonly DateOnly Creada = new(2026, 1, 1);

    private static Meta Meta(int porcentaje = 10, bool activa = true, DateOnly? inicio = null)
        => new() { PorcentajeObjetivo = porcentaje, FechaInicio = inicio ?? Creada, Activa = activa };

    // Periodos mensuales: inicio el día 1 de cada mes de 2026.
    private static Periodo Periodo(int mes, decimal kwh, bool estimado = false) => new()
    {
        Inicio = new DateOnly(2026, mes, 1),
        Fin = new DateOnly(2026, mes, 1).AddMonths(1).AddDays(-1),
        KwhTotales = kwh,
        Estimado = estimado,
    };

    // Base 500 kWh y meta del 10 %: objetivo = 500 × (1 − 0.10) = 450 kWh.
    private static readonly Periodo[] Base500 = [Periodo(1, 520m), Periodo(2, 500m)];

    private static ProgresoMeta Calcular(
        Meta meta, IReadOnlyList<Periodo> cerrados, decimal? consumido, decimal? proyectado, int diasParaCorte = 15,
        Func<decimal, decimal>? totalDe = null)
        => CalculoMeta.Calcular(meta, cerrados, consumido, proyectado, diasParaCorte, totalDe);

    [Fact]
    public void Calcular_ProyectadoDebajoDelObjetivo_Cumpliendo()
    {
        // Consumido 200, proyectado 440 ≤ 450. Quedan (450 − 200) / 15 días = 16.67 kWh al día.
        var progreso = Calcular(Meta(), Base500, consumido: 200m, proyectado: 440m);

        Assert.Equal(EstadoMeta.Cumpliendo, progreso.Estado);
        Assert.Equal(500m, progreso.BaseKwh);
        Assert.Equal(450m, progreso.ObjetivoKwh);
        Assert.Equal(200m, progreso.ConsumidoKwh);
        Assert.Equal(440m, progreso.ProyectadoKwh);
        Assert.Equal(16.6667m, Math.Round(progreso.KwhPorDiaPermitidos!.Value, 4));
    }

    [Fact]
    public void Calcular_ProyectadoArribaPeroConsumidoDebajo_EnRiesgo()
    {
        // Proyectado 480 > 450, consumido 300 ≤ 450. (450 − 300) / 15 = 10 kWh al día.
        var progreso = Calcular(Meta(), Base500, consumido: 300m, proyectado: 480m);

        Assert.Equal(EstadoMeta.EnRiesgo, progreso.Estado);
        Assert.Equal(10m, progreso.KwhPorDiaPermitidos);
    }

    [Fact]
    public void Calcular_ConsumidoArribaDelObjetivo_SuperadaYCeroKwhAlDia()
    {
        var progreso = Calcular(Meta(), Base500, consumido: 460m, proyectado: 600m);

        Assert.Equal(EstadoMeta.Superada, progreso.Estado);
        Assert.Equal(0m, progreso.KwhPorDiaPermitidos);
    }

    [Fact]
    public void Calcular_SinDiasRestantes_CeroKwhAlDia()
    {
        Assert.Equal(0m, Calcular(Meta(), Base500, consumido: 300m, proyectado: 300m, diasParaCorte: 0).KwhPorDiaPermitidos);
        Assert.Equal(0m, Calcular(Meta(), Base500, consumido: 300m, proyectado: 300m, diasParaCorte: -3).KwhPorDiaPermitidos);
    }

    [Fact]
    public void Calcular_SinPeriodoCerrado_SinBase()
    {
        var progreso = Calcular(Meta(), [], consumido: 100m, proyectado: 300m);

        Assert.Equal(EstadoMeta.SinBase, progreso.Estado);
        Assert.Null(progreso.BaseKwh);
        Assert.Null(progreso.ObjetivoKwh);
        Assert.Null(progreso.KwhPorDiaPermitidos);
    }

    [Fact]
    public void Calcular_SinLecturasDelPeriodo_SinConsumoMedidoConObjetivo()
    {
        var progreso = Calcular(Meta(), Base500, consumido: null, proyectado: null);

        Assert.Equal(EstadoMeta.SinConsumoMedido, progreso.Estado);
        Assert.Equal(450m, progreso.ObjetivoKwh);
        Assert.Null(progreso.KwhPorDiaPermitidos);
    }

    [Fact]
    public void Calcular_MetaInactiva_Inactiva()
    {
        var progreso = Calcular(Meta(activa: false), Base500, consumido: 200m, proyectado: 440m);

        Assert.Equal(EstadoMeta.Inactiva, progreso.Estado);
        Assert.Null(progreso.ObjetivoKwh);
        Assert.Empty(progreso.Historial);
    }

    [Fact]
    public void Calcular_BaseEstimada_LoAvisa()
    {
        var progreso = Calcular(Meta(), [Periodo(1, 520m), Periodo(2, 500m, estimado: true)], 200m, 440m);

        Assert.True(progreso.BaseEstimada);
        Assert.False(Calcular(Meta(), Base500, 200m, 440m).BaseEstimada);
    }

    [Fact]
    public void Calcular_AhorroEnPesosDelObjetivo_ConLaTarifa()
    {
        // A $2 por kWh: base 500 = $1000, objetivo 450 = $900 → $100 del objetivo.
        var progreso = Calcular(Meta(), Base500, 200m, 440m, totalDe: kwh => kwh * 2m);

        Assert.Equal(100m, progreso.AhorroPesosObjetivo);
        Assert.Null(Calcular(Meta(), Base500, 200m, 440m).AhorroPesosObjetivo);
    }

    // Ene 500, feb 450 (−10 %: cumple), mar 470 (+4.4 %: reducción negativa, no cumple),
    // abr 400 (−14.9 %: cumple), may 360 (−10 % exacto: cumple).
    private static readonly Periodo[] Cinco =
        [Periodo(1, 500m), Periodo(2, 450m), Periodo(3, 470m), Periodo(4, 400m), Periodo(5, 360m)];

    [Fact]
    public void Calcular_Historial_ReduccionCumplidosYNoCumplidosDelMasReciente()
    {
        var progreso = Calcular(Meta(inicio: new DateOnly(2026, 2, 1)), Cinco, 100m, 300m);

        Assert.Equal([5, 4, 3, 2], progreso.Historial.Select(f => f.Inicio.Month));
        Assert.Equal([360m, 400m, 470m, 450m], progreso.Historial.Select(f => f.Kwh));
        // (400 − 360) / 400 = 0.10; (470 − 400) / 470 = 0.1489; (450 − 470) / 450 = −0.0444; (500 − 450) / 500 = 0.10.
        Assert.Equal([0.1m, 0.1489m, -0.0444m, 0.1m], progreso.Historial.Select(f => Math.Round(f.Reduccion, 4)));
        Assert.Equal([true, true, false, true], progreso.Historial.Select(f => f.Cumplio));
    }

    [Fact]
    public void Calcular_Racha_CumplidosSeguidosDesdeElMasReciente()
    {
        Assert.Equal(2, Calcular(Meta(inicio: new DateOnly(2026, 2, 1)), Cinco, 100m, 300m).Racha);
        Assert.Equal(0, Calcular(Meta(inicio: new DateOnly(2026, 2, 1)), [.. Cinco, Periodo(6, 380m)], 100m, 300m).Racha);
    }

    [Fact]
    public void Calcular_PeriodosAnterioresALaFechaDeInicio_NoCuentan()
    {
        // Creada el 1 de marzo: feb (450) queda fuera, aunque sirva de "anterior" para marzo.
        var progreso = Calcular(Meta(inicio: new DateOnly(2026, 3, 1)), Cinco, 100m, 300m);

        Assert.Equal([5, 4, 3], progreso.Historial.Select(f => f.Inicio.Month));
    }

    [Fact]
    public void Calcular_MetaRecienCreadaSinPeriodosPosteriores_HistorialVacioYRacha0()
    {
        var progreso = Calcular(Meta(inicio: new DateOnly(2026, 6, 15)), Cinco, 100m, 300m);

        Assert.Empty(progreso.Historial);
        Assert.Equal(0, progreso.Racha);
        Assert.Equal(EstadoMeta.Cumpliendo, progreso.Estado); // base 360 → objetivo 324; proyectado 300.
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void Validar_PorcentajeDe1A50(int porcentaje, bool valido)
    {
        Assert.Equal(valido ? [] : [CodigoErrorMeta.PorcentajeFueraDeRango], MetaValidador.Validar(porcentaje));
    }

    // Fase 8 (D8): los pesos salen de la tarifa actual. Con $2 por kWh y $50 de cargo fijo: Total(k) = 50 + 2k.
    private static decimal Total(decimal kwh) => 50m + 2m * kwh;

    [Fact]
    public void Calcular_EnPesos_ObjetivoARitmoYLoQueFalta()
    {
        // Objetivo 450 kWh → $950. A este ritmo 480 kWh → $1,010. Te faltan 1,010 − 950 = $60.
        var progreso = Calcular(Meta(), Base500, consumido: 300m, proyectado: 480m, totalDe: Total);

        Assert.Equal(EstadoMeta.EnRiesgo, progreso.Estado);
        Assert.Equal(950m, progreso.ObjetivoPesos);
        Assert.Equal(1_010m, progreso.MontoARitmo);
        Assert.Equal(60m, progreso.FaltaPorAhorrar);
        Assert.Equal(1_050m, progreso.BasePesos);
        Assert.Equal(650m, progreso.ConsumidoPesos);
    }

    [Fact]
    public void Calcular_EnPesos_LoQueFaltaNuncaEsNegativo()
    {
        // A este ritmo 440 kWh → $930, debajo del objetivo de $950: no falta nada.
        var progreso = Calcular(Meta(), Base500, consumido: 200m, proyectado: 440m, totalDe: Total);

        Assert.Equal(EstadoMeta.Cumpliendo, progreso.Estado);
        Assert.Equal(0m, progreso.FaltaPorAhorrar);
    }

    [Fact]
    public void Calcular_EnPesos_ElHistorialTraeElMontoConLaTarifaActual()
    {
        // Feb 500 kWh contra ene 520 kWh: Total(500) = $1,050.
        var progreso = Calcular(Meta(), Base500, consumido: 200m, proyectado: 440m, totalDe: Total);

        Assert.Equal(1_050m, progreso.Historial.Single().Monto);
    }

    [Fact]
    public void Calcular_SinTarifa_NoHayPesosYLosEstadosNoCambian()
    {
        var conPesos = Calcular(Meta(), Base500, consumido: 300m, proyectado: 480m, totalDe: Total);
        var sinPesos = Calcular(Meta(), Base500, consumido: 300m, proyectado: 480m);

        Assert.Equal(conPesos.Estado, sinPesos.Estado);
        Assert.Null(sinPesos.ObjetivoPesos);
        Assert.Null(sinPesos.MontoARitmo);
        Assert.Null(sinPesos.FaltaPorAhorrar);
        Assert.Null(sinPesos.Historial.Single().Monto);
    }
}
