namespace VoltiaMX.Core.Services;

/// <summary>
/// Resultado de un recibo por monto: el error, o los kWh aproximados, su monto con la tarifa y sus fechas.
/// </summary>
public sealed record ResultadoReciboPorMonto(ErrorReciboPorMonto? Error, decimal Kwh, decimal Monto, DateOnly Inicio, DateOnly Fin)
{
    public static ResultadoReciboPorMonto Rechazado(ErrorReciboPorMonto error, DateOnly inicio = default, DateOnly fin = default)
        => new(error, 0m, 0m, inicio, fin);
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
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega un recibo anterior por su total: un periodo aproximado (Estimado) con los kWh que caben en el monto,
    /// Monto = Total(kWh) y MontoRecibo = el total escrito. Sin fecha, el último corte. No acepta fechas futuras ni
    /// recibos que se crucen con otro.
    /// </summary>
    Task<ResultadoReciboPorMonto> AgregarPorMontoAsync(
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Para el onboarding, cuando todavía no hay lugar: con la tarifa elegida (o la de por defecto si es null).
    /// </summary>
    Task<EstimacionPorMonto> EstimarSinLugarAsync(
        int? tarifaId, int meses, decimal total, decimal porcentaje, CancellationToken cancellationToken = default);
}
