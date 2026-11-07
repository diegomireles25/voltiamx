namespace VoltiaMX.Core.Services;

/// <summary>
/// Resultado de un recibo por monto: el error, o los kWh aproximados, su monto con la tarifa y sus fechas.
/// </summary>
/// <remarks>Fase 9i: con un rechazo por otro recibo, <see cref="ChoqueInicio"/> y <see cref="ChoqueFin"/> son las fechas de ese
/// recibo (para decir con cuál choca).</remarks>
public sealed record ResultadoReciboPorMonto(
    ErrorReciboPorMonto? Error, decimal Kwh, decimal Monto, DateOnly Inicio, DateOnly Fin, DateOnly? ChoqueInicio = null, DateOnly? ChoqueFin = null)
{
    public static ResultadoReciboPorMonto Rechazado(
        ErrorReciboPorMonto error, DateOnly inicio = default, DateOnly fin = default, DateOnly? choqueInicio = null, DateOnly? choqueFin = null)
        => new(error, 0m, 0m, inicio, fin, choqueInicio, choqueFin);
}

public interface IRecibosService
{
    /// <summary>
    /// Último día de corte (según el día de corte del lugar) en o antes de <paramref name="hoy"/>; null si el lugar no existe.
    /// </summary>
    Task<DateOnly?> UltimoCorteAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo que se guardaría con <see cref="AgregarPorMontoAsync"/>, sin guardar nada (para la confirmación).
    /// </summary>
    Task<ResultadoReciboPorMonto> PrevisualizarPorMontoAsync(
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, int? mesesPorPeriodo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un recibo anterior por su total: un periodo aproximado (Estimado) con los kWh que caben en el monto,
    /// Monto = Total(kWh) y MontoRecibo = el total escrito. Sin fecha, el último corte. No acepta fechas futuras ni
    /// recibos que se crucen con otro. <paramref name="mesesPorPeriodo"/> (1 o 2) es la respuesta a "¿Cada cuánto te
    /// llega el recibo?" (Fase 9c, D3): solo cuenta si el lugar todavía no tiene recibos, y entonces se guarda en
    /// <c>Lugar.MesesPorPeriodo</c>; con recibos guardados se ignora.
    /// </summary>
    Task<ResultadoReciboPorMonto> AgregarPorMontoAsync(
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, int? mesesPorPeriodo = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Si el lugar ya tiene algún recibo (periodo) guardado; con recibos ya no se pregunta la periodicidad.
    /// </summary>
    Task<bool> TieneRecibosAsync(int lugarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Para el onboarding, cuando todavía no hay lugar: con la tarifa elegida (o la de por defecto si es null).
    /// </summary>
    Task<EstimacionPorMonto> EstimarSinLugarAsync(
        int? tarifaId, int meses, decimal total, decimal porcentaje, CancellationToken cancellationToken = default);
}
