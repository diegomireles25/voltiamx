using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface IMetaService
{
    Task<Meta?> ObtenerAsync(int lugarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea o cambia la meta del lugar y la deja activa. Si es nueva o estaba inactiva, empieza
    /// <paramref name="hoy"/>; si solo cambia el porcentaje, conserva su fecha de inicio.
    /// </summary>
    Task<ResultadoGuardarMeta> GuardarAsync(int lugarId, int porcentaje, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// False si el lugar no tenía meta.
    /// </summary>
    Task<bool> DesactivarAsync(int lugarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Progreso del periodo actual, historial y racha. Null si el lugar no tiene meta.
    /// </summary>
    Task<ProgresoMeta?> ObtenerProgresoAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default);
}
