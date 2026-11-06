namespace VoltiaMX.ViewModels.Onboarding;

/// <summary>
/// Pasos del onboarding. Desde la Fase 9b el flujo es Bienvenida, Recibo, Tarifa y Listo (el orden lo lleva
/// OnboardingViewModel); Uso y Ubicacion ya no se muestran, pero sus páginas siguen en el proyecto.
/// </summary>
public enum PasoOnboarding
{
    Bienvenida,
    Uso,
    Ubicacion,
    Recibo,
    Tarifa,
    Listo
}

/// <summary>
/// Tarifa elegible en el paso Tu tarifa.
/// </summary>
public sealed record OpcionTarifa(int Id, string Nombre)
{
    public override string ToString() => Nombre;
}

/// <summary>
/// Renglón del resumen de ListoPage.
/// </summary>
public sealed record FilaResumen(string Etiqueta, string Valor);
