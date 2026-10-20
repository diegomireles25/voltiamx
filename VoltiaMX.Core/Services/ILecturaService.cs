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

    /// <summary>
    /// Las últimas <paramref name="cantidad"/> lecturas, de la más reciente a la más antigua.
    /// </summary>
    Task<IReadOnlyList<LecturaListada>> ListarAsync(int lugarId, int cantidad, CancellationToken cancellationToken = default);

    /// <summary>
    /// El lugar que muestra el Dashboard (ver <see cref="LugarPrincipal"/>), o null si no hay lugares.
    /// </summary>
    Task<LugarDeLecturas?> ObtenerLugarPrincipalAsync(CancellationToken cancellationToken = default);
}
