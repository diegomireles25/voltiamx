using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Costo mensual de un aparato (D4): lo que dejarías de pagar si no lo usaras. Tarifa de tres tramos
/// (0–100 a $1, 100–200 a $2, 200+ a $4) y 250 kWh al mes medidos: Total(250) = 100 + 200 + 200 = $500.
/// </summary>
public sealed class CostoAparatoTests
{
    private static readonly TarifaCalculator Calculadora = new();
    private static readonly Tarifa Tarifa = TarifasDePrueba.TresTramos();

    // 3 días a la semana: 3 × 52 / 12 = 13 días al mes, así cada 1,000 W por hora son 13 kWh al mes.
    private static Aparato Aparato(int id, string nombre, decimal watts, decimal horas, CategoriaAparato categoria = CategoriaAparato.Otro)
        => new() { Id = id, Nombre = nombre, Categoria = categoria, Watts = watts, HorasPorDia = horas, DiasPorSemana = 3 };

    [Fact]
    public void AparatoEnUnSoloTramo_CuestaSusKwhAlPrecioDeArriba()
    {
        // Sin él: Total(220) = 100 + 200 + 20 × 4 = $380. Costo: 500 − 380 = $120 (30 kWh a $4).
        Assert.Equal(120m, CostoAparato.CostoMensual(Calculadora, Tarifa, 1, 250m, 30m));
    }

    [Fact]
    public void AparatoQueCruzaDosTramos_CuestaCadaParteASuPrecio()
    {
        // Sin él: Total(150) = 100 + 50 × 2 = $200. Costo: 500 − 200 = $300 (50 kWh a $4 y 50 a $2).
        Assert.Equal(300m, CostoAparato.CostoMensual(Calculadora, Tarifa, 1, 250m, 100m));
    }

    [Fact]
    public void AparatoMayorQueLoMedido_SeLimitaAlConsumo()
    {
        // 400 kWh > 250 medidos: se limita a 250. Costo: Total(250) − Total(0) = $500.
        Assert.Equal(500m, CostoAparato.CostoMensual(Calculadora, Tarifa, 1, 250m, 400m));
    }

    [Fact]
    public void Bimestral_SeCalculaConElPeriodoCompletoYSeDivideEntreLosMeses()
    {
        // Bimestre: 500 kWh con tramos 0–200 a $1, 200–400 a $2 y 400+ a $4. Total(500) = 200 + 400 + 400 = $1,000.
        // Sin 60 kWh (30 al mes): Total(440) = 200 + 400 + 160 = $760. 240 / 2 = $120 al mes.
        Assert.Equal(120m, CostoAparato.CostoMensual(Calculadora, Tarifa, 2, 250m, 30m));
    }

    [Fact]
    public void Calcular_OrdenaPorCostoDeMayorAMenor()
    {
        // Lavadora 1,000 W × 3 h = 39 kWh; tele 300 W × 2 h = 7.8 kWh; aire 2,000 W × 2 h = 52 kWh.
        // Aire: Total(198) = 100 + 98 × 2 = 296 → $204. Lavadora: Total(211) = 300 + 44 = 344 → $156.
        // Tele: 7.8 kWh a $4 = $31.20.
        var costos = CostoAparato.Calcular(
            [Aparato(1, "Lavadora", 1_000m, 3m), Aparato(2, "Tele", 300m, 2m), Aparato(3, "Aire", 2_000m, 2m)],
            Calculadora, Tarifa, 1, 250m);

        Assert.True(costos.ConPesos);
        Assert.Equal(["Aire", "Lavadora", "Tele"], costos.Filas.Select(f => f.Nombre));
        Assert.Equal([204m, 156m, 31.2m], costos.Filas.Select(f => Math.Round(f.CostoMensual!.Value, 2)));
    }

    [Fact]
    public void Calcular_SinConsumoMedidoOSinTarifa_NoHayPesosYSeOrdenaPorConsumo()
    {
        Aparato[] aparatos = [Aparato(1, "Tele", 300m, 2m), Aparato(2, "Aire", 2_000m, 2m), Aparato(3, "Lavadora", 1_000m, 3m)];

        foreach (var costos in new[]
        {
            CostoAparato.Calcular(aparatos, Calculadora, Tarifa, 1, null),
            CostoAparato.Calcular(aparatos, Calculadora, Tarifa, 1, 0m),
            CostoAparato.Calcular(aparatos, Calculadora, null, 1, 250m),
        })
        {
            Assert.False(costos.ConPesos);
            Assert.Equal(["Aire", "Lavadora", "Tele"], costos.Filas.Select(f => f.Nombre));
            Assert.All(costos.Filas, f => Assert.Null(f.CostoMensual));
        }
    }

    [Fact]
    public void Calcular_SinAparatos_ListaVacia()
    {
        Assert.Empty(CostoAparato.Calcular([], Calculadora, Tarifa, 1, 250m).Filas);
    }

    [Fact]
    public void Costo_EsLoMismoQueElSimuladorAlQuitarTodoSuConsumo()
    {
        // El simulador no acepta 0 horas; con un escenario de menos watts que no consume casi nada se compara
        // directamente la fórmula: costo = AhorroConTarifa con todo el consumo del aparato.
        var aire = Aparato(3, "Aire", 2_000m, 2m);
        var consumoAire = ConsumoAparato.KwhPorMes(aire);
        var (pesos, _, _) = AhorroConTarifa.Pesos(Calculadora, Tarifa, 1, 250m, consumoAire);

        var costo = CostoAparato.Calcular([aire], Calculadora, Tarifa, 1, 250m).Filas.Single().CostoMensual;

        Assert.Equal(pesos, costo);

        // Y el simulador con un equipo de 1 W (casi nada) ahorra un poco menos que el costo, nunca más.
        var simulador = new SimuladorAhorroService(null!, Calculadora, new DacService());
        var resultado = simulador.Simular(new EntradaSimulacion(
            aire, EscenarioAhorro.EquipoEficiente(1m), Tarifa, 1, 250m, []));
        Assert.True(resultado.AhorroPesosMes < costo);
        Assert.True(resultado.AhorroPesosMes > costo - 1m);
    }
}
