using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9i: los recibos consecutivos dividen el tiempo en sus fechas de corte, aunque no estén a meses exactos.
/// Tarifa 1C; "hoy" es el 6 de noviembre de 2026.
/// </summary>
public sealed class CortesDeRecibosTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 11, 6);

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

    private static DateOnly F(int mes, int dia) => new(2026, mes, dia);

    private async Task<int> LugarAsync(int meses = 2, IEnumerable<Periodo>? periodos = null, IEnumerable<Lectura>? lecturas = null)
    {
        await using var db = _fabrica.CreateDbContext();
        var lugar = new Lugar
        {
            Nombre = "Mi casa",
            DiaCorte = 26,
            MesesPorPeriodo = meses,
            TarifaId = await db.Tarifas.Where(t => t.Nombre == "1C").Select(t => t.Id).SingleAsync(),
            Periodos = [.. periodos ?? []],
            Lecturas = [.. lecturas ?? []],
        };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private async Task<List<Periodo>> PeriodosAsync(int lugarId)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Periodos.Where(p => p.LugarId == lugarId).OrderBy(p => p.Fin).ToListAsync();
    }

    private Task<ResultadoReciboPorMonto> AgregarAsync(int lugarId, DateOnly corte, decimal total = 1_000m)
        => _servicio.AgregarPorMontoAsync(lugarId, total, corte, 16m, Hoy);

    [Fact]
    public async Task CincuentaYNueveDiasAntes_SeAcepta_YElSiguienteEmpiezaDespues()
    {
        // Existente: corte 26 oct, bimestral → 27 ago – 26 oct. Nuevo: corte 28 ago (59 días antes); no hay uno más
        // antiguo → 28 ago − 2 meses + 1 = 29 jun – 28 ago. El existente (por monto) pasa a empezar el 29 ago.
        var lugarId = await LugarAsync();
        await AgregarAsync(lugarId, F(10, 26));
        var antes = Assert.Single(await PeriodosAsync(lugarId));

        var r = await AgregarAsync(lugarId, F(8, 28), 900m);

        Assert.Null(r.Error);
        Assert.Equal((F(6, 29), F(8, 28)), (r.Inicio, r.Fin));
        var periodos = await PeriodosAsync(lugarId);
        Assert.Equal([(F(6, 29), F(8, 28)), (F(8, 29), F(10, 26))], periodos.Select(p => (p.Inicio, p.Fin)));
        // D4: el recibo existente conserva sus kWh, su monto y su total.
        var existente = periodos[1];
        Assert.Equal((antes.KwhTotales, antes.Monto, antes.MontoRecibo), (existente.KwhTotales, existente.Monto, existente.MontoRecibo));
    }

    [Fact]
    public async Task MismaFecha_SeRechaza_ConLaFecha()
    {
        var lugarId = await LugarAsync();
        await AgregarAsync(lugarId, F(10, 26));

        var r = await AgregarAsync(lugarId, F(10, 26));

        Assert.Equal(ErrorReciboPorMonto.CorteRepetido, r.Error);
        Assert.Equal((F(8, 27), F(10, 26)), (r.ChoqueInicio, r.ChoqueFin));
        Assert.Single(await PeriodosAsync(lugarId));
    }

    [Theory]
    // 16 y 19 días antes del corte del 26 oct: muy cerca. 20 días exactos (6 oct): se acepta.
    [InlineData(10, ErrorReciboPorMonto.CorteMuyCerca)]
    [InlineData(7, ErrorReciboPorMonto.CorteMuyCerca)]
    [InlineData(6, null)]
    public async Task MenosDeVeinteDias_MuyCerca(int dia, ErrorReciboPorMonto? esperado)
    {
        Assert.Equal(20, CortesDeRecibos.DiasMinimosEntreCortes);
        var lugarId = await LugarAsync();
        await AgregarAsync(lugarId, F(10, 26));

        var r = await AgregarAsync(lugarId, F(10, dia));

        Assert.Equal(esperado, r.Error);
        if (esperado is not null)
        {
            Assert.Equal(F(10, 26), r.ChoqueFin);
        }
    }

    [Fact]
    public async Task EntreDosRecibos_EmpiezaDespuesDelAnterior_YMueveAlSiguiente()
    {
        // Existentes: 26 jun (27 abr – 26 jun) y 26 oct (27 ago – 26 oct). Nuevo 28 ago: empieza al día siguiente del
        // anterior (27 jun) y el del 26 oct pasa a empezar el 29 ago.
        var lugarId = await LugarAsync();
        await AgregarAsync(lugarId, F(6, 26));
        await AgregarAsync(lugarId, F(10, 26));

        var r = await AgregarAsync(lugarId, F(8, 28));

        Assert.Null(r.Error);
        Assert.Equal(
            [(F(4, 27), F(6, 26)), (F(6, 27), F(8, 28)), (F(8, 29), F(10, 26))],
            (await PeriodosAsync(lugarId)).Select(p => (p.Inicio, p.Fin)));
    }

    [Fact]
    public async Task Mensual()
    {
        // Mensual: 26 oct → 27 sep – 26 oct. Nuevo 28 sep (28 días antes): 29 ago – 28 sep; el existente, desde el 29 sep.
        var lugarId = await LugarAsync(meses: 1);
        await AgregarAsync(lugarId, F(10, 26));

        var r = await AgregarAsync(lugarId, F(9, 28));

        Assert.Null(r.Error);
        Assert.Equal([(F(8, 29), F(9, 28)), (F(9, 29), F(10, 26))], (await PeriodosAsync(lugarId)).Select(p => (p.Inicio, p.Fin)));
    }

    [Fact]
    public async Task ReciboConLecturas_NoSeModifica_YUnaFechaDentroSeRechaza()
    {
        // Recibo cerrado con lecturas (lectura de corte el 26 oct): 27 ago – 26 oct.
        var periodo = new Periodo { Inicio = F(8, 27), Fin = F(10, 26), KwhTotales = 600m, Monto = 675m };
        var lugarId = await LugarAsync(
            periodos: [periodo],
            lecturas: [new Lectura { Fecha = F(8, 26), ValorKwh = 1_000m }, new Lectura { Fecha = F(10, 26), ValorKwh = 1_600m }]);

        var dentro = await AgregarAsync(lugarId, F(9, 15));
        var antes = await AgregarAsync(lugarId, F(8, 1));

        Assert.Equal(ErrorReciboPorMonto.DentroDeLecturas, dentro.Error);
        Assert.Equal((F(8, 27), F(10, 26)), (dentro.ChoqueInicio, dentro.ChoqueFin));
        Assert.Null(antes.Error);
        var periodos = await PeriodosAsync(lugarId);
        // 1 ago − 2 meses + 1 = 2 jun – 1 ago; el de lecturas no se toca.
        Assert.Equal([(F(6, 2), F(8, 1)), (F(8, 27), F(10, 26))], periodos.Select(p => (p.Inicio, p.Fin)));
        Assert.Equal((600m, 675m), (periodos[1].KwhTotales, periodos[1].Monto));
    }

    [Fact]
    public async Task LosCuatroRecibosDeLaPresentacion_EnDistintoOrden_DanLosMismosPeriodos()
    {
        // Cortes el 26 de abril, junio, agosto y octubre de 2026, bimestral. Siempre: 27 feb – 26 abr, 27 abr – 26 jun,
        // 27 jun – 26 ago y 27 ago – 26 oct, con los mismos kWh.
        int[][] ordenes = [[4, 6, 8, 10], [10, 8, 6, 4], [6, 10, 4, 8]];
        var resultados = new List<List<(DateOnly, DateOnly, decimal)>>();
        foreach (var orden in ordenes)
        {
            var lugarId = await LugarAsync();
            foreach (var mes in orden)
            {
                Assert.Null((await AgregarAsync(lugarId, F(mes, 26))).Error);
            }

            resultados.Add((await PeriodosAsync(lugarId)).Select(p => (p.Inicio, p.Fin, p.KwhTotales)).ToList());
        }

        Assert.Equal(
            [(F(2, 27), F(4, 26)), (F(4, 27), F(6, 26)), (F(6, 27), F(8, 26)), (F(8, 27), F(10, 26))],
            resultados[0].Select(p => (p.Item1, p.Item2)));
        Assert.Equal(resultados[0], resultados[1]);
        Assert.Equal(resultados[0], resultados[2]);
    }

    [Fact]
    public async Task UnReciboMasAntiguo_NoCambiaSiSiguesIgualNiLaComparacion_YEntraAlDac()
    {
        var lugarId = await LugarAsync();
        await AgregarAsync(lugarId, F(10, 26));
        var resumenes = new ResumenDashboardService(
            _fabrica, new ProyeccionService(_calculadora), new DacService(), new PresupuestoInversoService(_calculadora), _calculadora);
        var antes = await resumenes.ObtenerDeLugarAsync(lugarId, Hoy);

        await AgregarAsync(lugarId, F(8, 28), 700m);
        var despues = await resumenes.ObtenerDeLugarAsync(lugarId, Hoy);

        Assert.Equal(antes!.MontoReciboAnterior, despues!.MontoReciboAnterior);
        Assert.Equal(antes.FechaCorteUltimoRecibo, despues.FechaCorteUltimoRecibo);
        Assert.Equal(F(10, 26), despues.FechaCorteUltimoRecibo);
        // El DAC reparte cada recibo en sus meses: 2 meses más con el recibo nuevo.
        await using var db = _fabrica.CreateDbContext();
        var periodos = await db.Periodos.Where(p => p.LugarId == lugarId).ToListAsync();
        Assert.Equal(4, HistorialMensualDac.Desde(periodos, 2).Count);
    }
}
