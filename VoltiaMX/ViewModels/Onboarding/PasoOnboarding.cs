namespace VoltiaMX.ViewModels.Onboarding;

/// <summary>
/// Pasos del onboarding en orden. Los tres del medio son las preguntas numeradas (desde la Fase 7a ya no
/// hay pasos de objetivo ni de presupuesto).
/// </summary>
public enum PasoOnboarding
{
    Bienvenida,
    Uso,
    Ubicacion,
    Recibo,
    Listo
}

/// <summary>
/// Tarifa elegible en el paso de ubicación.
/// </summary>
public sealed record OpcionTarifa(int Id, string Nombre)
{
    public override string ToString() => Nombre;
}

/// <summary>
/// Renglón del resumen de ListoPage.
/// </summary>
public sealed record FilaResumen(string Etiqueta, string Valor);
