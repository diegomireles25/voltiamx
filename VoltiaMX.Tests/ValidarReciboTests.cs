using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class ValidarReciboTests : IAsyncLifetime
{
    private readonly TarifaCalculator _calculadora = new();
    private FabricaDePrueba _fabrica = null!;
    private TarifaService _tarifas = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _tarifas = new TarifaService(_fabrica, _calculadora);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tarifa de prueba (no es de CFE): 0-100 kWh al mes a $1, más de 100 a $2, cargo fijo $50.
    /// </summary>
    private static Tarifa Sencilla() => new()
    {
        Nombre = "PRUEBA",
        CargoFijoMensual = 50m,
        Rangos =
        [
            new TarifaRango { Nombre = "Básico", KwhDesde = 0m, KwhHasta = 100m, PrecioPorKwh = 1m, Orden = 1 },
            new TarifaRango { Nombre = "Excedente", KwhDesde = 100m, PrecioPorKwh = 2m, Orden = 2 },
        ],
    };

    // A mano, bimestral (los rangos se duplican: 0-200 a $1): 300 kWh = 200 × $1 + 100 × $2 = $400 de
    // energía. El cargo fijo no entra: el recibo se compara con la suma de Básico, Intermedio y Excedente.
    [Theory]
    [InlineData("400.80", VeredictoRecibo.Coincide)]   // $0.80 de diferencia: dentro de $1
    [InlineData("402.00", VeredictoRecibo.Coincide)]   // $2 = 0.4975 % del recibo: dentro de 0.5 %
    [InlineData("410.00", VeredictoRecibo.Cercano)]    // $10 = 2.44 %
    [InlineData("450.00", VeredictoRecibo.NoCoincide)] // $50 = 11.1 %
    public void Comparar_VeredictoSegunLaDiferencia(string recibo, VeredictoRecibo esperado)
    {
        var monto = decimal.Parse(recibo, System.Globalization.CultureInfo.InvariantCulture);

        var comparacion = new ValidadorRecibo(_calculadora).Comparar(Sencilla(), 2, 300m, monto);

        Assert.Equal(400m, comparacion.SubtotalEnergia);
        Assert.Equal(400m - monto, comparacion.Diferencia);
        Assert.Equal((400m - monto) / monto, comparacion.DiferenciaPorcentaje);
        Assert.Equal(esperado, comparacion.Veredicto);
        Assert.Equal(
            [new DesgloseRango("Básico", 200m, 1m, 200m), new DesgloseRango("Excedente", 100m, 2m, 200m)],
            comparacion.Calculo.Desglose);
    }

    [Fact]
    public void Comparar_ReciboEnCero_SinPorcentaje()
    {
        var comparacion = new ValidadorRecibo(_calculadora).Comparar(Sencilla(), 1, 0m, 0m);

        Assert.Null(comparacion.DiferenciaPorcentaje);
        Assert.Equal(VeredictoRecibo.Coincide, comparacion.Veredicto);
    }

    // ---- Recalcular periodos ----

    private async Task<int> LugarConPeriodosAsync()
    {
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.SingleAsync(t => t.Nombre == "1C");
        var lugar = new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 1,
            MesesPorPeriodo = 2,
            Periodos =
            [
                new Periodo { Inicio = new(2026, 3, 2), Fin = new(2026, 5, 1), KwhTotales = 600m, Monto = 1m },
                new Periodo { Inicio = new(2026, 5, 2), Fin = new(2026, 7, 1), KwhTotales = 700m, Monto = 1m },
                new Periodo { Inicio = new(2026, 7, 2), Fin = new(2026, 9, 1), KwhTotales = 800m, Monto = 999m, MontoRecibo = 999m },
            ],
        };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    [Fact]
    public async Task Recalcular_CambiaLosMontosSinTocarLosDelRecibo()
    {
        var lugar = await LugarConPeriodosAsync();

        Assert.Equal(new ConteoRecalculo(2, 1), await _tarifas.ContarParaRecalcularAsync(lugar));
        var resultado = await _tarifas.RecalcularPeriodosAsync(lugar);

        Assert.Equal(new ResultadoRecalculo(Recalculados: 2, Cambiados: 2, SaltadosPorRecibo: 1), resultado);
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.Include(t => t.Rangos).SingleAsync(t => t.Nombre == "1C");
        var periodos = await db.Periodos.Where(p => p.LugarId == lugar).OrderBy(p => p.Inicio).ToListAsync();
        Assert.Equal(_calculadora.Calcular(600m, tarifa, 2).Total, periodos[0].Monto);
        Assert.Equal(_calculadora.Calcular(700m, tarifa, 2).Total, periodos[1].Monto);
        Assert.Equal([600m, 700m, 800m], periodos.Select(p => p.KwhTotales));
        // El periodo con monto de recibo no se toca.
        Assert.Equal((999m, (decimal?)999m), (periodos[2].Monto, periodos[2].MontoRecibo));
    }

    [Fact]
    public async Task Recalcular_DosVeces_LaSegundaNoCambiaNada()
    {
        var lugar = await LugarConPeriodosAsync();
        await _tarifas.RecalcularPeriodosAsync(lugar);

        var segunda = await _tarifas.RecalcularPeriodosAsync(lugar);

        Assert.Equal(new ResultadoRecalculo(Recalculados: 2, Cambiados: 0, SaltadosPorRecibo: 1), segunda);
    }
}
