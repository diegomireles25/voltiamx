using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class HistorialServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private readonly TarifaCalculator _calculadora = new();
    private FabricaDePrueba _fabrica = null!;
    private HistorialService _servicio = null!;
    private ResumenDashboardService _resumenes = null!;
    private DatosDemoService _demo = null!;
    private PerfilService _perfiles = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        var proyeccion = new ProyeccionService(_calculadora);
        var presupuesto = new PresupuestoInversoService(_calculadora);
        _servicio = new HistorialService(_fabrica, proyeccion, presupuesto);
        _resumenes = new ResumenDashboardService(_fabrica, proyeccion, new DacService(), presupuesto);
        _demo = new DatosDemoService(_fabrica, _calculadora);
        _perfiles = new PerfilService(_fabrica);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> IdAsync(bool esDemo)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Lugares.Where(l => l.EsDemo == esDemo).Select(l => l.Id).SingleAsync();
    }

    [Fact]
    public async Task ObtenerAsync_LugarInexistente_DevuelveNull()
    {
        Assert.Null(await _servicio.ObtenerAsync(999, Hoy));
    }

    [Fact]
    public async Task ObtenerAsync_SinPeriodosNiLecturas_HistorialVacio()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa] });

        var historial = await _servicio.ObtenerAsync(await IdAsync(esDemo: false), Hoy);

        Assert.NotNull(historial);
        Assert.Empty(historial.Periodos);
        Assert.Empty(historial.AcumuladoActual);
        Assert.Equal(0, historial.PeriodosCerrados);
        Assert.Null(historial.KwhPresupuestoPeriodo);
    }

    [Fact]
    public async Task ObtenerAsync_ConDemo_SeisCerradosYElActualCuadranConElDashboard()
    {
        await _demo.CargarAsync(Hoy);

        var historial = await _servicio.ObtenerAsync(await IdAsync(esDemo: true), Hoy);
        var resumen = await _resumenes.ObtenerAsync(Hoy);

        Assert.NotNull(historial);
        Assert.NotNull(resumen);
        Assert.Equal(6, historial.PeriodosCerrados);
        Assert.Equal([820m, 764m, 912m, 1_046m, 871m, 718m], historial.Periodos.Where(p => !p.EsActual).Select(p => p.Kwh));
        var primero = historial.Periodos[0];
        Assert.Equal(primero.Kwh / (primero.Fin.DayNumber - primero.Inicio.DayNumber + 1), primero.KwhPorDia);

        var actual = historial.Periodos[^1];
        Assert.True(actual.EsActual);
        Assert.Equal((resumen.InicioPeriodo, resumen.FinPeriodo), (actual.Inicio, actual.Fin));
        Assert.Equal(new Ventana(resumen.InicioPeriodo, resumen.FinPeriodo), historial.PeriodoActual);
        Assert.Equal(resumen.KwhProyectados, actual.Kwh);
        Assert.Equal(resumen.MontoProyectado, actual.Monto);
        Assert.Equal(17.2m, actual.KwhPorDia);
        Assert.Null(historial.KwhPresupuestoPeriodo);
    }

    [Fact]
    public async Task ObtenerAsync_ConDemo_AcumuladoDelPeriodoActualDesdeElCorte()
    {
        await _demo.CargarAsync(Hoy);

        var historial = await _servicio.ObtenerAsync(await IdAsync(esDemo: true), Hoy);

        // Corte de hace 24 días (base, 0 kWh), una lectura cada 3 días y la de hoy: 17.2 kWh por día.
        Assert.NotNull(historial);
        Assert.Equal(9, historial.AcumuladoActual.Count);
        Assert.Equal(new PuntoAcumulado(Hoy.AddDays(-24), 0m), historial.AcumuladoActual[0]);
        Assert.Equal(new PuntoAcumulado(Hoy, 17.2m * 24), historial.AcumuladoActual[^1]);
    }

    [Fact]
    public async Task ObtenerAsync_PeriodoEstimadoConMontoDelRecibo_LosConserva()
    {
        await using (var db = _fabrica.CreateDbContext())
        {
            var tarifa = await db.Tarifas.SingleAsync(t => t.Nombre == "1C");
            db.Lugares.Add(new Lugar
            {
                Nombre = "Mi casa",
                TarifaId = tarifa.Id,
                DiaCorte = 1,
                MesesPorPeriodo = 1,
                Periodos =
                [
                    new Periodo
                    {
                        Inicio = new(2026, 9, 2), Fin = new(2026, 10, 1), KwhTotales = 300m, Monto = 410m,
                        Estimado = true, MontoRecibo = 512.50m,
                    },
                ],
            });
            await db.SaveChangesAsync();
        }

        var historial = await _servicio.ObtenerAsync(await IdAsync(esDemo: false), Hoy);

        var periodo = Assert.Single(historial!.Periodos);
        Assert.Equal(new PeriodoDeHistorial(new(2026, 9, 2), new(2026, 10, 1), 300m, 410m, true, 512.50m, 10m, false), periodo);
    }

    [Fact]
    public async Task ObtenerAsync_ConPresupuesto_TopeDeKwhDelPresupuestoInverso()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { PresupuestoMax = 1_500m });
        await _demo.CargarAsync(Hoy);
        Tarifa tarifa;
        await using (var db = _fabrica.CreateDbContext())
        {
            tarifa = await db.Tarifas.Include(t => t.Rangos).SingleAsync(t => t.Nombre == "1C");
        }

        var historial = await _servicio.ObtenerAsync(await IdAsync(esDemo: true), Hoy);

        var esperado = new PresupuestoInversoService(_calculadora).Calcular(
            1_500m, tarifa, historial!.PeriodoActual.Dias, mesesDelPeriodo: 2, kwhConsumidos: 17.2m * 24, diasTranscurridos: 24);
        Assert.Equal(esperado.KwhMaximosPeriodo, historial.KwhPresupuestoPeriodo);
    }
}
