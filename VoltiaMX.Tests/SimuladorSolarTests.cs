using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class SimuladorSolarTests
{
    private static readonly TarifaCalculator Calculadora = new();
    private static readonly DacService Dac = new();

    private static ParametrosSolar Parametros(decimal? consumo = 600m, decimal porcentaje = 100m, decimal horas = 5m,
        decimal eficiencia = 0.80m, decimal watts = 550m, decimal? costo = null)
        => new(consumo, porcentaje, horas, eficiencia, watts, costo);

    /// <summary>
    /// Lugar con consumo medido, la tarifa Escalonada ($1 hasta 75, $2 hasta 140, $3 arriba; sin DAC) y periodo mensual.
    /// </summary>
    private static DatosLugarSolar Lugar(decimal? medido = 600m, Tarifa? tarifa = null, int meses = 1, IReadOnlyList<decimal>? historial = null)
        => new("Casa", false, medido, tarifa ?? TarifasDePrueba.Escalonada(), meses, historial ?? []);

    private static ResultadoSolar Calcular(ParametrosSolar p, DatosLugarSolar? datos = null)
        => SimuladorSolar.Calcular(p, datos ?? Lugar(), Calculadora, Dac);

    [Fact]
    public void CasoDeReferencia_SeReproduceAMano()
    {
        // Generación por kWp = 5 × 30.41667 × 0.8 = 121.6667 kWh/mes.
        // kWp necesarios = 600 / 121.6667 = 4.9315; paneles = techo(4931.5 / 550) = techo(8.966) = 9.
        // kWp instalados = 9 × 550 / 1000 = 4.95; generación = 4.95 × 121.6667 = 602.25 kWh/mes
        // (4.95 × 5 × 0.8 × 365 / 12 = 7227 / 12 = 602.25 exacto).
        var r = Calcular(Parametros());

        Assert.Equal(121.6667m, Math.Round(r.GeneracionPorKwpMes, 4));
        Assert.Equal(4.9315m, Math.Round(r.KwpNecesarios, 4));
        Assert.Equal(9, r.Paneles);
        Assert.Equal(4.95m, r.KwpInstalados);
        Assert.Equal(602.25m, r.GeneracionMensualKwh);
        Assert.Equal(600m, r.KwhAprovechadosMes);
        Assert.Equal(2.25m, r.ExcedenteKwhMes);
    }

    [Fact]
    public void LaMitadDelConsumo_MenosPaneles()
    {
        // A cubrir 300: techo(300 × 12 × 1000 / (5 × 0.8 × 365 × 550)) = techo(3,600,000 / 803,000) = techo(4.48) = 5.
        // kWp = 2.75; generación = 2.75 × 4 × 365 / 12 = 334.5833; aprovechados = 334.58 (menos que 600).
        var r = Calcular(Parametros(porcentaje: 50m));

        Assert.Equal(5, r.Paneles);
        Assert.Equal(2.75m, r.KwpInstalados);
        Assert.Equal(334.58m, Math.Round(r.GeneracionMensualKwh, 2));
        Assert.Equal(r.GeneracionMensualKwh, r.KwhAprovechadosMes);
        Assert.Equal(0m, r.ExcedenteKwhMes);
    }

    [Fact]
    public void NumeroExactoDePaneles()
    {
        // 6 h × 0.8 × 365 / 12 = 146 kWh por kWp exacto. 584 kWh → 4 kWp → 4000 / 500 = 8 paneles exactos.
        var r = Calcular(Parametros(consumo: 584m, horas: 6m, watts: 500m));

        Assert.Equal(146m, r.GeneracionPorKwpMes);
        Assert.Equal(8, r.Paneles);
        Assert.Equal(584m, r.GeneracionMensualKwh);
        Assert.Equal(0m, r.ExcedenteKwhMes);
    }

    [Fact]
    public void ConsumoPequeno_AlMenosUnPanel_YElAhorroSeLimitaAlConsumo()
    {
        // 5 kWh: techo(5 × 12000 / 803000) = techo(0.075) = 1 panel. Genera 0.55 × 121.6667 = 66.92;
        // aprovecha 5 y el excedente (61.92) no vale pesos: ahorro = $5 (los 5 kWh del rango de $1).
        var r = Calcular(Parametros(consumo: 5m), Lugar(medido: 5m));

        Assert.Equal(1, r.Paneles);
        Assert.Equal(5m, r.KwhAprovechadosMes);
        Assert.Equal(61.92m, Math.Round(r.ExcedenteKwhMes, 2));
        Assert.Equal(5m, r.AhorroMes);
    }

    [Fact]
    public void Ahorro_TarifaQueCruzaRangos()
    {
        // 200 kWh, 50 %, 6 h, 0.8, 500 W: a cubrir 100 → techo(100 / 146 × 1000 / 500) = techo(1.37) = 2 paneles
        // → 1 kWp → 146 kWh aprovechados.
        // Antes (200): 75 × $1 + 65 × $2 + 60 × $3 = 75 + 130 + 180 = 385.
        // Después (200 − 146 = 54): 54 × $1 = 54. Ahorro = 331 al mes; 3,972 al año.
        var r = Calcular(Parametros(consumo: 200m, porcentaje: 50m, horas: 6m, watts: 500m), Lugar(medido: 200m));

        Assert.Equal(2, r.Paneles);
        Assert.Equal(146m, r.KwhAprovechadosMes);
        Assert.Equal(EstadoPesosSolar.Normal, r.EstadoPesos);
        Assert.Equal(331m, r.AhorroMes);
        Assert.Equal(3_972m, r.AhorroAnio);
    }

    [Fact]
    public void Ahorro_PeriodoBimestral_SeCalculaSobreElPeriodoYSeDivide()
    {
        // Bimestral: rangos × 2 (150 a $1, 130 a $2, resto a $3).
        // Antes (400): 150 + 260 + 120 × 3 = 770. Después (400 − 292 = 108): 108. Periodo = 662; mes = 331.
        var r = Calcular(Parametros(consumo: 200m, porcentaje: 50m, horas: 6m, watts: 500m), Lugar(medido: 200m, meses: 2));

        Assert.Equal(662m, r.AhorroPeriodo);
        Assert.Equal(331m, r.AhorroMes);
        Assert.Equal(3_972m, r.AhorroAnio);
    }

    [Fact]
    public void SinCosto_NoHayInversionNiRecuperacion()
    {
        var r = Calcular(Parametros(costo: null));
        var cero = Calcular(Parametros(costo: 0m));

        Assert.Null(r.Inversion);
        Assert.Null(r.RecuperacionMeses);
        Assert.Null(cero.Inversion);
    }

    [Fact]
    public void ConCosto_InversionYMesesDeRecuperacion()
    {
        // Ahorro: antes (600) = 75 + 130 + 460 × 3 = 1,585; después (0) = 0 → $1,585 al mes.
        // Inversión = 4.95 kWp × $20,000 = $99,000; recuperación = 99,000 / 1,585 = 62.46 meses.
        var r = Calcular(Parametros(costo: 20_000m));

        Assert.Equal(1_585m, r.AhorroMes);
        Assert.Equal(99_000m, r.Inversion);
        Assert.Equal(62.46m, Math.Round(r.RecuperacionMeses!.Value, 2));
        Assert.False(r.RecuperacionExcedeMaximo);
    }

    [Fact]
    public void RecuperacionDeMasDeVeinteAnios()
    {
        // 4.95 × 1,000,000 = 4,950,000 / 1,585 = 3,123 meses > 240.
        var r = Calcular(Parametros(costo: 1_000_000m));

        Assert.True(r.RecuperacionExcedeMaximo);
    }

    [Fact]
    public void SinConsumoMedido_SoloKwh()
    {
        var r = Calcular(Parametros(costo: 20_000m), Lugar(medido: null));

        Assert.Equal(EstadoPesosSolar.SinConsumoMedido, r.EstadoPesos);
        Assert.Equal(9, r.Paneles);
        Assert.Null(r.AhorroMes);
        Assert.Null(r.AhorroAnio);
        Assert.Equal(99_000m, r.Inversion);
        Assert.Null(r.RecuperacionMeses);
    }

    [Fact]
    public void SinTarifa_SoloKwh()
    {
        var r = Calcular(Parametros(), DatosLugarSolar.SinLugar);

        Assert.Equal(EstadoPesosSolar.SinTarifa, r.EstadoPesos);
        Assert.Null(r.AhorroMes);
        Assert.Null(r.DacAntes);
        Assert.Equal(602.25m, r.GeneracionMensualKwh);
    }

    [Fact]
    public void TarifaComercial_SinDac()
    {
        var r = Calcular(Parametros(), Lugar(tarifa: TarifasDePrueba.RangoUnico(), historial: [.. Enumerable.Repeat(600m, 12)]));

        Assert.Null(r.DacAntes);
        Assert.Null(r.DacDespues);
        // Rango único a $2: 600 × 2 = 1,200 al mes.
        Assert.Equal(1_200m, r.AhorroMes);
    }

    [Fact]
    public void ConLimiteDac_EstimaElEfecto()
    {
        // Historial de 300 kWh/mes contra un límite de 250: excede. Con 300 aprovechados al mes queda en 0.
        var tarifa = TarifasDePrueba.Escalonada();
        tarifa.LimiteDacKwhMensual = 250m;
        var r = Calcular(Parametros(consumo: 300m), Lugar(medido: 300m, tarifa: tarifa, historial: [.. Enumerable.Repeat(300m, 12)]));

        Assert.True(r.DacAntes!.ExcedeLimite);
        Assert.Equal(300m, r.DacAntes.PromedioMensualKwh);
        Assert.False(r.DacDespues!.ExcedeLimite);
        Assert.Equal(0m, r.DacDespues.PromedioMensualKwh);
    }

    [Fact]
    public void Calcular_ConParametrosInvalidos_Lanza()
        => Assert.Throws<ArgumentException>(() => Calcular(Parametros(eficiencia: 0.99m)));

    [Theory]
    [InlineData(null, CodigoErrorSolar.ConsumoRequerido)]
    [InlineData(0.0, CodigoErrorSolar.ConsumoFueraDeRango)]
    [InlineData(100_000.01, CodigoErrorSolar.ConsumoFueraDeRango)]
    public void Validador_Consumo(double? consumo, CodigoErrorSolar esperado)
        => Assert.Equal([esperado], SimuladorSolarValidador.Validar(Parametros(consumo: (decimal?)consumo)));

    [Fact]
    public void Validador_Limites()
    {
        static IReadOnlyList<CodigoErrorSolar> V(ParametrosSolar p) => SimuladorSolarValidador.Validar(p);

        Assert.Empty(V(Parametros(consumo: 100_000m, porcentaje: 10m, horas: 1m, eficiencia: 0.50m, watts: 100m, costo: 0m)));
        Assert.Empty(V(Parametros(consumo: 0.01m, porcentaje: 100m, horas: 9m, eficiencia: 0.95m, watts: 800m, costo: 1_000_000m)));
        Assert.Equal([CodigoErrorSolar.PorcentajeFueraDeRango], V(Parametros(porcentaje: 9.9m)));
        Assert.Equal([CodigoErrorSolar.PorcentajeFueraDeRango], V(Parametros(porcentaje: 100.1m)));
        Assert.Equal([CodigoErrorSolar.HorasFueraDeRango], V(Parametros(horas: 0.9m)));
        Assert.Equal([CodigoErrorSolar.HorasFueraDeRango], V(Parametros(horas: 9.1m)));
        Assert.Equal([CodigoErrorSolar.EficienciaFueraDeRango], V(Parametros(eficiencia: 0.49m)));
        Assert.Equal([CodigoErrorSolar.EficienciaFueraDeRango], V(Parametros(eficiencia: 0.96m)));
        Assert.Equal([CodigoErrorSolar.WattsFueraDeRango], V(Parametros(watts: 99m)));
        Assert.Equal([CodigoErrorSolar.WattsFueraDeRango], V(Parametros(watts: 801m)));
        Assert.Equal([CodigoErrorSolar.CostoFueraDeRango], V(Parametros(costo: -1m)));
        Assert.Equal([CodigoErrorSolar.CostoFueraDeRango], V(Parametros(costo: 1_000_000.01m)));
        Assert.Equal(
            [CodigoErrorSolar.ConsumoRequerido, CodigoErrorSolar.EficienciaFueraDeRango, CodigoErrorSolar.WattsFueraDeRango],
            V(Parametros(consumo: null, eficiencia: 0.99m, watts: 900m)));
    }

    [Fact]
    public void ValoresPorDefecto_SonValidosConUnConsumo()
    {
        var p = new ParametrosSolar(600m, ValoresSolarPorDefecto.PorcentajeCubrir, ValoresSolarPorDefecto.HorasSol,
            ValoresSolarPorDefecto.Eficiencia, ValoresSolarPorDefecto.WattsPanel, null);
        Assert.Empty(SimuladorSolarValidador.Validar(p));
        Assert.Equal(365m / 12m, SimuladorSolar.DiasPorMes);
    }

    [Fact]
    public async Task ObtenerDatos_ConLaDemo_UsaElConsumoMedidoYElHistorialDac()
    {
        var hoy = new DateOnly(2026, 11, 4);
        using var fabrica = await FabricaDePrueba.CrearAsync();
        await new DatosDemoService(fabrica, Calculadora).CargarAsync(hoy);
        await using var db = fabrica.CreateDbContext();
        var lugar = await db.Lugares.Include(l => l.Lecturas).Include(l => l.Periodos).Include(l => l.Tarifa).SingleAsync();
        var servicio = new SimuladorSolarService(fabrica, Calculadora, Dac);

        var datos = await servicio.ObtenerDatosAsync(lugar.Id, hoy);

        Assert.Equal(ConsumoMedido.KwhPorMes(lugar, hoy), datos.KwhMedidoPorMes);
        Assert.NotNull(datos.KwhMedidoPorMes);
        Assert.Equal(HistorialMensualDac.Desde(lugar.Periodos, 2), datos.HistorialMensual);
        Assert.Equal((lugar.Nombre, true, 2, lugar.Tarifa!.Nombre), (datos.NombreLugar, datos.EsDemo, datos.MesesPorPeriodo, datos.Tarifa!.Nombre));
        Assert.NotNull(servicio.Calcular(Parametros(consumo: datos.KwhMedidoPorMes), datos).DacAntes);
    }

    [Fact]
    public async Task ObtenerDatos_SinLugar_DatosVacios()
    {
        using var fabrica = await FabricaDePrueba.CrearAsync();
        var servicio = new SimuladorSolarService(fabrica, Calculadora, Dac);

        Assert.Same(DatosLugarSolar.SinLugar, await servicio.ObtenerDatosAsync(null, new DateOnly(2026, 11, 4)));
        Assert.Same(DatosLugarSolar.SinLugar, await servicio.ObtenerDatosAsync(999, new DateOnly(2026, 11, 4)));
    }
}
