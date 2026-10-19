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
            new PresupuestoInversoService(_calculadora));
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
        Assert.Equal(resumen.InicioPeriodo.AddMonths(2).AddDays(-1), resumen.FinPeriodo);
        Assert.Equal(resumen.FinPeriodo.DayNumber - Hoy.DayNumber, resumen.DiasParaCorte);
        Assert.Equal(17.2m * 23, resumen.KwhHastaHoy);

        var esperada = new ProyeccionService(_calculadora)
            .Proyectar(17.2m * 23, 23, resumen.DiasDelPeriodo, tarifa, mesesDelPeriodo: 2);
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
            1_500m, tarifa, resumen.DiasDelPeriodo, mesesDelPeriodo: 2, kwhConsumidos: 17.2m * 23, diasTranscurridos: 23);
        Assert.Equal(1_500m, resumen.PresupuestoMax);
        Assert.Equal(esperado.KwhPorDiaRestante, resumen.KwhPorDiaRestante);
        Assert.Equal(resumen.MontoProyectado - 1_500m, resumen.DiferenciaContraPresupuesto);
        var ritmo = esperado.KwhMaximosPeriodo / resumen.DiasDelPeriodo;
        Assert.Equal(17.2m / ritmo - 1, resumen.VariacionRitmoPresupuesto);
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
}
