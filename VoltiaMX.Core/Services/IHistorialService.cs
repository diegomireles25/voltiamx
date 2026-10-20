namespace VoltiaMX.Core.Services;

public interface IHistorialService
{
    /// <summary>
    /// Historial del lugar a la fecha <paramref name="hoy"/>, o null si el lugar no existe.
    /// </summary>
    Task<HistorialLugar?> ObtenerAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default);
}
