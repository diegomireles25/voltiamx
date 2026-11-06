using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.Views.Onboarding
{
    public sealed partial class ReciboPage : Page
    {
        public ReciboPage()
        {
            this.InitializeComponent();
        }

        public OnboardingViewModel ViewModel { get; private set; } = null!;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel = (OnboardingViewModel)e.Parameter;
            base.OnNavigatedTo(e);
#if DEBUG
            MostrarRutaDePrueba();
#endif
        }

#if DEBUG
        // Solo en Debug, para pruebas automatizadas sin el selector de archivos (como en Escanear recibo). No
        // existe en Release, por eso sus textos no están en Resources.resw.
        private void MostrarRutaDePrueba()
        {
            CampoRutaPrueba.Header = "Ruta del archivo (solo pruebas)";
            CampoRutaPrueba.Text = ViewModel.RutaPrueba;
            CampoRutaPrueba.TextChanged += (_, _) => ViewModel.RutaPrueba = CampoRutaPrueba.Text;
            BotonRutaPrueba.Content = "Leer ruta";
            BotonRutaPrueba.Command = ViewModel.LeerRutaCommand;
            ZonaRutaPrueba.Visibility = Visibility.Visible;
        }
#endif
    }
}
