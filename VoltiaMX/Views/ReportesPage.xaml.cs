using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class ReportesPage : Page
    {
        public ReportesPage()
        {
            ViewModel = App.Servicios.GetRequiredService<ReportesViewModel>();
            this.InitializeComponent();
#if DEBUG
            AgregarCarpetaDePrueba();
#endif
        }

        public ReportesViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
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
