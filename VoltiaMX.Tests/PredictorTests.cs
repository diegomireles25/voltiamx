using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Lo que muestra el Predictor: desglose del pago proyectado y detalle del riesgo DAC.
/// </summary>
public sealed class PredictorTests : IAsyncLifetime
{
    private readonly TarifaCalculator _calculadora = new();
    private FabricaDePrueba _fabrica = null!;
    private ResumenDashboardService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new ResumenDashboardService(
            _fabrica,
            new ProyeccionService(_calculadora),
            new DacService(),
            new PresupuestoInversoService(_calculadora));
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private static Lectura L(int mes, int dia, decimal valor) => new() { Fecha = new(2026, mes, dia), ValorKwh = valor };

    private async Task AgregarAsync(Lugar lugar, Tarifa? tarifaNueva = null)
    {
        await using var db = _fabrica.CreateDbContext();
        if (tarifaNueva is not null)
        {
            db.Tarifas.Add(tarifaNueva);
            await db.SaveChangesAsync();
            lugar.TarifaId = tarifaNueva.Id;
        }

        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
    }

    private async Task<int> IdDeTarifaAsync(string nombre)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Tarifas.Where(t => t.Nombre == nombre).Select(t => t.Id).SingleAsync();
    }

    [Fact]
    public async Task Desglose_TarifaDeTresRangos_CuadraConElPagoProyectado()
    {
        // Tarifa de prueba (no es de CFE): 0-100 a $1.00, 100-200 a $1.50, más de 200 a $3.00, cargo fijo $10.
        var tarifa = new Tarifa
        {
            Nombre = "PRUEBA",
            Tipo = TipoTarifa.Domestica,
            CargoFijoMensual = 10m,
            Vigencia = new(2026, 1, 1),
            Rangos =
            [
                new TarifaRango { Nombre = "Básico", KwhDesde = 0m, KwhHasta = 100m, PrecioPorKwh = 1.00m, Orden = 1 },
                new TarifaRango { Nombre = "Intermedio", KwhDesde = 100m, KwhHasta = 200m, PrecioPorKwh = 1.50m, Orden = 2 },
                new TarifaRango { Nombre = "Excedente", KwhDesde = 200m, PrecioPorKwh = 3.00m, Orden = 3 },
            ],
        };
        await AgregarAsync(
            new Lugar
            {
                Nombre = "Mi casa",
                DiaCorte = 1,
                MesesPorPeriodo = 1,
                Periodos = [new Periodo { Inicio = new(2026, 8, 2), Fin = new(2026, 9, 1), KwhTotales = 300m, Monto = 560m }],
                Lecturas = [L(8, 1, 700m), L(9, 1, 1_000m), L(9, 11, 1_100m)],
            },
            tarifa);

        var resumen = await _servicio.ObtenerAsync(new DateOnly(2026, 9, 11));

        // A mano: periodo del 2 sep al 1 oct (30 días). Del corte (1 sep, 1000) al 11 sep (1100) van
        // 100 kWh en 10 días: 10 kWh al día, 300 kWh al cierre.
        // 100 × $1.00 = $100; 100 × $1.50 = $150; 100 × $3.00 = $300. Energía $550 + cargo fijo $10 = $560.
        Assert.NotNull(resumen?.DesgloseProyectado);
        Assert.Equal(300m, resumen.KwhProyectados);
        Assert.Equal(
            [
                new DesgloseRango("Básico", 100m, 1.00m, 100m),
                new DesgloseRango("Intermedio", 100m, 1.50m, 150m),
                new DesgloseRango("Excedente", 100m, 3.00m, 300m),
            ],
            resumen.DesgloseProyectado.Desglose);
        Assert.Equal(10m, resumen.DesgloseProyectado.CargoFijo);
        Assert.Equal(560m, resumen.DesgloseProyectado.Total);
        Assert.Equal(resumen.MontoProyectado, resumen.DesgloseProyectado.Total);
    }

    [Fact]
    public async Task Desglose_SinProyeccion_QuedaEnNull()
    {
        await AgregarAsync(new Lugar { Nombre = "Mi casa", DiaCorte = 1, TarifaId = await IdDeTarifaAsync("1C") });

        var resumen = await _servicio.ObtenerAsync(new DateOnly(2026, 9, 11));

        Assert.Null(resumen?.DesgloseProyectado);
    }

    [Fact]
    public void Redondeado_LasFilasSumanExactamenteElTotalMostrado()
    {
        // Sin redondear, 0.005 + 0.005 + 0.004 = 0.014 se mostraría como 0.01, pero las filas
        // redondeadas dicen 0.01 + 0.01 + 0.00: el total mostrado debe ser 0.02.
        var calculo = new ResultadoCalculo(
            [new DesgloseRango("A", 0.333m, 0.015m, 0.005m), new DesgloseRango("B", 1.005m, 0.0049m, 0.005m)],
            0.010m,
            0.004m,
            0.014m);

        var redondeado = DesgloseRedondeado.Desde(calculo);

        Assert.Equal(
            [new RangoRedondeado("A", 0.33m, 0.02m, 0.01m), new RangoRedondeado("B", 1.01m, 0.00m, 0.01m)],
            redondeado.Rangos);
        Assert.Equal(0.00m, redondeado.CargoFijo);
        Assert.Equal(redondeado.Rangos.Sum(r => r.Subtotal) + redondeado.CargoFijo, redondeado.Total);
        Assert.Equal(0.02m, redondeado.Total);
    }

    [Fact]
    public async Task Dac_MenosDeDoceMeses_DiceCuantosHayYSiExcedeElLimite()
    {
        // Tarifa 1C de ejemplo: límite de 850 kWh al mes. Dos meses de 900 y 1,000: promedio 950,
        // 100 por arriba del límite.
        await AgregarAsync(new Lugar
        {
            Nombre = "Mi casa",
            DiaCorte = 1,
            MesesPorPeriodo = 1,
            TarifaId = await IdDeTarifaAsync("1C"),
            Periodos =
            [
                new Periodo { Inicio = new(2026, 7, 2), Fin = new(2026, 8, 1), KwhTotales = 900m, Monto = 1m },
                new Periodo { Inicio = new(2026, 8, 2), Fin = new(2026, 9, 1), KwhTotales = 1_000m, Monto = 1m },
            ],
        });

        var resumen = await _servicio.ObtenerAsync(new DateOnly(2026, 9, 11));

        Assert.NotNull(resumen?.Dac);
        Assert.Equal(2, resumen.MesesEnPromedioDac);
        Assert.Equal(850m, resumen.LimiteDacKwhMensual);
        Assert.Equal(new EvaluacionDac(NivelRiesgoDac.Alto, 950m, -100m, ExcedeLimite: true), resumen.Dac);
    }

    [Fact]
    public async Task Dac_ConDemo_PromedioDeDoceMeses()
    {
        await new DatosDemoService(_fabrica, _calculadora).CargarAsync(new DateOnly(2026, 10, 19));

        var resumen = await _servicio.ObtenerAsync(new DateOnly(2026, 10, 19));

        Assert.Equal(12, resumen?.MesesEnPromedioDac);
    }
}
