using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class SolarPage : Page
    {
        public SolarPage()
        {
            ViewModel = App.Servicios.GetRequiredService<SolarViewModel>();
            this.InitializeComponent();
#if DEBUG
            AgregarCarpetaDePrueba();
#endif
        }

        public SolarViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync(e.Parameter as string);
        }

#if DEBUG
        // Solo en Debug, para pruebas automatizadas sin el selector de archivos. No existe en Release,
        // por eso su texto no está en Resources.resw.
        private void AgregarCarpetaDePrueba()
        {
            var carpeta = new TextBox
            {
                Header = "Carpeta de salida (solo pruebas)",
                Text = ViewModel.CarpetaPrueba,
                Style = (Style)Application.Current.Resources["InputStyle"],
            };
            carpeta.TextChanged += (_, _) => ViewModel.CarpetaPrueba = carpeta.Text;
            ZonaDepuracion.Children.Add(carpeta);
        }
#endif
    }
}
