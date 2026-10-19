using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface ITarifaService
{
    /// <summary>
    /// Tarifas guardadas (sin rangos), ordenadas por nombre, para elegirlas en pantalla.
    /// </summary>
    Task<IReadOnlyList<Tarifa>> ObtenerTarifasAsync(CancellationToken cancellationToken = default);
}
