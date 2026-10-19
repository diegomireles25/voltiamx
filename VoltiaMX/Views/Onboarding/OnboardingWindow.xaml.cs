using Microsoft.Windows.ApplicationModel.Resources;
using VoltiaMX.Helpers;

namespace VoltiaMX.Views.Onboarding
{
    public sealed partial class OnboardingWindow : Window
    {
        public OnboardingWindow()
        {
            this.InitializeComponent();
            Title = new ResourceLoader().GetString("Ventana_Titulo");
            EstiloVentana.Aplicar(this, BarraTitulo);
        }
    }
}
