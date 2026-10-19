namespace VoltiaMX.Core.Services;

/// <summary>
/// Datos de ejemplo para conocer la app sin capturar un recibo. Viven en un Lugar con
/// <c>EsDemo = true</c> para no mezclarse con los datos reales.
/// </summary>
public interface IDatosDemoService
{
    Task<bool> HayDemoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea el lugar de demo con doce meses de periodos y las lecturas del periodo actual
    /// hasta <paramref name="hoy"/>. Si ya existe, no hace nada.
    /// </summary>
    Task CargarAsync(DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra el lugar de demo; sus periodos y lecturas se borran en cascada.
    /// </summary>
    Task QuitarAsync(CancellationToken cancellationToken = default);
}
