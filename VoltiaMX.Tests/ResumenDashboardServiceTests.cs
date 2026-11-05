using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class ResumenDashboardServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private readonly TarifaCalculator _calculadora = new();
    private FabricaDePrueba _fabrica = null!;
    private ResumenDashboardService _servicio = null!;
    private PerfilService _perfiles = null!;
    private DatosDemoService _demo = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new ResumenDashboardService(
            _fabrica,
            new ProyeccionService(_calculadora),
            new DacService(),
            new PresupuestoInversoService(_calculadora), new TarifaCalculator());
        _perfiles = new PerfilService(_fabrica);
        _demo = new DatosDemoService(_fabrica, _calculadora);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Tarifa> TarifaAsync(string nombre)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Tarifas.Include(t => t.Rangos).SingleAsync(t => t.Nombre == nombre);
    }

    private async Task AgregarLugarAsync(Lugar lugar)
    {
        await using var db = _fabrica.CreateDbContext();
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ObtenerAsync_PreciosDeEjemplo_SigueLaMarcaDeLaTarifaDelLugar()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], NombreLugar = "Mi casa" });
        Assert.True((await _servicio.ObtenerAsync(Hoy))!.PreciosDeEjemplo);

        // Con los precios del recibo ya escritos en el editor, deja de ser de ejemplo.
        var tarifas = new TarifaService(_fabrica);
        int lugarId;
        await using (var db = _fabrica.CreateDbContext())
        {
            lugarId = await db.Lugares.Select(l => l.Id).SingleAsync();
        }

        var copia = await tarifas.DuplicarAsync((await TarifaAsync("1C")).Id);
        await tarifas.AsignarALugarAsync(lugarId, copia!.Value);

        Assert.False((await _servicio.ObtenerAsync(Hoy))!.PreciosDeEjemplo);
    }

    [Fact]
    public async Task ObtenerAsync_ReciboAnterior_SeRecalculaConLaTarifaActualYNoUsaElMontoGuardado()
    {
        // 1C bimestral: 0–300 a $1 y 300–900 a $1.25. 600 kWh = 300 + 300 × 1.25 = $675 (el guardado dice $9,999).
        var tarifa = await TarifaAsync("1C");
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 26,
            MesesPorPeriodo = 2,
            Periodos = [new Periodo { Inicio = new DateOnly(2026, 7, 27), Fin = new DateOnly(2026, 9, 26), KwhTotales = 600m, Monto = 9_999m }],
        });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.Equal(675m, resumen!.MontoReciboAnterior);
    }

    [Fact]
    public async Task ObtenerAsync_SinLugares_DevuelveNull()
    {
        Assert.Null(await _servicio.ObtenerAsync(Hoy));
    }

    [Fact]
    public async Task ObtenerAsync_LugarRealSinDatos_DevuelveResumenVacioConLaTarifaPorConfirmar()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], NombreLugar = "Mi casa" });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.NotNull(resumen);
        Assert.False(resumen.TieneDatos);
        Assert.False(resumen.EsDemo);
        Assert.Equal("1", resumen.NombreTarifa);
        Assert.True(resumen.TarifaPorConfirmar);
        Assert.Null(resumen.KwhHastaHoy);
        Assert.Empty(resumen.Historial);
    }

    [Fact]
    public async Task ObtenerAsync_TarifaConfirmada_NoQuedaPorConfirmar()
    {
        var tarifa = await TarifaAsync("1F");
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], TarifaId = tarifa.Id });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.Equal("1F", resumen?.NombreTarifa);
        Assert.False(resumen?.TarifaPorConfirmar);
    }

    [Fact]
    public async Task ObtenerAsync_ConDemo_CalculaElPeriodoActualConLosServiciosDeTarifa()
    {
        await _demo.CargarAsync(Hoy);
        var tarifa = await TarifaAsync("1C");

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.NotNull(resumen);
        Assert.True(resumen.EsDemo);
        Assert.Equal(2, resumen.MesesDelPeriodo);
        Assert.Equal(24, resumen.DiaDelPeriodo);
        var ultimoCorte = Hoy.AddDays(-24);
        Assert.Equal(
            VentanaPeriodo.Calcular(ultimoCorte.Day, 2, ultimoCorte, ultimoCorte),
            new Ventana(resumen.InicioPeriodo, resumen.FinPeriodo));
        Assert.Equal(resumen.FinPeriodo.DayNumber - Hoy.DayNumber, resumen.DiasParaCorte);
        Assert.False(resumen.PeriodoTerminado);

        // Desde la lectura de corte (hace 24 días) hasta la de hoy.
        Assert.Equal(17.2m * 24, resumen.KwhHastaHoy);

        var esperada = new ProyeccionService(_calculadora)
            .Proyectar(17.2m * 24, 24, resumen.DiasDelPeriodo, tarifa, mesesDelPeriodo: 2);
        Assert.Equal(esperada.KwhProyectados, resumen.KwhProyectados);
        Assert.Equal(esperada.MontoProyectado, resumen.MontoProyectado);
        Assert.Equal(17.2m, resumen.PromedioDiarioKwh);
    }

    [Fact]
    public async Task ObtenerAsync_ConDemo_HistorialDeCincoCerradosMasElActual()
    {
        await _demo.CargarAsync(Hoy);

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.NotNull(resumen);
        Assert.Equal(6, resumen.Historial.Count);
        Assert.Equal([764m, 912m, 1_046m, 871m, 718m], resumen.Historial.Take(5).Select(p => p.Kwh));
        Assert.All(resumen.Historial.Take(5), p => Assert.False(p.EsActual));
        Assert.True(resumen.Historial[^1].EsActual);
        Assert.Equal(resumen.KwhProyectados, resumen.Historial[^1].Kwh);
        Assert.Equal(resumen.KwhProyectados / 718m - 1, resumen.VariacionVsPeriodoAnterior);
    }

    [Fact]
    public async Task ObtenerAsync_ConDemo_EvaluaDacConElPromedioMensualDeDoceMeses()
    {
        await _demo.CargarAsync(Hoy);

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.NotNull(resumen?.Dac);
        Assert.Equal(850m, resumen.LimiteDacKwhMensual);
        // 6 bimestres (5,131 kWh) repartidos en 12 meses.
        Assert.Equal(5_131m / 12, resumen.Dac.PromedioMensualKwh);
        Assert.Equal(NivelRiesgoDac.Bajo, resumen.Dac.Nivel);
    }

    [Fact]
    public async Task ObtenerAsync_ConPresupuesto_CalculaElRestanteYLaDiferencia()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { PresupuestoMax = 1_500m });
        await _demo.CargarAsync(Hoy);
        var tarifa = await TarifaAsync("1C");

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.NotNull(resumen);
        var esperado = new PresupuestoInversoService(_calculadora).Calcular(
            1_500m, tarifa, resumen.DiasDelPeriodo, mesesDelPeriodo: 2, kwhConsumidos: 17.2m * 24, diasTranscurridos: 24);
        Assert.Equal(1_500m, resumen.PresupuestoMax);
        Assert.Equal(esperado.KwhPorDiaRestante, resumen.KwhPorDiaRestante);
        Assert.Equal(resumen.MontoProyectado - 1_500m, resumen.DiferenciaContraPresupuesto);
        var ritmo = esperado.KwhMaximosPeriodo / resumen.DiasDelPeriodo;
        Assert.Equal(17.2m / ritmo - 1, resumen.VariacionRitmoPresupuesto);

        // Lo que necesita la pantalla de Presupuesto, sin recalcular nada.
        Assert.Equal(esperado.KwhMaximosPeriodo, resumen.KwhMaximosPeriodo);
        Assert.Equal(esperado.KwhRestantes, resumen.KwhRestantes);
        Assert.Equal(ritmo, resumen.KwhPorDiaPermitido);
        Assert.True(resumen.PresupuestoCubreCargoFijo);
    }

    [Fact]
    public async Task ObtenerAsync_PresupuestoMenorAlCargoFijo_NoLoCubre()
    {
        // PDBT de ejemplo: cargo fijo de 60 al mes; un tope de 50 no alcanza.
        var pdbt = await TarifaAsync("PDBT");
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], TarifaId = pdbt.Id, PresupuestoMax = 50m });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.NotNull(resumen);
        Assert.False(resumen.PresupuestoCubreCargoFijo);
        Assert.Equal(0m, resumen.KwhMaximosPeriodo);
    }

    [Fact]
    public async Task ObtenerAsync_SinPresupuesto_LosCamposDePresupuestoQuedanEnNull()
    {
        await _demo.CargarAsync(Hoy);

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.Null(resumen?.PresupuestoMax);
        Assert.Null(resumen?.KwhPorDiaRestante);
        Assert.Null(resumen?.DiferenciaContraPresupuesto);
    }

    [Fact]
    public async Task ObtenerAsync_LugarRealConDatos_TienePrioridadSobreLaDemo()
    {
        var tarifa = await TarifaAsync("1");
        await _demo.CargarAsync(Hoy);
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 31,
            Periodos = [new Periodo { Inicio = new(2026, 7, 1), Fin = new(2026, 8, 31), KwhTotales = 500m, Monto = 700m }],
        });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.False(resumen?.EsDemo);
        Assert.Equal("Mi casa", resumen?.NombreLugar);
    }

    [Fact]
    public async Task ObtenerAsync_LugarRealSinDatosYDemo_MuestraLaDemo()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa] });
        await _demo.CargarAsync(Hoy);

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.True(resumen?.EsDemo);
    }

    [Fact]
    public async Task ObtenerAsync_PeriodosSinLecturas_HistorialSoloConLosCerrados()
    {
        var tarifa = await TarifaAsync("1");
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 31,
            Periodos = [new Periodo { Inicio = new(2026, 7, 1), Fin = new(2026, 8, 31), KwhTotales = 500m, Monto = 700m }],
        });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.NotNull(resumen);
        Assert.True(resumen.TieneDatos);
        Assert.Null(resumen.KwhHastaHoy);
        Assert.Null(resumen.MontoProyectado);
        Assert.Equal([500m], resumen.Historial.Select(p => p.Kwh));
        Assert.Equal(new DateOnly(2026, 9, 1), resumen.InicioPeriodo);
    }

    [Fact]
    public async Task ObtenerAsync_TarifaSinLimiteDac_NoEvaluaDac()
    {
        var tarifa = await TarifaAsync("PDBT");
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Local",
            Tipo = TipoLugar.Negocio,
            TarifaId = tarifa.Id,
            DiaCorte = 31,
            MesesPorPeriodo = 1,
            Periodos = [new Periodo { Inicio = new(2026, 9, 1), Fin = new(2026, 9, 30), KwhTotales = 300m, Monto = 1_260m }],
        });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.Null(resumen?.Dac);
        Assert.Equal(1, resumen?.MesesDelPeriodo);
    }

    [Fact]
    public async Task ObtenerPropiedadesAsync_DevuelveElUltimoPeriodoDeCadaPropiedad()
    {
        var tarifa = await TarifaAsync("1");
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Depto 2",
            Tipo = TipoLugar.Propiedad,
            TarifaId = tarifa.Id,
            Periodos =
            [
                new Periodo { Inicio = new(2026, 5, 1), Fin = new(2026, 6, 30), KwhTotales = 280m, Monto = 310m },
                new Periodo { Inicio = new(2026, 7, 1), Fin = new(2026, 8, 31), KwhTotales = 300m, Monto = 335m },
            ],
        });
        await AgregarLugarAsync(new Lugar { Nombre = "Mi casa", Tipo = TipoLugar.Casa, TarifaId = tarifa.Id });
        await AgregarLugarAsync(new Lugar { Nombre = "Local 3", Tipo = TipoLugar.Propiedad, TarifaId = tarifa.Id });

        var propiedades = await _servicio.ObtenerPropiedadesAsync();

        Assert.Collection(propiedades,
            p => { Assert.Equal("Depto 2", p.Nombre); Assert.Equal(300m, p.UltimoKwh); Assert.Equal(335m, p.UltimoMonto); },
            p => { Assert.Equal("Local 3", p.Nombre); Assert.Null(p.UltimoKwh); });
    }

    // ---- Reglas de lecturas y periodos (Fase 3a) ----

    private static Lectura L(int anio, int mes, int dia, decimal valor) => new() { Fecha = new(anio, mes, dia), ValorKwh = valor };

    /// <summary>
    /// Casa mensual con corte el día 1 y un periodo cerrado (2 ago - 1 sep) con su lectura de corte.
    /// </summary>
    private async Task AgregarCasaMensualAsync(params Lectura[] lecturasDelPeriodoActual)
    {
        var tarifa = await TarifaAsync("1");
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 1,
            MesesPorPeriodo = 1,
            Periodos = [new Periodo { Inicio = new(2026, 8, 2), Fin = new(2026, 9, 1), KwhTotales = 300m, Monto = 400m }],
            Lecturas = [L(2026, 8, 1, 700m), L(2026, 9, 1, 1_000m), .. lecturasDelPeriodoActual],
        });
    }

    [Fact]
    public async Task ObtenerAsync_KwhHastaHoy_DesdeLaLecturaDeCorteDelPeriodoAnterior()
    {
        await AgregarCasaMensualAsync(L(2026, 9, 11, 1_100m), L(2026, 9, 15, 1_140m));

        var resumen = await _servicio.ObtenerAsync(new DateOnly(2026, 9, 20));

        // Periodo 2 sep - 1 oct; base: corte del 1 sep (1000); última: 15 sep (1140), 140 kWh en 14 días.
        Assert.NotNull(resumen);
        Assert.Equal(new DateOnly(2026, 9, 2), resumen.InicioPeriodo);
        Assert.Equal(new DateOnly(2026, 10, 1), resumen.FinPeriodo);
        Assert.Equal(140m, resumen.KwhHastaHoy);
        Assert.Equal(10m, resumen.PromedioDiarioKwh);
        Assert.False(resumen.PeriodoTerminado);
        Assert.Equal(11, resumen.DiasParaCorte);
    }

    [Theory]
    [InlineData(2026, 10, 1, 0)]
    [InlineData(2026, 10, 19, -18)]
    public async Task ObtenerAsync_CorteSinLecturaEnOdespuesDelFin_PeriodoTerminado(int anio, int mes, int dia, int diasParaCorte)
    {
        // El periodo 2 sep - 1 oct no se ha cerrado: falta una lectura en o después del 1 oct.
        await AgregarCasaMensualAsync(L(2026, 9, 20, 1_190m));

        var resumen = await _servicio.ObtenerAsync(new DateOnly(anio, mes, dia));

        Assert.NotNull(resumen);
        Assert.True(resumen.PeriodoTerminado);
        Assert.Equal(diasParaCorte, resumen.DiasParaCorte);
        Assert.Equal(new DateOnly(2026, 10, 1), resumen.FinPeriodo);
        Assert.Equal(190m, resumen.KwhHastaHoy);
        Assert.Equal(resumen.DiasDelPeriodo, resumen.DiaDelPeriodo);
    }

    /// <summary>
    /// Periodos consecutivos, del más antiguo al más reciente, que terminan antes de <see cref="Hoy"/>.
    /// </summary>
    private static List<Periodo> Consecutivos(DateOnly primerInicio, int meses, params decimal[] kwh)
    {
        var inicio = primerInicio;
        return kwh.Select(k =>
        {
            var periodo = new Periodo { Inicio = inicio, Fin = inicio.AddMonths(meses).AddDays(-1), KwhTotales = k, Monto = 1m };
            inicio = periodo.Fin.AddDays(1);
            return periodo;
        }).ToList();
    }

    [Fact]
    public async Task ObtenerAsync_DacBimestral_CadaBimestreAportaDosMesesYSoloCuentanDoce()
    {
        var tarifa = await TarifaAsync("1C");
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 31,
            MesesPorPeriodo = 2,
            // Ocho bimestres: los dos más antiguos quedan fuera de los últimos 12 meses.
            Periodos = Consecutivos(new DateOnly(2025, 6, 1), 2, 5_000m, 5_000m, 1_200m, 1_000m, 1_400m, 1_200m, 1_000m, 1_400m),
        });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        // (1200 + 1000 + 1400 + 1200 + 1000 + 1400) / 12 meses = 600 kWh al mes.
        Assert.Equal(600m, resumen?.Dac?.PromedioMensualKwh);
    }

    [Fact]
    public async Task ObtenerAsync_DacMensual_CadaMesAportaUnValorYSoloCuentanDoce()
    {
        var tarifa = await TarifaAsync("1C");
        await AgregarLugarAsync(new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 31,
            MesesPorPeriodo = 1,
            // Catorce meses: los dos más antiguos quedan fuera.
            Periodos = Consecutivos(new DateOnly(2025, 8, 1), 1,
                9_000m, 9_000m, 300m, 500m, 300m, 500m, 300m, 500m, 300m, 500m, 300m, 500m, 300m, 500m),
        });

        var resumen = await _servicio.ObtenerAsync(Hoy);

        Assert.Equal(400m, resumen?.Dac?.PromedioMensualKwh);
    }
}
