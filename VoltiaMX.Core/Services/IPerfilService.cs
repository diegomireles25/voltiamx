using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface IPerfilService
{
    /// <summary>
    /// Perfil único con sus usos, módulos y objetivo; null si todavía no se hace el onboarding.
    /// </summary>
    Task<PerfilUsuario?> ObtenerPerfilAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Módulos activos en el orden de <see cref="ModuloApp"/>.
    /// </summary>
    Task<IReadOnlyList<ModuloApp>> ObtenerModulosActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Primer lugar real (no demo) con su tarifa; sirve para precargar el onboarding al repetirlo.
    /// </summary>
    Task<Lugar?> ObtenerLugarPrincipalAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda las respuestas del onboarding: perfil, usos, módulos, objetivo y el lugar principal
    /// (con el periodo del recibo si se capturó). Repetir el onboarding actualiza sin duplicar.
    /// </summary>
    Task GuardarOnboardingAsync(DatosOnboarding datos, CancellationToken cancellationToken = default);

    Task CambiarModoAvanzadoAsync(bool modoAvanzado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tope por recibo (sin IVA ni DAP). Crea el objetivo "no pasarme de mi presupuesto" si no hay
    /// ninguno; si ya hay otro, solo cambia el monto. Null quita el tope. Acepta de 0 a 1,000,000 (sin 0).
    /// </summary>
    Task GuardarPresupuestoAsync(decimal? monto, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca el onboarding como pendiente para repetirlo; no borra datos.
    /// </summary>
    Task ReiniciarOnboardingAsync(CancellationToken cancellationToken = default);
}
