using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Recibo capturado a mano en el onboarding. <paramref name="LecturaMedidor"/> es la "lectura actual"
/// del recibo: si viene, sirve de base real para el siguiente periodo.
/// </summary>
public sealed record ReciboCapturado(
    DateOnly FechaCorte, int MesesDelPeriodo, decimal Kwh, decimal Monto, decimal? LecturaMedidor = null);

/// <summary>
/// Respuestas del onboarding. Todos los pasos se pueden omitir, así que todo es opcional.
/// </summary>
public sealed record DatosOnboarding
{
    public IReadOnlyCollection<UsoApp> Usos { get; init; } = [];

    public string? NombreLugar { get; init; }

    public string? Estado { get; init; }

    public string? Municipio { get; init; }

    /// <summary>
    /// Tarifa elegida; null si la persona no sabe su tarifa o se saltó el paso.
    /// </summary>
    public int? TarifaId { get; init; }

    public ReciboCapturado? Recibo { get; init; }

    public TipoObjetivo? Objetivo { get; init; }

    public decimal? PresupuestoMax { get; init; }

    public bool NotificacionesActivas { get; init; }
}
