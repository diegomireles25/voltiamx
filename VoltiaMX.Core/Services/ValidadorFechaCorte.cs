namespace VoltiaMX.Core.Services;

public enum ErrorFechaCorte
{
    /// <summary>
    /// La fecha de corte es posterior a hoy: ese recibo todavía no llega.
    /// </summary>
    PosteriorAHoy,
}

/// <summary>
/// Regla de la fecha de corte de un recibo (Fase 9b, D5): no puede ser posterior a hoy; hoy mismo sí.
/// La usan el onboarding, el recibo por monto y el escáner.
/// </summary>
public static class ValidadorFechaCorte
{
    public static ErrorFechaCorte? Validar(DateOnly fechaCorte, DateOnly hoy)
        => fechaCorte > hoy ? ErrorFechaCorte.PosteriorAHoy : null;
}
