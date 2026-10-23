using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class EscanearReciboPage : Page
    {
        public EscanearReciboPage()
        {
            ViewModel = App.Servicios.GetRequiredService<EscanearReciboViewModel>();
            this.InitializeComponent();
#if DEBUG
            AgregarLecturaPorRuta();
#endif
        }

        public EscanearReciboViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }

        // Lo leído no se conserva: al salir de la pantalla se descarta.
        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ViewModel.Descartar();
            base.OnNavigatedFrom(e);
        }

#if DEBUG
        // Solo en Debug, para pruebas automatizadas sin el selector de archivos. No existe en Release,
        // por eso sus textos no están en Resources.resw.
        private void AgregarLecturaPorRuta()
        {
            var ruta = new TextBox
            {
                Header = "Ruta del archivo (solo pruebas)",
                Style = (Style)Application.Current.Resources["InputStyle"],
            };
            ruta.TextChanged += (_, _) => ViewModel.RutaPrueba = ruta.Text;
            var leer = new Button
            {
                Content = "Leer ruta",
                Command = ViewModel.LeerRutaCommand,
                Style = (Style)Application.Current.Resources["GhostButtonStyle"],
            };
            ZonaDepuracion.Children.Add(ruta);
            ZonaDepuracion.Children.Add(leer);
        }
#endif
    }
}
