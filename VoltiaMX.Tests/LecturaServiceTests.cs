using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class LecturaServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 20);

    private readonly TarifaCalculator _calculadora = new();
    private FabricaDePrueba _fabrica = null!;
    private LecturaService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new LecturaService(_fabrica, _calculadora);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private static DateOnly F(int anio, int mes, int dia) => new(anio, mes, dia);

    private static async Task<int> CrearLugarAsync(
        FabricaDePrueba fabrica, int diaCorte = 1, int meses = 1, Periodo? cerrado = null)
    {
        await using var db = fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.SingleAsync(t => t.Nombre == "1C");
        var lugar = new Lugar { Nombre = "Casa", TarifaId = tarifa.Id, DiaCorte = diaCorte, MesesPorPeriodo = meses };
        if (cerrado is not null)
        {
            lugar.Periodos.Add(cerrado);
        }

        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private Task<int> CrearLugarAsync(int diaCorte = 1, int meses = 1, Periodo? cerrado = null)
        => CrearLugarAsync(_fabrica, diaCorte, meses, cerrado);

    private Task<ResultadoLectura> GuardarAsync(int lugarId, DateOnly fecha, decimal valor, decimal? montoRecibo = null)
        => _servicio.GuardarAsync(new NuevaLectura(lugarId, fecha, valor, MontoRecibo: montoRecibo), Hoy);

    private static async Task<List<Lectura>> LecturasAsync(FabricaDePrueba fabrica, int lugarId)
    {
        await using var db = fabrica.CreateDbContext();
        var lecturas = await db.Lecturas.Where(l => l.LugarId == lugarId).ToListAsync();
        return lecturas.OrderBy(l => l.Fecha).ToList();
    }

    private Task<List<Lectura>> LecturasAsync(int lugarId) => LecturasAsync(_fabrica, lugarId);

    private static async Task<List<Periodo>> PeriodosAsync(FabricaDePrueba fabrica, int lugarId)
    {
        await using var db = fabrica.CreateDbContext();
        var periodos = await db.Periodos.Where(p => p.LugarId == lugarId).ToListAsync();
        return periodos.OrderBy(p => p.Inicio).ToList();
    }

    private Task<List<Periodo>> PeriodosAsync(int lugarId) => PeriodosAsync(_fabrica, lugarId);

    private async Task<Tarifa> Tarifa1CAsync()
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Tarifas.Include(t => t.Rangos).SingleAsync(t => t.Nombre == "1C");
    }

    // ---- Rechazos ----

    [Fact]
    public async Task Guardar_LugarInexistente_SeRechaza()
    {
        var resultado = await GuardarAsync(999, F(2026, 4, 1), 1000m);

        Assert.Equal(MotivoRechazoLectura.LugarNoExiste, resultado.Rechazo);
    }

    [Fact]
    public async Task Guardar_ValorNegativo_SeRechaza()
    {
        var lugar = await CrearLugarAsync();

        var resultado = await GuardarAsync(lugar, F(2026, 4, 1), -1m);

        Assert.Equal(MotivoRechazoLectura.ValorNegativo, resultado.Rechazo);
        Assert.Empty(await LecturasAsync(lugar));
    }

    [Fact]
    public async Task Guardar_FechaFutura_SeRechaza()
    {
        var lugar = await CrearLugarAsync();

        var resultado = await GuardarAsync(lugar, Hoy.AddDays(1), 1000m);

        Assert.False(resultado.Aceptada);
        Assert.Equal(MotivoRechazoLectura.FechaFutura, resultado.Rechazo);
        Assert.Empty(await LecturasAsync(lugar));
    }

    [Fact]
    public async Task Guardar_MenorQueLaAnteriorPorFecha_SeRechaza()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);

        var resultado = await GuardarAsync(lugar, F(2026, 4, 10), 999m);

        Assert.Equal(MotivoRechazoLectura.MenorQueAnterior, resultado.Rechazo);
        Assert.Single(await LecturasAsync(lugar));
    }

    [Fact]
    public async Task Guardar_MayorQueLaSiguientePorFecha_SeRechaza()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 21), 1200m);

        var resultado = await GuardarAsync(lugar, F(2026, 4, 11), 1201m);

        Assert.Equal(MotivoRechazoLectura.MayorQueSiguiente, resultado.Rechazo);
        Assert.Equal(2, (await LecturasAsync(lugar)).Count);
    }

    [Fact]
    public async Task Guardar_EntreDosLecturasConValorIntermedio_SeAcepta()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 21), 1200m);

        var resultado = await GuardarAsync(lugar, F(2026, 4, 11), 1100m);

        Assert.True(resultado.Aceptada);
        Assert.Equal([1000m, 1100m, 1200m], (await LecturasAsync(lugar)).Select(l => l.ValorKwh));
    }

    [Fact]
    public async Task Guardar_FechaDentroDeUnPeriodoCerrado_SeRechaza()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 5, 1), 1300m); // cierra 2 abr - 1 may

        var dentro = await GuardarAsync(lugar, F(2026, 4, 15), 1100m);
        var enElCorte = await GuardarAsync(lugar, F(2026, 5, 1), 1310m);

        Assert.Equal(MotivoRechazoLectura.PeriodoCerrado, dentro.Rechazo);
        Assert.Equal(MotivoRechazoLectura.PeriodoCerrado, enElCorte.Rechazo);
        Assert.Equal([1000m, 1300m], (await LecturasAsync(lugar)).Select(l => l.ValorKwh));
    }

    // ---- Registro ----

    [Fact]
    public async Task Guardar_PrimeraLectura_SoloFijaElPuntoDePartida()
    {
        var lugar = await CrearLugarAsync();

        var resultado = await GuardarAsync(lugar, F(2026, 4, 1), 1000m);

        Assert.True(resultado.Aceptada);
        Assert.True(resultado.EsPuntoDePartida);
        Assert.Empty(resultado.Cierres);
        var lectura = Assert.Single(await LecturasAsync(lugar));
        Assert.Equal(1000m, lectura.ValorKwh);
        Assert.False(lectura.EsEstimada);
        Assert.Empty(await PeriodosAsync(lugar));
    }

    [Fact]
    public async Task Guardar_MismaFecha_ReemplazaLaLectura()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 11), 1100m);

        var resultado = await GuardarAsync(lugar, F(2026, 4, 11), 1150m);

        Assert.True(resultado.Reemplaza);
        Assert.False(resultado.EsPuntoDePartida);
        Assert.Equal([1000m, 1150m], (await LecturasAsync(lugar)).Select(l => l.ValorKwh));
    }

    // ---- Cierre de periodos ----

    [Fact]
    public async Task Guardar_LecturaExactaEnElCorte_CierraSinEstimarConElMontoDeLaTarifa()
    {
        var lugar = await CrearLugarAsync(); // corte el día 1, mensual
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m); // ancla: periodo 2 abr - 1 may

        var resultado = await GuardarAsync(lugar, F(2026, 5, 1), 1300m);

        var montoEsperado = _calculadora.Calcular(300m, await Tarifa1CAsync(), mesesDelPeriodo: 1).Total;
        var cierre = Assert.Single(resultado.Cierres);
        Assert.Equal(new CierrePeriodo(F(2026, 4, 2), F(2026, 5, 1), 300m, montoEsperado, Estimado: false), cierre);

        var periodo = Assert.Single(await PeriodosAsync(lugar));
        Assert.Equal((F(2026, 4, 2), F(2026, 5, 1), 300m, montoEsperado, false),
            (periodo.Inicio, periodo.Fin, periodo.KwhTotales, periodo.Monto, periodo.Estimado));
        Assert.Null(periodo.MontoRecibo);
        Assert.DoesNotContain(await LecturasAsync(lugar), l => l.EsEstimada);
    }

    [Fact]
    public async Task Guardar_LecturaDespuesDelCorte_InterpolaLaLecturaDeCorte()
    {
        var lugar = await CrearLugarAsync(); // corte el día 1, mensual: periodo 2 abr - 1 may
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 21), 1200m);

        var resultado = await GuardarAsync(lugar, F(2026, 5, 11), 1400m);

        // A mano: entre 1200 (21 abr) y 1400 (11 may) hay 20 días y 200 kWh, 10 kWh por día.
        // El corte (1 may) está 10 días después del 21 abr: 1200 + 10 × 10 = 1300.
        // Consumo del periodo: 1300 − 1000 (base del 1 abr) = 300 kWh.
        var cierre = Assert.Single(resultado.Cierres);
        Assert.Equal(300m, cierre.Kwh);
        Assert.True(cierre.Estimado);

        var corte = (await LecturasAsync(lugar)).Single(l => l.Fecha == F(2026, 5, 1));
        Assert.Equal(1300m, corte.ValorKwh);
        Assert.True(corte.EsEstimada);
        Assert.True(Assert.Single(await PeriodosAsync(lugar)).Estimado);
    }

    [Fact]
    public async Task Guardar_CorteInterpolado_SirveDeBaseAlSiguientePeriodo()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 21), 1200m);
        await GuardarAsync(lugar, F(2026, 5, 11), 1400m); // corte estimado del 1 may: 1300

        var resultado = await GuardarAsync(lugar, F(2026, 6, 1), 1650m);

        // Periodo 2 may - 1 jun: 1650 − 1300 = 350 kWh, con corte exacto.
        var cierre = Assert.Single(resultado.Cierres);
        Assert.Equal(new CierrePeriodo(F(2026, 5, 2), F(2026, 6, 1), 350m, cierre.Monto, Estimado: false), cierre);
        Assert.Equal(2, (await PeriodosAsync(lugar)).Count);
    }

    [Fact]
    public async Task Guardar_SaltoDeTresPeriodos_CierraLosTres()
    {
        var lugar = await CrearLugarAsync(); // corte el día 1, mensual
        await GuardarAsync(lugar, F(2026, 1, 1), 1000m);

        // 105 días después marca 2050: 10 kWh por día.
        var resultado = await GuardarAsync(lugar, F(2026, 4, 16), 2050m);

        // Cortes interpolados: 1 feb (31 días) 1310, 1 mar (59) 1590, 1 abr (90) 1900.
        Assert.Equal(
            [
                new CierrePeriodo(F(2026, 1, 2), F(2026, 2, 1), 310m, resultado.Cierres[0].Monto, true),
                new CierrePeriodo(F(2026, 2, 2), F(2026, 3, 1), 280m, resultado.Cierres[1].Monto, true),
                new CierrePeriodo(F(2026, 3, 2), F(2026, 4, 1), 310m, resultado.Cierres[2].Monto, true),
            ],
            resultado.Cierres);
        Assert.Equal(3, (await PeriodosAsync(lugar)).Count);
        Assert.Equal(
            [(F(2026, 2, 1), 1310m), (F(2026, 3, 1), 1590m), (F(2026, 4, 1), 1900m)],
            (await LecturasAsync(lugar)).Where(l => l.EsEstimada).Select(l => (l.Fecha, l.ValorKwh)));
    }

    [Fact]
    public async Task Guardar_DosVecesLaMismaLectura_NoDuplicaPeriodosNiCortes()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 1, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 16), 2050m);

        var otraVez = await GuardarAsync(lugar, F(2026, 4, 16), 2050m);

        Assert.True(otraVez.Reemplaza);
        Assert.Empty(otraVez.Cierres);
        Assert.Equal(3, (await PeriodosAsync(lugar)).Count);
        Assert.Equal(5, (await LecturasAsync(lugar)).Count); // inicial, 3 cortes y la del 16 abr
    }

    [Fact]
    public async Task Guardar_PrimeraLecturaAMitadDelPeriodo_ElCierreQuedaEstimado()
    {
        var lugar = await CrearLugarAsync(); // ancla 1 abr: periodo 2 abr - 1 may
        await GuardarAsync(lugar, F(2026, 4, 11), 1000m);

        var resultado = await GuardarAsync(lugar, F(2026, 5, 1), 1200m);

        // Se midió del 11 abr al corte: el periodo está incompleto.
        var cierre = Assert.Single(resultado.Cierres);
        Assert.Equal(200m, cierre.Kwh);
        Assert.True(cierre.Estimado);
    }

    [Fact]
    public async Task Guardar_SinLecturaAntesDelCorte_NoCierraYLoIndica()
    {
        // Periodo del recibo (sin lecturas); el siguiente va del 2 mar al 1 may.
        var lugar = await CrearLugarAsync(diaCorte: 1, meses: 2, cerrado: new Periodo
        {
            Inicio = F(2026, 1, 2), Fin = F(2026, 3, 1), KwhTotales = 500m, Monto = 400m,
        });

        var resultado = await GuardarAsync(lugar, F(2026, 5, 20), 5000m);

        Assert.True(resultado.Aceptada);
        Assert.True(resultado.EsPuntoDePartida);
        Assert.True(resultado.SinLecturaBase);
        Assert.Empty(resultado.Cierres);
        Assert.Single(await PeriodosAsync(lugar));
        Assert.Single(await LecturasAsync(lugar));
    }

    [Fact]
    public async Task Guardar_ConMontoDelRecibo_LoGuardaEnElPeriodoQueCierra()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);

        await GuardarAsync(lugar, F(2026, 5, 1), 1300m, montoRecibo: 512.50m);

        var periodo = Assert.Single(await PeriodosAsync(lugar));
        Assert.Equal(512.50m, periodo.MontoRecibo);
        Assert.NotEqual(periodo.MontoRecibo, periodo.Monto);
    }

    [Fact]
    public async Task Guardar_SiFallaAlGuardarElPeriodo_NoQuedaNada()
    {
        using var fabrica = await FabricaDePrueba.CrearAsync(new FallaAlInsertarPeriodos());
        var servicio = new LecturaService(fabrica, _calculadora);
        var lugar = await CrearLugarAsync(fabrica);
        await servicio.GuardarAsync(new NuevaLectura(lugar, F(2026, 4, 1), 1000m), Hoy);
        await servicio.GuardarAsync(new NuevaLectura(lugar, F(2026, 4, 21), 1200m), Hoy);

        // Cerraría el periodo con un corte interpolado: lectura nueva, corte y periodo.
        await Assert.ThrowsAnyAsync<Exception>(
            () => servicio.GuardarAsync(new NuevaLectura(lugar, F(2026, 5, 11), 1400m), Hoy));

        Assert.Equal([1000m, 1200m], (await LecturasAsync(fabrica, lugar)).Select(l => l.ValorKwh));
        Assert.Empty(await PeriodosAsync(fabrica, lugar));
    }

    [Fact]
    public async Task Previsualizar_DiceQueCerraria_SinGuardarNada()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 21), 1200m);

        var resultado = await _servicio.PrevisualizarAsync(new NuevaLectura(lugar, F(2026, 5, 11), 1400m), Hoy);

        var cierre = Assert.Single(resultado.Cierres);
        Assert.Equal(300m, cierre.Kwh);
        Assert.True(cierre.Estimado);
        Assert.Equal(2, (await LecturasAsync(lugar)).Count);
        Assert.Empty(await PeriodosAsync(lugar));
    }

    [Fact]
    public async Task Previsualizar_ConMotivoDeRechazo_LoDevuelve()
    {
        var lugar = await CrearLugarAsync();

        var resultado = await _servicio.PrevisualizarAsync(new NuevaLectura(lugar, Hoy.AddDays(1), 1m), Hoy);

        Assert.Equal(MotivoRechazoLectura.FechaFutura, resultado.Rechazo);
    }

    // ---- Eliminar la última ----

    [Fact]
    public async Task EliminarUltima_LecturaDelPeriodoActual_LaBorra()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 4, 11), 1100m);

        var resultado = await _servicio.EliminarUltimaAsync(lugar);

        Assert.Equal(ResultadoEliminacion.Eliminada, resultado);
        Assert.Equal(F(2026, 4, 1), Assert.Single(await LecturasAsync(lugar)).Fecha);
    }

    [Fact]
    public async Task EliminarUltima_CorteDeUnPeriodoCerrado_NoLaBorra()
    {
        var lugar = await CrearLugarAsync();
        await GuardarAsync(lugar, F(2026, 4, 1), 1000m);
        await GuardarAsync(lugar, F(2026, 5, 1), 1300m);

        var resultado = await _servicio.EliminarUltimaAsync(lugar);

        Assert.Equal(ResultadoEliminacion.EsCorteDePeriodoCerrado, resultado);
        Assert.Equal(2, (await LecturasAsync(lugar)).Count);
    }

    [Fact]
    public async Task EliminarUltima_SinLecturas_LoIndica()
    {
        var lugar = await CrearLugarAsync();

        Assert.Equal(ResultadoEliminacion.SinLecturas, await _servicio.EliminarUltimaAsync(lugar));
    }

    /// <summary>
    /// Falla después de insertar el periodo, para comprobar que la transacción deshace todo.
    /// </summary>
    private sealed class FallaAlInsertarPeriodos : DbCommandInterceptor
    {
        private static void Revisar(DbCommand comando)
        {
            if (comando.CommandText.Contains("INSERT INTO \"Periodos\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Falla simulada al guardar el periodo.");
            }
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Revisar(command);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Revisar(command);
            return ValueTask.FromResult(result);
        }

        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            Revisar(command);
            return result;
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Revisar(command);
            return ValueTask.FromResult(result);
        }
    }
}
