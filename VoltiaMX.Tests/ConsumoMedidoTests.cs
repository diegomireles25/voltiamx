using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class ConsumoMedidoTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private FabricaDePrueba _fabrica = null!;

    public async Task InitializeAsync() => _fabrica = await FabricaDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private static Lugar Lugar(int meses = 2) => new() { Nombre = "Mi casa", DiaCorte = 1, MesesPorPeriodo = meses };

    [Fact]
    public void KwhPorMes_SinLecturasNiPeriodos_Null()
    {
        Assert.Null(ConsumoMedido.KwhPorMes(Lugar(), Hoy));
    }

    [Fact]
    public void KwhPorMes_SinBaseEnElPeriodo_UsaElUltimoPeriodoCerrado()
    {
        // Último periodo bimestral de 728 kWh: 728 / 2 = 364 kWh al mes.
        var lugar = Lugar();
        lugar.Periodos.Add(new Periodo { Inicio = new(2026, 5, 2), Fin = new(2026, 7, 1), KwhTotales = 600m });
        lugar.Periodos.Add(new Periodo { Inicio = new(2026, 7, 2), Fin = new(2026, 9, 1), KwhTotales = 728m });

        Assert.Equal(364m, ConsumoMedido.KwhPorMes(lugar, Hoy));
    }

    [Fact]
    public void KwhPorMes_ConBase_ProyectaElPeriodoYLoDivideEntreSusMeses()
    {
        // 100 kWh en 10 días = 10 kWh al día; proyectado a los días del periodo y entre 1 mes.
        var lugar = Lugar(meses: 1);
        lugar.Lecturas.Add(new Lectura { Fecha = new(2026, 10, 1), ValorKwh = 1_000m });
        lugar.Lecturas.Add(new Lectura { Fecha = new(2026, 10, 11), ValorKwh = 1_100m });

        var ventana = PeriodoActual.Ventana(lugar, Hoy);

        Assert.Equal(10m * ventana.Dias, ConsumoMedido.KwhPorMes(lugar, Hoy));
    }

    [Fact]
    public async Task KwhPorMes_ConLaDemo_EsElProyectadoDelDashboardEntreLosMeses()
    {
        await new DatosDemoService(_fabrica, new TarifaCalculator()).CargarAsync(Hoy);
        var calculadora = new TarifaCalculator();
        var resumen = await new ResumenDashboardService(
            _fabrica, new ProyeccionService(calculadora), new DacService(), new PresupuestoInversoService(calculadora), new TarifaCalculator())
            .ObtenerAsync(Hoy);

        await using var db = _fabrica.CreateDbContext();
        var lugar = await db.Lugares.Include(l => l.Lecturas).Include(l => l.Periodos).SingleAsync();

        Assert.Equal(resumen!.KwhProyectados / resumen.MesesDelPeriodo, ConsumoMedido.KwhPorMes(lugar, Hoy));
    }
}
