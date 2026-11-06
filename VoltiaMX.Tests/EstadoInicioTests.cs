using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9b (D6): qué muestra Inicio. Inicio nunca queda vacío ni con días negativos.
/// </summary>
public sealed class EstadoInicioTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 5);

    // Recibo actual que empezó el 27 de septiembre (último corte el 26).
    private static readonly DateOnly InicioPasado = new(2026, 9, 27);

    [Fact]
    public void SinRecibosNiLecturas_SinRecibo()
    {
        Assert.Equal(EstadoPantallaInicio.SinRecibo, EstadoInicio.Evaluar(recibos: 0, lecturas: 0, InicioPasado, consumoMedido: false, Hoy));
    }

    [Fact]
    public void ConReciboYSinConsumoMedido_ReciboSinLecturas()
    {
        Assert.Equal(EstadoPantallaInicio.ReciboSinLecturas, EstadoInicio.Evaluar(1, 0, InicioPasado, false, Hoy));
        // Una sola lectura todavía no mide consumo.
        Assert.Equal(EstadoPantallaInicio.ReciboSinLecturas, EstadoInicio.Evaluar(1, 1, InicioPasado, false, Hoy));
    }

    [Fact]
    public void ConConsumoMedido_ConLecturas()
    {
        Assert.Equal(EstadoPantallaInicio.ConLecturas, EstadoInicio.Evaluar(1, 2, InicioPasado, true, Hoy));
    }

    [Fact]
    public void LecturasSinRecibo_ComoHoy()
    {
        // Quien solo anotó lecturas ve Inicio como siempre ("Anota dos veces…" o "A este ritmo").
        Assert.Equal(EstadoPantallaInicio.ConLecturas, EstadoInicio.Evaluar(0, 1, InicioPasado, false, Hoy));
        Assert.Equal(EstadoPantallaInicio.ConLecturas, EstadoInicio.Evaluar(0, 2, InicioPasado, true, Hoy));
    }

    [Fact]
    public void ElRecibActualEmpiezaDespuesDeHoy_FechaFutura()
    {
        // Último corte el 24 de noviembre: el recibo actual empezaría el 25, después de hoy (5 de noviembre).
        var inicio = new DateOnly(2026, 11, 25);

        Assert.Equal(EstadoPantallaInicio.FechaFutura, EstadoInicio.Evaluar(1, 0, inicio, false, Hoy));
        Assert.Equal(EstadoPantallaInicio.FechaFutura, EstadoInicio.Evaluar(1, 3, inicio, true, Hoy));
    }

    [Fact]
    public void ElRecibActualEmpiezaHoy_NoEsFechaFutura()
    {
        Assert.Equal(EstadoPantallaInicio.ReciboSinLecturas, EstadoInicio.Evaluar(1, 0, Hoy, false, Hoy));
    }

    // ---- Fase 9d (D5): el recibo ya debió llegar ----

    [Fact]
    public void ReciboPorLlegar_ConLecturasYElPeriodoTerminado()
    {
        Assert.True(EstadoInicio.ReciboPorLlegar(EstadoPantallaInicio.ConLecturas, periodoTerminado: true));
    }

    [Theory]
    [InlineData(EstadoPantallaInicio.ConLecturas, false)]
    [InlineData(EstadoPantallaInicio.ReciboSinLecturas, true)]
    [InlineData(EstadoPantallaInicio.SinRecibo, true)]
    [InlineData(EstadoPantallaInicio.FechaFutura, true)]
    public void ReciboPorLlegar_SoloConLecturas(EstadoPantallaInicio estado, bool periodoTerminado)
    {
        // Sin lecturas, "Si sigues igual…" ya habla del recibo anterior; con el periodo en curso no falta nada.
        Assert.False(EstadoInicio.ReciboPorLlegar(estado, periodoTerminado));
    }
}
