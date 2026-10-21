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
}
