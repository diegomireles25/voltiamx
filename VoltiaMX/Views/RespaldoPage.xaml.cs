using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class RespaldoPage : Page
    {
        public RespaldoPage()
        {
            ViewModel = App.Servicios.GetRequiredService<RespaldoViewModel>();
            this.InitializeComponent();
#if DEBUG
            AgregarRutasDePrueba();
#endif
        }

        public RespaldoViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }

#if DEBUG
        // Solo en Debug, para pruebas automatizadas sin los selectores de archivos. No existen en Release,
        // por eso sus textos no están en Resources.resw.
        private void AgregarRutasDePrueba()
        {
            var estilo = (Style)Application.Current.Resources["InputStyle"];
            var carpeta = new TextBox { Header = "Carpeta de salida (solo pruebas)", Text = ViewModel.CarpetaPrueba, Style = estilo };
            carpeta.TextChanged += (_, _) => ViewModel.CarpetaPrueba = carpeta.Text;
            var respaldo = new TextBox { Header = "Ruta del respaldo (solo pruebas)", Text = ViewModel.RespaldoPrueba, Style = estilo };
            respaldo.TextChanged += (_, _) => ViewModel.RespaldoPrueba = respaldo.Text;
            ZonaDepuracion.Children.Add(carpeta);
            ZonaDepuracion.Children.Add(respaldo);
        }
#endif
    }
}
