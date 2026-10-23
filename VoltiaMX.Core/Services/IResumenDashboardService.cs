namespace VoltiaMX.Core.Services;

public interface IResumenDashboardService
{
    /// <summary>
    /// Resumen del lugar principal: el primer lugar real con datos; si no hay, el de demo;
    /// si tampoco, el primer lugar real sin datos (para mostrar su tarifa). Null si no hay lugares.
    /// </summary>
    Task<ResumenDashboard?> ObtenerAsync(DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// El mismo resumen para un lugar dado. Null si no existe o no tiene tarifa.
    /// </summary>
    Task<ResumenDashboard?> ObtenerDeLugarAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Último periodo de cada propiedad en renta (sin la demo).
    /// </summary>
    Task<IReadOnlyList<ResumenPropiedad>> ObtenerPropiedadesAsync(CancellationToken cancellationToken = default);
}
