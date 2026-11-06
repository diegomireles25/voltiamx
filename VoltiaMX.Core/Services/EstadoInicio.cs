namespace VoltiaMX.Core.Services;

/// <summary>
/// Qué muestra Inicio (Fase 9b, D6).
/// </summary>
public enum EstadoPantallaInicio
{
    /// <summary>
    /// Sin recibos ni lecturas: las dos entradas para agregar el primer recibo.
    /// </summary>
    SinRecibo,

    /// <summary>
    /// Con recibo pero sin consumo medido en el recibo actual: "Si sigues igual que tu recibo anterior…".
    /// </summary>
    ReciboSinLecturas,

    /// <summary>
    /// Con consumo medido (o con lecturas sin recibo): Inicio como siempre.
    /// </summary>
    ConLecturas,

    /// <summary>
    /// La fecha de corte del último recibo todavía no llega: el recibo actual empezaría después de hoy.
    /// </summary>
    FechaFutura,
}

public static class EstadoInicio
{
    /// <param name="recibos">Recibos (periodos cerrados) del lugar.</param>
    /// <param name="lecturas">Lecturas del medidor del lugar.</param>
    /// <param name="inicioReciboActual">Primer día del recibo actual (el día después del último corte).</param>
    /// <param name="consumoMedido">Hay dos lecturas en el recibo actual: ya se sabe cuánto va.</param>
    public static EstadoPantallaInicio Evaluar(int recibos, int lecturas, DateOnly inicioReciboActual, bool consumoMedido, DateOnly hoy)
    {
        if (recibos == 0)
        {
            return lecturas == 0 ? EstadoPantallaInicio.SinRecibo : EstadoPantallaInicio.ConLecturas;
        }

        if (hoy < inicioReciboActual)
        {
            return EstadoPantallaInicio.FechaFutura;
        }

        return consumoMedido ? EstadoPantallaInicio.ConLecturas : EstadoPantallaInicio.ReciboSinLecturas;
    }

    /// <summary>
    /// Fase 9d (D5): con lecturas y el recibo actual ya terminado (sin la lectura de su día de corte), el recibo ya
    /// debió llegar: Inicio pide agregarlo en lugar de mostrar "DÍA n DE n".
    /// </summary>
    public static bool ReciboPorLlegar(EstadoPantallaInicio estado, bool periodoTerminado)
        => estado == EstadoPantallaInicio.ConLecturas && periodoTerminado;
}
