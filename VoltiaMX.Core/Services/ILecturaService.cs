namespace VoltiaMX.Core.Services;

public interface ILecturaService
{
    /// <summary>
    /// Valida la lectura y dice qué periodos cerraría, sin guardar nada.
    /// </summary>
    Task<ResultadoLectura> PrevisualizarAsync(NuevaLectura entrada, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida y guarda la lectura junto con los periodos que cierra, todo o nada.
    /// </summary>
    Task<ResultadoLectura> GuardarAsync(NuevaLectura entrada, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra la lectura más reciente del lugar, salvo que sea el corte de un periodo cerrado.
    /// </summary>
    Task<ResultadoEliminacion> EliminarUltimaAsync(int lugarId, CancellationToken cancellationToken = default);
}
