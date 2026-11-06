using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9e (D4): el espacio que ocupan los paneles. Espacio = paneles × superficie de cada panel × (1 + espacio de
/// paso / 100), redondeado hacia arriba a 0.5 m². Es una estimación: no dice nada del techo de la persona.
/// </summary>
public sealed class EspacioSolarTests
{
    private static readonly TarifaCalculator Calculadora = new();
    private static readonly DacService Dac = new();

    private static ParametrosSolar Parametros(decimal consumo = 600m, decimal superficie = 2.6m, decimal paso = 10m, decimal? costo = null)
        => new(consumo, 100m, 5m, 0.80m, 550m, costo, superficie, paso);

    private static DatosLugarSolar Lugar() => new("Casa", false, 600m, TarifasDePrueba.Escalonada(), 1, []);

    private static ResultadoSolar Calcular(ParametrosSolar p) => SimuladorSolar.Calcular(p, Lugar(), Calculadora, Dac);

    [Fact]
    public void CasoDeReferencia_NuevePaneles_VeintiseisMetros()
    {
        // 600 kWh, 100 %, 5 h, 0.80 y 550 W → 9 paneles (SimuladorSolarTests). 9 × 2.6 × 1.10 = 25.74 → 26.0 m².
        var r = Calcular(Parametros());

        Assert.Equal(9, r.Paneles);
        Assert.Equal(26.0m, r.EspacioM2);
    }

    [Fact]
    public void ValoresPorDefecto_SonLosDeEjemplo()
    {
        Assert.Equal(2.6m, ValoresSolarPorDefecto.SuperficiePanelM2);
        Assert.Equal(10m, ValoresSolarPorDefecto.EspacioPasoPorciento);
        var p = new ParametrosSolar(600m, 100m, 5m, 0.80m, 550m, null);
        Assert.Equal((2.6m, 10m), (p.SuperficiePanelM2, p.EspacioPasoPorciento));
    }

    [Theory]
    // Un panel: 1 × 2.6 × 1.1 = 2.86 → 3.0.
    [InlineData(1, 2.6, 10, 3.0)]
    // Superficie en los límites: 9 × 1 × 1.1 = 9.9 → 10.0; 9 × 4 × 1.1 = 39.6 → 40.0.
    [InlineData(9, 1.0, 10, 10.0)]
    [InlineData(9, 4.0, 10, 40.0)]
    // Sin espacio de paso: 9 × 2.6 = 23.4 → 23.5. Con 30 %: 9 × 2.6 × 1.3 = 30.42 → 30.5.
    [InlineData(9, 2.6, 0, 23.5)]
    [InlineData(9, 2.6, 30, 30.5)]
    // Exacto: 2 × 1.25 = 2.5 se queda en 2.5.
    [InlineData(2, 1.25, 0, 2.5)]
    public void Espacio_ALaMano(int paneles, double superficie, double paso, double esperado)
        => Assert.Equal((decimal)esperado, EspacioSolar.Calcular(paneles, (decimal)superficie, (decimal)paso));

    [Fact]
    public void ConsumoPequeno_UnPanel_TresMetros()
    {
        // 30 kWh al mes: kWp necesarios = 30 / 121.67 = 0.25 → 1 panel → 2.86 → 3.0 m².
        var r = Calcular(Parametros(consumo: 30m));

        Assert.Equal((1, 3.0m), (r.Paneles, r.EspacioM2));
    }

    [Fact]
    public void Validador_SuperficieYPaso()
    {
        Assert.Empty(SimuladorSolarValidador.Validar(Parametros(superficie: 1m, paso: 0m)));
        Assert.Empty(SimuladorSolarValidador.Validar(Parametros(superficie: 4m, paso: 30m)));
        Assert.Equal([CodigoErrorSolar.SuperficieFueraDeRango], SimuladorSolarValidador.Validar(Parametros(superficie: 0.99m)));
        Assert.Equal([CodigoErrorSolar.SuperficieFueraDeRango], SimuladorSolarValidador.Validar(Parametros(superficie: 4.01m)));
        Assert.Equal([CodigoErrorSolar.PasoFueraDeRango], SimuladorSolarValidador.Validar(Parametros(paso: -1m)));
        Assert.Equal([CodigoErrorSolar.PasoFueraDeRango], SimuladorSolarValidador.Validar(Parametros(paso: 31m)));
        // En el orden de la pantalla: la superficie y el paso van antes del costo.
        Assert.Equal(
            [CodigoErrorSolar.SuperficieFueraDeRango, CodigoErrorSolar.PasoFueraDeRango, CodigoErrorSolar.CostoFueraDeRango],
            SimuladorSolarValidador.Validar(Parametros(superficie: 5m, paso: 40m, costo: -1m)));
    }

    [Fact]
    public void SinCosto_NoHayInversionNiRecuperacion_ConCostoSi()
    {
        // D5: el costo empieza vacío; sin costo la pantalla no muestra recuperación.
        var sin = Calcular(Parametros());
        var con = Calcular(Parametros(costo: 20_000m));

        Assert.Equal((null, null), (sin.Inversion, sin.RecuperacionMeses));
        Assert.Equal(99_000m, con.Inversion);
        Assert.NotNull(con.RecuperacionMeses);
    }

    // ---- D2: el consumo de partida es aproximado si sale de un recibo por monto ----

    private static async Task<(FabricaDePrueba Fabrica, int LugarId)> LugarConReciboAsync(bool estimado, params Lectura[] lecturas)
    {
        var fabrica = await FabricaDePrueba.CrearAsync();
        await using var db = fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.FirstAsync(t => t.Nombre == "1C");
        var lugar = new Lugar
        {
            Nombre = "Mi casa",
            TarifaId = tarifa.Id,
            DiaCorte = 26,
            MesesPorPeriodo = 2,
            Periodos = [new Periodo { Inicio = new(2026, 7, 27), Fin = new(2026, 9, 26), KwhTotales = 600m, Estimado = estimado }],
            Lecturas = [.. lecturas],
        };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return (fabrica, lugar.Id);
    }

    [Fact]
    public async Task ObtenerDatos_ConUnReciboPorMonto_ConsumoAproximado()
    {
        // Recibo por monto de 600 kWh en el bimestre, sin lecturas: 300 kWh al mes, aproximado.
        var (fabrica, lugarId) = await LugarConReciboAsync(estimado: true);
        using var _ = fabrica;

        var datos = await new SimuladorSolarService(fabrica, Calculadora, Dac).ObtenerDatosAsync(lugarId, new DateOnly(2026, 10, 19));

        Assert.Equal(300m, datos.KwhMedidoPorMes);
        Assert.True(datos.ConsumoAproximado);
    }

    [Fact]
    public async Task ObtenerDatos_ConLecturasDelReciboActual_NoEsAproximado()
    {
        var (fabrica, lugarId) = await LugarConReciboAsync(estimado: true,
            new Lectura { Fecha = new DateOnly(2026, 10, 1), ValorKwh = 1_000m },
            new Lectura { Fecha = new DateOnly(2026, 10, 15), ValorKwh = 1_140m });
        using var _ = fabrica;

        var datos = await new SimuladorSolarService(fabrica, Calculadora, Dac).ObtenerDatosAsync(lugarId, new DateOnly(2026, 10, 19));

        Assert.False(datos.ConsumoAproximado);
        Assert.False(DatosLugarSolar.SinLugar.ConsumoAproximado);
    }
}
