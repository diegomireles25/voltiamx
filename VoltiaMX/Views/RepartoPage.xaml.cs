using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class RepartoPage : Page
    {
        public RepartoPage()
        {
            ViewModel = App.Servicios.GetRequiredService<RepartoViewModel>();
            this.InitializeComponent();
#if DEBUG
            AgregarCarpetaDePrueba();
#endif
        }

        public RepartoViewModel ViewModel { get; }

        /// <summary>
        /// El parámetro es el Id del reparto guardado que se quiere ver; null = reparto nuevo.
        /// </summary>
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync(e.Parameter as int?);
        }

#if DEBUG
        // Solo en Debug, para pruebas automatizadas sin los selectores. No existe en Release,
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
