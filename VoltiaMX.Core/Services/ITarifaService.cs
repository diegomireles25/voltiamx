using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface ITarifaService
{
    /// <summary>
    /// Tarifas guardadas (sin rangos), ordenadas por nombre, para elegirlas en pantalla.
    /// </summary>
    Task<IReadOnlyList<Tarifa>> ObtenerTarifasAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tarifas con su número de rangos y los lugares que las usan, ordenadas por nombre.
    /// </summary>
    Task<IReadOnlyList<TarifaConUso>> ListarConUsoAsync(CancellationToken cancellationToken = default);

    Task<TarifaEditable?> ObtenerParaEditarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea o actualiza. Con errores de validación no guarda nada. Los rangos se reemplazan completos
    /// (Orden 1..N) en una sola transacción.
    /// </summary>
    Task<ResultadoGuardarTarifa> GuardarAsync(TarifaEditable tarifa, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copia con nombre "X (copia)", "X (copia 2)"... Devuelve el id nuevo o null si no existe.
    /// </summary>
    Task<int?> DuplicarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// No borra una tarifa que use algún lugar ni la última que queda.
    /// </summary>
    Task<ResultadoEliminarTarifa> EliminarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asigna la tarifa al lugar y la da por confirmada. False si el lugar o la tarifa no existen.
    /// </summary>
    Task<bool> AsignarALugarAsync(int lugarId, int tarifaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuántos periodos cerrados del lugar se recalcularían y cuántos se saltan por tener monto de recibo.
    /// </summary>
    Task<ConteoRecalculo> ContarParaRecalcularAsync(int lugarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recalcula Periodo.Monto de los periodos cerrados con la tarifa actual del lugar. Nunca toca un
    /// periodo con MontoRecibo ni los kWh.
    /// </summary>
    Task<ResultadoRecalculo> RecalcularPeriodosAsync(int lugarId, CancellationToken cancellationToken = default);
}

/// <param name="ARecalcular">Periodos sin monto de recibo.</param>
/// <param name="ConMontoDeRecibo">Periodos que se saltan porque tienen monto de recibo.</param>
public sealed record ConteoRecalculo(int ARecalcular, int ConMontoDeRecibo);

/// <param name="Recalculados">Periodos revisados (sin monto de recibo).</param>
/// <param name="Cambiados">De esos, cuántos cambiaron de monto.</param>
/// <param name="SaltadosPorRecibo">Periodos que no se tocaron por tener monto de recibo.</param>
public sealed record ResultadoRecalculo(int Recalculados, int Cambiados, int SaltadosPorRecibo);
