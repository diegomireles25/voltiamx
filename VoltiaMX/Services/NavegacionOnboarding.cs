using VoltiaMX.ViewModels.Onboarding;
using VoltiaMX.Views.Onboarding;

namespace VoltiaMX.Services;

/// <summary>
/// Relaciona cada paso con su página. OnboardingPage le entrega su Frame al cargarse.
/// </summary>
public sealed class NavegacionOnboarding : INavegacionOnboarding
{
    public Frame? Marco { get; set; }

    public void IrA(PasoOnboarding paso, OnboardingViewModel viewModel)
    {
        if (Marco is null)
        {
            return;
        }

        var pagina = paso switch
        {
            PasoOnboarding.Bienvenida => typeof(BienvenidaPage),
            PasoOnboarding.Uso => typeof(UsoPage),
            PasoOnboarding.Ubicacion => typeof(UbicacionPage),
            PasoOnboarding.Recibo => typeof(ReciboPage),
            PasoOnboarding.Tarifa => typeof(TarifaPage),
            _ => typeof(ListoPage),
        };

        // Cada página recibe el mismo ViewModel; el historial lo lleva el ViewModel, no el Frame.
        Marco.Navigate(pagina, viewModel);
        Marco.BackStack.Clear();
    }
}
