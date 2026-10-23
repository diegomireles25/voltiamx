using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class SimuladorAhorroTests : IAsyncLifetime
{
    private FabricaDePrueba _fabrica = null!;
    private SimuladorAhorroService _simulador = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _simulador = new SimuladorAhorroService(_fabrica, new TarifaCalculator(), new DacService());
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Mensual: 0–150 a $1, 150–300 a $2 y 300+ a $3 por kWh; sin cargo fijo; límite DAC de 500 kWh.
    /// </summary>
    private static Tarifa TresRangos(decimal? limiteDac = 500m) => new()
    {
        Nombre = "Tres rangos",
        Tipo = limiteDac is null ? TipoTarifa.Comercial : TipoTarifa.Domestica,
        LimiteDacKwhMensual = limiteDac,
        Rangos =
        [
            new() { Nombre = "Básico", KwhDesde = 0m, KwhHasta = 150m, PrecioPorKwh = 1m, Orden = 1 },
            new() { Nombre = "Intermedio", KwhDesde = 150m, KwhHasta = 300m, PrecioPorKwh = 2m, Orden = 2 },
            new() { Nombre = "Excedente", KwhDesde = 300m, KwhHasta = null, PrecioPorKwh = 3m, Orden = 3 },
        ]
    };

    // 1 kW × 24 h × 7 días × 52/12 = 728 kWh al mes; con ciclo 0.25 → 182 kWh al mes.
    // "Menos horas" a 12 h deja 91 kWh: el ahorro es 91 kWh al mes.
    private static Aparato Aire() => new()
    {
        Id = 1,
        LugarId = 1,
        Nombre = "Aire",
        Categoria = CategoriaAparato.Climatizacion,
        Cantidad = 1,
        Watts = 1_000m,
        HorasPorDia = 24m,
        DiasPorSemana = 7,
        CicloDeTrabajo = 0.25m,
    };

    private static readonly EscenarioAhorro DoceHoras = EscenarioAhorro.MenosHoras(12m, 7);

    private ResultadoSimulacion Simular(
        EscenarioAhorro escenario, decimal? medido, Tarifa? tarifa = null, int meses = 1, IReadOnlyList<decimal>? historial = null)
        => _simulador.Simular(new EntradaSimulacion(Aire(), escenario, tarifa ?? TresRangos(), meses, medido, historial ?? []));

    [Fact]
    public void Simular_AhorroEnUnSoloRango_ValeElPrecioDelExcedente()
    {
        // Medido 500: Calcular(500) = 150×1 + 150×2 + 200×3 = 1050.
        // Con 91 kWh menos, Calcular(409) = 150 + 300 + 109×3 = 777. Ahorro = 273 al mes = 91 × $3.
        var resultado = Simular(DoceHoras, medido: 500m);

        Assert.Equal(EstadoSimulacion.Normal, resultado.Estado);
        Assert.Equal(182m, Math.Round(resultado.KwhPorMesAntes, 4));
        Assert.Equal(91m, Math.Round(resultado.KwhPorMesDespues, 4));
        Assert.Equal(91m, Math.Round(resultado.AhorroKwhPorMes, 4));
        Assert.Equal(273m, Math.Round(resultado.AhorroPesosMes!.Value, 2));
        Assert.Equal(273m, Math.Round(resultado.AhorroPesosPeriodo!.Value, 2));
        Assert.Equal(3_276m, Math.Round(resultado.AhorroPesosAnio!.Value, 2));
    }

    [Fact]
    public void Simular_AhorroQueCruzaDosRangos()
    {
        // Medido 350: Calcular(350) = 150 + 300 + 50×3 = 600.
        // 350 − 91 = 259: Calcular(259) = 150 + 109×2 = 368. Ahorro = 232 = 50×3 + 41×2.
        var resultado = Simular(DoceHoras, medido: 350m);

        Assert.Equal(232m, Math.Round(resultado.AhorroPesosMes!.Value, 2));
    }

    [Fact]
    public void Simular_Bimestral_ElAhorroDelPeriodoEsDobleYElMensualLaMitad()
    {
        // Bimestral: rangos de 0–300, 300–600 y 600+. Medido 500 al mes → 1000 en el periodo.
        // Calcular(1000) = 300 + 600 + 400×3 = 2100; Calcular(818) = 300 + 600 + 218×3 = 1554.
        // Ahorro del periodo = 546 (182 kWh × $3); al mes = 273; al año = 3276.
        var resultado = Simular(DoceHoras, medido: 500m, meses: 2);

        Assert.Equal(546m, Math.Round(resultado.AhorroPesosPeriodo!.Value, 2));
        Assert.Equal(273m, Math.Round(resultado.AhorroPesosMes!.Value, 2));
        Assert.Equal(3_276m, Math.Round(resultado.AhorroPesosAnio!.Value, 2));
    }

    [Fact]
    public void Simular_EquipoQueConsumeMas_ConsumiriasMasSinRecuperacion()
    {
        // 2 kW con ciclo 0.25 = 364 kWh al mes contra 182: consumirías 182 kWh más.
        // Medido 500: Calcular(682) = 150 + 300 + 382×3 = 1596; 1050 − 1596 = −546 al mes.
        var resultado = Simular(EscenarioAhorro.EquipoEficiente(2_000m, precioEquipo: 5_000m), medido: 500m);

        Assert.Equal(EstadoSimulacion.ConsumiriasMas, resultado.Estado);
        Assert.Equal(-182m, Math.Round(resultado.AhorroKwhPorMes, 4));
        Assert.Equal(-546m, Math.Round(resultado.AhorroPesosMes!.Value, 2));
        Assert.Null(resultado.MesesRecuperacion);
        Assert.False(resultado.RecuperacionMasDe10Anios);
    }

    [Fact]
    public void Simular_AhorroMayorQueElConsumoMedido_SeLimitaAlConsumo()
    {
        // Medido 50 y ahorro 91: solo se pueden ahorrar 50 kWh. Calcular(50) − Calcular(0) = 50.
        var resultado = Simular(DoceHoras, medido: 50m);

        Assert.Equal(EstadoSimulacion.AhorroExcedeConsumo, resultado.Estado);
        Assert.Equal(91m, Math.Round(resultado.AhorroKwhPorMes, 4));
        Assert.Equal(50m, Math.Round(resultado.AhorroPesosMes!.Value, 2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Simular_SinConsumoMedido_SoloKwh(int? medido)
    {
        var resultado = Simular(DoceHoras, medido);

        Assert.Equal(EstadoSimulacion.SinConsumoMedido, resultado.Estado);
        Assert.Equal(91m, Math.Round(resultado.AhorroKwhPorMes, 4));
        Assert.Null(resultado.AhorroPesosPeriodo);
        Assert.Null(resultado.AhorroPesosMes);
        Assert.Null(resultado.AhorroPesosAnio);
        Assert.Null(resultado.MesesRecuperacion);
    }

    [Fact]
    public void Simular_SinPrecioDelEquipo_SinRecuperacion()
    {
        // 500 W con el mismo ciclo = 91 kWh: el mismo ahorro de 91 kWh ($273 al mes con medido 500).
        var resultado = Simular(EscenarioAhorro.EquipoEficiente(500m), medido: 500m);

        Assert.Equal(273m, Math.Round(resultado.AhorroPesosMes!.Value, 2));
        Assert.Null(resultado.MesesRecuperacion);
    }

    [Fact]
    public void Simular_PrecioQueSeRecuperaEnSeisMeses()
    {
        // 1638 / 273 al mes = 6 meses.
        var resultado = Simular(EscenarioAhorro.EquipoEficiente(500m, precioEquipo: 1_638m), medido: 500m);

        Assert.Equal(6m, Math.Round(resultado.MesesRecuperacion!.Value, 4));
        Assert.False(resultado.RecuperacionMasDe10Anios);
    }

    [Fact]
    public void Simular_PrecioQueTardaMasDe120Meses_MasDe10Anios()
    {
        // 33033 / 273 = 121 meses.
        var resultado = Simular(EscenarioAhorro.EquipoEficiente(500m, precioEquipo: 33_033m), medido: 500m);

        Assert.Equal(121m, Math.Round(resultado.MesesRecuperacion!.Value, 4));
        Assert.True(resultado.RecuperacionMasDe10Anios);
    }

    [Fact]
    public void Simular_EquipoEficienteSinCiclo_ConservaElCicloDelAparato()
    {
        // 500 W con el ciclo original 0.25 = 91 kWh; con ciclo 0.5 serían 182 (sin ahorro).
        Assert.Equal(91m, Math.Round(Simular(EscenarioAhorro.EquipoEficiente(500m), null).KwhPorMesDespues, 4));
        Assert.Equal(182m, Math.Round(Simular(EscenarioAhorro.EquipoEficiente(500m, 0.5m), null).KwhPorMesDespues, 4));
    }

    [Fact]
    public void Simular_DacQueBajaDeNivel()
    {
        // 12 meses de 500 contra límite 500 = 100 % → Alto. Restando 91: 409 / 500 = 81.8 % → Medio.
        var resultado = Simular(DoceHoras, medido: 500m, historial: Enumerable.Repeat(500m, 12).ToList());

        Assert.Equal(NivelRiesgoDac.Alto, resultado.DacAntes!.Nivel);
        Assert.Equal(500m, resultado.DacAntes.PromedioMensualKwh);
        Assert.Equal(NivelRiesgoDac.Medio, resultado.DacDespues!.Nivel);
        Assert.Equal(409m, Math.Round(resultado.DacDespues.PromedioMensualKwh, 4));
    }

    [Fact]
    public void Simular_DacNoBajaDeCero()
    {
        var resultado = Simular(DoceHoras, medido: 500m, historial: [10m, 10m, 500m]);

        // (0 + 0 + 409) / 3 = 136.33.
        Assert.Equal(136.3333m, Math.Round(resultado.DacDespues!.PromedioMensualKwh, 4));
    }

    [Fact]
    public void Simular_TarifaComercialSinLimite_SinDac()
    {
        var resultado = Simular(DoceHoras, medido: 500m, tarifa: TresRangos(limiteDac: null), historial: [500m, 500m]);

        Assert.Null(resultado.DacAntes);
        Assert.Null(resultado.DacDespues);
        Assert.Equal(273m, Math.Round(resultado.AhorroPesosMes!.Value, 2));
    }

    [Fact]
    public void Simular_SinHistorial_SinDac()
    {
        Assert.Null(Simular(DoceHoras, medido: 500m).DacAntes);
    }

    [Fact]
    public void Simular_EscenarioInvalido_Lanza()
    {
        Assert.Throws<ArgumentException>(() => Simular(EscenarioAhorro.MenosHoras(30m, 7), 500m));
    }

    // ---- SimularAsync: datos reales del lugar ----

    private async Task<(int LugarId, int AparatoId)> CrearLugarConAireAsync(string nombre, decimal kwhPeriodo)
    {
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.OrderBy(t => t.Id).FirstAsync();
        var lugar = new Lugar { Nombre = nombre, Tipo = TipoLugar.Casa, TarifaId = tarifa.Id, DiaCorte = 1, MesesPorPeriodo = 2 };
        lugar.Periodos.Add(new Periodo { Inicio = new(2026, 7, 2), Fin = new(2026, 9, 1), KwhTotales = kwhPeriodo });
        var aire = Aire();
        aire.Id = 0;
        lugar.Aparatos.Add(aire);
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return (lugar.Id, aire.Id);
    }

    [Fact]
    public async Task SimularAsync_UsaElLugarYElAparatoCorrectos()
    {
        var (casa, aireCasa) = await CrearLugarConAireAsync("Mi casa", 728m);
        var (_, aireLocal) = await CrearLugarConAireAsync("Local", 2_000m);

        var resultado = await _simulador.SimularAsync(casa, aireCasa, DoceHoras, new DateOnly(2026, 10, 19));

        // Lo mismo que la función pura con los datos de "Mi casa": 728 / 2 = 364 al mes y su único periodo.
        await using var db = _fabrica.CreateDbContext();
        var tarifaId = await db.Lugares.Where(l => l.Id == casa).Select(l => l.TarifaId).SingleAsync();
        var tarifa = await db.Tarifas.Include(t => t.Rangos).SingleAsync(t => t.Id == tarifaId);
        var esperado = _simulador.Simular(new EntradaSimulacion(Aire(), DoceHoras, tarifa, 2, 364m, [364m, 364m]));
        Assert.NotNull(resultado);
        Assert.Equal(esperado.AhorroPesosMes, resultado.AhorroPesosMes);
        Assert.Equal(esperado.DacDespues, resultado.DacDespues);
        Assert.Equal(91m, Math.Round(resultado.AhorroKwhPorMes, 4));

        // Un aparato de otro lugar no se simula con los datos de este.
        Assert.Null(await _simulador.SimularAsync(casa, aireLocal, DoceHoras, new DateOnly(2026, 10, 19)));
    }

    [Fact]
    public void HistorialMensualDac_RepartePeriodosEntreSusMesesYTomaLosUltimos12()
    {
        var periodos = Enumerable.Range(1, 7)
            .Select(i => new Periodo { Inicio = new DateOnly(2025, 1, 1).AddMonths(2 * i), KwhTotales = 100m * i })
            .Reverse()
            .ToList();

        var mensuales = HistorialMensualDac.Desde(periodos, meses: 2);

        // 7 bimestres = 14 meses; quedan los últimos 12, del más antiguo al más reciente.
        Assert.Equal([100m, 100m, 150m, 150m, 200m, 200m, 250m, 250m, 300m, 300m, 350m, 350m], mensuales);
    }
}
