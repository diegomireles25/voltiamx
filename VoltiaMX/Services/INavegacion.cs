using VoltiaMX.ViewModels;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.Services;

/// <summary>
/// Navegación entre los pasos del onboarding. La implementación conoce el Frame y las páginas;
/// el ViewModel solo dice a qué paso ir.
/// </summary>
public interface INavegacionOnboarding
{
    void IrA(PasoOnboarding paso, OnboardingViewModel viewModel);
}

/// <summary>
/// Navegación del Shell a partir de los elementos generados por los módulos activos.
/// </summary>
public interface INavegacionShell
{
    void Navegar(ElementoNavegacion elemento);

    /// <summary>
    /// Galería del sistema de diseño (solo se ofrece en Debug).
    /// </summary>
    void AbrirGaleriaDeDiseno();
}
