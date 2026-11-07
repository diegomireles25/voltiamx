using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Recibo anterior por su monto (D9). Tarifa 1C bimestral: 0–300 a $1, 300–900 a $1.25 y 900+ a $3.50.
/// $1,000 con 16 % → $862.07 sin impuestos: 300 + 449 × 1.25 = $861.25 → 749 kWh (750 ya cuesta $862.50).
/// </summary>
public sealed class RecibosServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

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

    private async Task<int> LugarAsync(string tarifa = "1C", int meses = 2, params Lectura[] lecturas)
    {
        await using var db = _fabrica.CreateDbContext();
        var lugar = new Lugar
        {
            Nombre = "Mi casa",
            DiaCorte = 26,
            MesesPorPeriodo = meses,
            TarifaId = await db.Tarifas.Where(t => t.Nombre == tarifa).Select(t => t.Id).SingleAsync(),
            Lecturas = [.. lecturas],
        };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private async Task<List<Periodo>> PeriodosAsync(int lugarId)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Periodos.Where(p => p.LugarId == lugarId).OrderBy(p => p.Inicio).ToListAsync();
    }

    [Fact]
    public async Task Agregar_CreaUnPeriodoAproximadoConLosKwhDelMonto()
    {
        var lugarId = await LugarAsync();

        var r = await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy);

        // Sin fecha: el último corte según el día de corte (26): el 26 de septiembre.
        Assert.Null(r.Error);
        Assert.Equal((749m, 861.25m), (r.Kwh, r.Monto));
        var periodo = Assert.Single(await PeriodosAsync(lugarId));
        Assert.Equal(new DateOnly(2026, 7, 27), periodo.Inicio);
        Assert.Equal(new DateOnly(2026, 9, 26), periodo.Fin);
        Assert.True(periodo.Estimado);
        Assert.Equal(749m, periodo.KwhTotales);
        Assert.Equal(861.25m, periodo.Monto);
        Assert.Equal(1_000m, periodo.MontoRecibo);
    }

    [Fact]
    public async Task Previsualizar_CalculaSinGuardar()
    {
        var lugarId = await LugarAsync();

        var r = await _servicio.PrevisualizarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy);

        Assert.Equal(749m, r.Kwh);
        Assert.Equal(new DateOnly(2026, 9, 26), r.Fin);
        Assert.Empty(await PeriodosAsync(lugarId));
    }

    [Fact]
    public async Task OtroCorte_SeAcomodaPorFechas_YLaMismaFechaSeRechaza()
    {
        // Fase 9i (antes "Traslape_ConUnPeriodoExistente_SeRechaza"): ya no se rechaza un corte por cruzarse con los
        // meses calculados de otro recibo. Existente 26 sep (27 jul – 26 sep). Un corte el 26 ago (31 días antes) se
        // acepta: 27 jun – 26 ago, y el existente pasa a empezar el 27 ago. La misma fecha (26 sep) sí se rechaza.
        var lugarId = await LugarAsync();
        await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, new DateOnly(2026, 9, 26), 16m, Hoy);

        var otro = await _servicio.AgregarPorMontoAsync(lugarId, 900m, new DateOnly(2026, 8, 26), 16m, Hoy);
        var repetido = await _servicio.AgregarPorMontoAsync(lugarId, 900m, new DateOnly(2026, 9, 26), 16m, Hoy);

        Assert.Null(otro.Error);
        Assert.Equal(ErrorReciboPorMonto.CorteRepetido, repetido.Error);
        var periodos = await PeriodosAsync(lugarId);
        Assert.Equal(
            [(new DateOnly(2026, 6, 27), new DateOnly(2026, 8, 26)), (new DateOnly(2026, 8, 27), new DateOnly(2026, 9, 26))],
            periodos.Select(p => (p.Inicio, p.Fin)));
    }

    [Fact]
    public async Task FechaFutura_SeRechaza()
    {
        var lugarId = await LugarAsync();

        var r = await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, Hoy.AddDays(1), 16m, Hoy);

        Assert.Equal(ErrorReciboPorMonto.FechaFutura, r.Error);
        Assert.Empty(await PeriodosAsync(lugarId));
    }

    [Fact]
    public async Task MontoMenorAlCargoFijo_EnUnaTarifaComercial_SeRechaza()
    {
        // PDBT: $60 de cargo fijo al mes. $50 no alcanza ni para el cargo fijo.
        var lugarId = await LugarAsync("PDBT", meses: 1);

        var r = await _servicio.AgregarPorMontoAsync(lugarId, 50m, null, 16m, Hoy);

        Assert.Equal(ErrorReciboPorMonto.MontoNoAlcanza, r.Error);
        Assert.Empty(await PeriodosAsync(lugarId));
    }

    [Fact]
    public async Task LugarQueNoExiste_SeRechaza()
    {
        Assert.Equal(ErrorReciboPorMonto.LugarNoExiste, (await _servicio.AgregarPorMontoAsync(999, 1_000m, null, 16m, Hoy)).Error);
    }

    [Fact]
    public async Task ElPeriodoPorMonto_AlimentaLaComparacionElHistorialYElDac()
    {
        // Lecturas del recibo actual: 1,000 el día del corte y 1,400 hoy, para que haya "a este ritmo".
        var lugarId = await LugarAsync(lecturas:
        [
            new Lectura { Fecha = new DateOnly(2026, 9, 26), ValorKwh = 1_000m },
            new Lectura { Fecha = Hoy, ValorKwh = 1_400m },
        ]);
        await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy);

        var proyeccion = new ProyeccionService(_calculadora);
        var presupuesto = new PresupuestoInversoService(_calculadora);
        var resumen = await new ResumenDashboardService(_fabrica, proyeccion, new DacService(), presupuesto, _calculadora)
            .ObtenerDeLugarAsync(lugarId, Hoy);
        var historial = await new HistorialService(_fabrica, proyeccion, presupuesto).ObtenerAsync(lugarId, Hoy);

        // Comparación (D6): el recibo anterior es el del monto, con la tarifa de hoy.
        Assert.Equal(861.25m, resumen!.MontoReciboAnterior);
        var comparacion = ComparacionRecibo.Evaluar(resumen.MontoProyectado, resumen.MontoReciboAnterior, resumen.DiaDelPeriodo);
        Assert.NotEqual(EstadoComparacion.SinReciboAnterior, comparacion.Estado);
        Assert.Equal(861.25m, comparacion.MontoAnterior);

        // Historial: el recibo queda marcado como aproximado; el DAC lo cuenta (749 / 2 meses = 374.5 al mes).
        var cerrado = Assert.Single(historial!.Periodos, p => !p.EsActual);
        Assert.True(cerrado.Estimado);
        Assert.Equal(749m, cerrado.Kwh);
        Assert.Equal(374.5m, resumen.Dac!.PromedioMensualKwh);
    }

    [Fact]
    public async Task EstimarSinLugar_UsaLaTarifaElegidaOLaDePorDefecto()
    {
        int id1C;
        await using (var db = _fabrica.CreateDbContext())
        {
            id1C = await db.Tarifas.Where(t => t.Nombre == "1C").Select(t => t.Id).SingleAsync();
        }

        var con1C = await _servicio.EstimarSinLugarAsync(id1C, 2, 1_000m, 16m);
        // Sin tarifa elegida se usa la 1, la misma que el onboarding asigna por defecto: tiene otros tramos y da otros kWh.
        var porDefecto = await _servicio.EstimarSinLugarAsync(null, 2, 1_000m, 16m);

        Assert.Equal(749m, con1C.Kwh);
        Assert.Null(porDefecto.Error);
        Assert.NotEqual(con1C.Kwh, porDefecto.Kwh);
    }

    // ---- Fase 9c (D3): "¿Cada cuánto te llega el recibo?" en el primer recibo ----
    // 1C mensual: 0–150 a $1, 150–450 a $1.25 y 450+ a $3.50 (bimestral, los tramos al doble).
    // $1,000 con 16 % → $862.07 sin impuestos.
    // Cada mes: 150 + 300 × 1.25 = $525 hasta 450 kWh; quedan $337.07 → 96 kWh × 3.50 = $336 → 546 kWh y
    //   $861.00 (547 ya cuesta $864.50).
    // Cada 2 meses: 300 + 449 × 1.25 = $861.25 → 749 kWh (750 ya cuesta $862.50).

    private async Task<int> MesesDelLugarAsync(int lugarId)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Lugares.Where(l => l.Id == lugarId).Select(l => l.MesesPorPeriodo).SingleAsync();
    }

    [Fact]
    public async Task PrimerRecibo_CadaMes_EstimaConUnMesYGuardaLaPeriodicidad()
    {
        var lugarId = await LugarAsync(meses: 2);

        var r = await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy, mesesPorPeriodo: 1);

        Assert.Null(r.Error);
        Assert.Equal((546m, 861.00m), (r.Kwh, r.Monto));
        // Un mes que termina en el corte del 26 de septiembre.
        Assert.Equal((new DateOnly(2026, 8, 27), new DateOnly(2026, 9, 26)), (r.Inicio, r.Fin));
        Assert.Equal(1, await MesesDelLugarAsync(lugarId));
    }

    [Fact]
    public async Task PrimerRecibo_CadaDosMeses_EstimaConDosMesesYGuardaLaPeriodicidad()
    {
        var lugarId = await LugarAsync(meses: 1);

        var r = await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy, mesesPorPeriodo: 2);

        Assert.Equal((749m, 861.25m), (r.Kwh, r.Monto));
        Assert.Equal(new DateOnly(2026, 7, 27), r.Inicio);
        Assert.Equal(2, await MesesDelLugarAsync(lugarId));
    }

    [Fact]
    public async Task Previsualizar_ConPeriodicidad_NoLaGuarda()
    {
        var lugarId = await LugarAsync(meses: 2);

        var r = await _servicio.PrevisualizarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy, mesesPorPeriodo: 1);

        Assert.Equal(546m, r.Kwh);
        Assert.Equal(2, await MesesDelLugarAsync(lugarId));
        Assert.Empty(await PeriodosAsync(lugarId));
    }

    [Fact]
    public async Task ConRecibosGuardados_LaPeriodicidadSeIgnora()
    {
        var lugarId = await LugarAsync(meses: 2);
        await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy, mesesPorPeriodo: 1);

        // El segundo recibo ya no pregunta: aunque llegue "cada 2 meses", se queda en 1 mes.
        var r = await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, new DateOnly(2026, 8, 26), 16m, Hoy, mesesPorPeriodo: 2);

        Assert.Null(r.Error);
        Assert.Equal(new DateOnly(2026, 7, 27), r.Inicio);
        Assert.Equal(546m, r.Kwh);
        Assert.Equal(1, await MesesDelLugarAsync(lugarId));
    }

    [Theory]
    [InlineData(1, 861.00)]
    [InlineData(2, 861.25)]
    public async Task SiSiguesIgual_SaleDelPrimerReciboConSuPeriodicidad(int meses, double esperado)
    {
        var lugarId = await LugarAsync(meses: 2);
        await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy, mesesPorPeriodo: meses);

        var proyeccion = new ProyeccionService(_calculadora);
        var resumen = await new ResumenDashboardService(_fabrica, proyeccion, new DacService(), new PresupuestoInversoService(_calculadora), _calculadora)
            .ObtenerDeLugarAsync(lugarId, Hoy);

        // "Si sigues igual que tu recibo anterior, pagarías unos $…": el recibo recalculado con la tarifa.
        Assert.Equal(EstadoPantallaInicio.ReciboSinLecturas, resumen!.EstadoInicio);
        Assert.Equal((decimal)esperado, resumen.MontoReciboAnterior);
        Assert.Equal(meses, resumen.MesesDelPeriodo);
    }

    [Fact]
    public async Task TieneRecibos_SoloDespuesDeGuardarUno()
    {
        var lugarId = await LugarAsync();
        Assert.False(await _servicio.TieneRecibosAsync(lugarId));

        await _servicio.AgregarPorMontoAsync(lugarId, 1_000m, null, 16m, Hoy);

        Assert.True(await _servicio.TieneRecibosAsync(lugarId));
    }
}
