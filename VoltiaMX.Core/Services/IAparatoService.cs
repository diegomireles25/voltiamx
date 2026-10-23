using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface IAparatoService
{
    /// <summary>
    /// Aparatos del lugar, ordenados por nombre.
    /// </summary>
    Task<IReadOnlyList<Aparato>> ListarAsync(int lugarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea o edita. Si hay errores de validación los devuelve sin guardar.
    /// </summary>
    Task<ResultadoGuardarAparato> GuardarAsync(AparatoEditable aparato, CancellationToken cancellationToken = default);

    /// <summary>
    /// False si el aparato ya no existía.
    /// </summary>
    Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valores típicos para empezar a capturar (de ejemplo, no de la etiqueta del aparato).
    /// </summary>
    IReadOnlyList<PlantillaAparato> PlantillasDeReferencia();
}
