using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9b (D5): la fecha de corte de un recibo no puede ser posterior a hoy (hoy mismo sí). Se prueba en las
/// tres entradas: el onboarding, "Agregar un recibo anterior por su monto" y el escáner.
/// </summary>
public sealed class FechaCorteTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 11, 5);

    // Hoy, mañana, 20 días después, el año equivocado (2027 en lugar de 2026) y una fecha pasada válida.
    public static TheoryData<DateOnly, bool> Fechas => new()
    {
        { Hoy, true },
        { Hoy.AddDays(1), false },
        { Hoy.AddDays(20), false },
        { new DateOnly(2027, 10, 26), false },
        { new DateOnly(2026, 10, 26), true },
    };

    private FabricaDePrueba _fabrica = null!;
    private RecibosService _recibos = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        var calculadora = new TarifaCalculator();
        _recibos = new RecibosService(_fabrica, new PresupuestoInversoService(calculadora), calculadora);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(Fechas))]
    public void Validador(DateOnly fecha, bool valida)
    {
        Assert.Equal(valida ? null : ErrorFechaCorte.PosteriorAHoy, ValidadorFechaCorte.Validar(fecha, Hoy));
    }

    [Theory]
    [MemberData(nameof(Fechas))]
    public void Onboarding_ReciboPorMonto(DateOnly fecha, bool valida)
    {
        Assert.Equal(valida ? null : ErrorReciboPorMonto.FechaFutura, EstimadorPorMonto.ValidarRecibo(1_000m, fecha, Hoy));
    }

    [Fact]
    public void Onboarding_TotalFueraDeRango_SeRevisaAntesQueLaFecha()
    {
        Assert.Equal(ErrorReciboPorMonto.TotalFueraDeRango, EstimadorPorMonto.ValidarRecibo(0m, Hoy.AddDays(3), Hoy));
    }

    [Theory]
    [MemberData(nameof(Fechas))]
    public async Task AgregarPorMonto(DateOnly fecha, bool valida)
    {
        int lugarId;
        await using (var db = _fabrica.CreateDbContext())
        {
            var tarifa = db.Tarifas.Single(t => t.Nombre == "1C");
            var lugar = new Core.Models.Lugar { Nombre = "Mi casa", DiaCorte = fecha.Day, MesesPorPeriodo = 2, TarifaId = tarifa.Id };
            db.Lugares.Add(lugar);
            await db.SaveChangesAsync();
            lugarId = lugar.Id;
        }

        var r = await _recibos.AgregarPorMontoAsync(lugarId, 1_000m, fecha, 16m, Hoy);

        Assert.Equal(valida ? null : ErrorReciboPorMonto.FechaFutura, r.Error);
    }

    [Theory]
    [MemberData(nameof(Fechas))]
    public void Escaner(DateOnly fecha, bool valida)
    {
        var recibo = new ReciboAConfirmar { FechaCorte = fecha, FechaLectura = new DateOnly(2026, 10, 20), LecturaActual = 48_210m };

        var errores = recibo.ANuevaLectura(lugarId: 7, Hoy).Errores;

        Assert.Equal(valida, !errores.Contains(ErrorConfirmacionRecibo.FechaCorteFutura));
    }

    [Fact]
    public void Escaner_LecturaConFechaFutura_TambienSeRechaza()
    {
        var recibo = new ReciboAConfirmar { FechaCorte = Hoy, FechaLectura = Hoy.AddDays(2), LecturaActual = 48_210m };

        Assert.Contains(ErrorConfirmacionRecibo.FechaCorteFutura, recibo.ANuevaLectura(7, Hoy).Errores);
    }
}
