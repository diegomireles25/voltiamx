using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9k-2: un recibo con el consumo leído (y coherente) se guarda con ESE consumo, no estimado desde el monto; y
/// el importe de energía del recibo se compara con la tarifa guardada. Datos SINTÉTICOS del recibo A.
/// </summary>
public sealed class ReciboConConsumoTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 11, 9);

    private readonly TarifaCalculator _calculadora = new();
    private FabricaDePrueba _fabrica = null!;
    private RecibosService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new RecibosService(_fabrica, new PresupuestoInversoService(_calculadora), _calculadora);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tarifa de prueba con los precios del recibo A, en límites MENSUALES: 0–150 a $1.00, 150–300 a $1.20, 300–450 a
    /// $1.50 y 450+ a $4.00; sin cargo fijo. En un bimestre los límites se duplican (300, 600 y 900).
    /// </summary>
    private static Tarifa CuatroTramos() => new()
    {
        Nombre = "Cuatro tramos",
        Tipo = TipoTarifa.Domestica,
        Rangos =
        [
            new() { Nombre = "Básico", KwhDesde = 0m, KwhHasta = 150m, PrecioPorKwh = 1.00m, Orden = 1 },
            new() { Nombre = "Intermedio 1", KwhDesde = 150m, KwhHasta = 300m, PrecioPorKwh = 1.20m, Orden = 2 },
            new() { Nombre = "Intermedio 2", KwhDesde = 300m, KwhHasta = 450m, PrecioPorKwh = 1.50m, Orden = 3 },
            new() { Nombre = "Excedente", KwhDesde = 450m, KwhHasta = null, PrecioPorKwh = 4.00m, Orden = 4 },
        ],
    };

    private async Task<int> LugarAsync(Tarifa tarifa, int meses = 2)
    {
        await using var db = _fabrica.CreateDbContext();
        var lugar = new Lugar { Nombre = "Mi casa", DiaCorte = 10, MesesPorPeriodo = meses, Tarifa = tarifa };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private async Task<List<Periodo>> PeriodosAsync(int lugarId)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Periodos.Where(p => p.LugarId == lugarId).OrderBy(p => p.Fin).ToListAsync();
    }

    [Fact]
    public async Task ConConsumoLeido_GuardaLosKwhExactos_NoEstimado_YElMontoConLaTarifaDelLugar()
    {
        // Recibo A: 1,000 kWh, importe 1,751.60, fin del periodo 10 ago 2026.
        // Con la tarifa del lugar (bimestral): 300 × 1.00 + 300 × 1.20 + 300 × 1.50 + 100 × 4.00 = $1,510.00.
        var lugarId = await LugarAsync(CuatroTramos());

        var r = await _servicio.AgregarConConsumoAsync(lugarId, 1_000m, 1_751.60m, new DateOnly(2026, 8, 10), Hoy);

        Assert.Null(r.Error);
        var periodo = Assert.Single(await PeriodosAsync(lugarId));
        Assert.Equal((1_000m, false, 1_751.60m, 1_510.00m), (periodo.KwhTotales, periodo.Estimado, periodo.MontoRecibo, periodo.Monto));
        // Bimestral sin recibo anterior: 10 ago − 2 meses + 1 = 11 jun – 10 ago.
        Assert.Equal((new DateOnly(2026, 6, 11), new DateOnly(2026, 8, 10)), (periodo.Inicio, periodo.Fin));
        Assert.Equal((1_000m, 1_510.00m), (r.Kwh, r.Monto));
    }

    [Fact]
    public async Task ConConsumo_RespetaLasReglasDeLosCortes_YLaPeriodicidad()
    {
        var lugarId = await LugarAsync(CuatroTramos(), meses: 2);

        // Primer recibo con "cada mes": se guarda la periodicidad (D3 de la 9c) → 11 jul – 10 ago.
        var primero = await _servicio.AgregarConConsumoAsync(lugarId, 500m, 700m, new DateOnly(2026, 8, 10), Hoy, mesesPorPeriodo: 1);
        // La misma fecha y una a 10 días se rechazan como en la 9i.
        var repetido = await _servicio.AgregarConConsumoAsync(lugarId, 500m, 700m, new DateOnly(2026, 8, 10), Hoy);
        var cerca = await _servicio.AgregarConConsumoAsync(lugarId, 500m, 700m, new DateOnly(2026, 7, 31), Hoy);

        Assert.Equal((new DateOnly(2026, 7, 11), new DateOnly(2026, 8, 10)), (primero.Inicio, primero.Fin));
        Assert.Equal(ErrorReciboPorMonto.CorteRepetido, repetido.Error);
        Assert.Equal(ErrorReciboPorMonto.CorteMuyCerca, cerca.Error);
        Assert.Single(await PeriodosAsync(lugarId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100_001)]
    public async Task ConsumoFueraDeRango_SeRechaza(int kwh)
    {
        var lugarId = await LugarAsync(CuatroTramos());

        var r = await _servicio.PrevisualizarConConsumoAsync(lugarId, kwh, 1_751.60m, new DateOnly(2026, 8, 10), Hoy);

        Assert.Equal(ErrorReciboPorMonto.ConsumoFueraDeRango, r.Error);
    }

    [Fact]
    public async Task ConConsumo_EnElHistorialYEnInicio_NoEsAproximado()
    {
        var lugarId = await LugarAsync(CuatroTramos());
        await _servicio.AgregarConConsumoAsync(lugarId, 1_000m, 1_751.60m, new DateOnly(2026, 8, 10), Hoy);

        var resumen = await new ResumenDashboardService(
                _fabrica, new ProyeccionService(_calculadora), new DacService(), new PresupuestoInversoService(_calculadora), _calculadora)
            .ObtenerDeLugarAsync(lugarId, Hoy);
        await using var db = _fabrica.CreateDbContext();
        var lugar = await db.Lugares.Include(l => l.Periodos).Include(l => l.Lecturas).SingleAsync(l => l.Id == lugarId);

        Assert.False(resumen!.UltimoReciboEstimado);
        Assert.False(ConsumoMedido.EsAproximado(lugar, Hoy));
        // "Si sigues igual": la tarifa del lugar con los 1,000 kWh del recibo = $1,510.00.
        Assert.Equal(1_510.00m, resumen.MontoReciboAnterior);
    }

    // ---- Validación contra la tarifa guardada (D8) ----

    [Fact]
    public void Validacion_ConLaTarifaDeLosMismosPrecios_Coincide()
    {
        // 1,000 kWh en un bimestre con la tarifa de cuatro tramos = $1,510.00; el recibo dice $1,510.00.
        var c = new ValidadorRecibo(_calculadora).Comparar(CuatroTramos(), 2, 1_000m, 1_510.00m);

        Assert.Equal((VeredictoRecibo.Coincide, 1_510.00m), (c.Veredicto, c.SubtotalEnergia));
    }

    [Fact]
    public void Validacion_ConOtraTarifa_NoCoincide()
    {
        // Tarifa de tres tramos (0–100 a $1, 100–200 a $2, 200+ a $4, mensual) en bimestre: 200 × 1 + 200 × 2 + 600 × 4
        // = $3,000 contra $1,510 del recibo: muy lejos.
        var c = new ValidadorRecibo(_calculadora).Comparar(TarifasDePrueba.TresTramos(), 2, 1_000m, 1_510.00m);

        Assert.Equal((VeredictoRecibo.NoCoincide, 3_000m), (c.Veredicto, c.SubtotalEnergia));
    }
}
