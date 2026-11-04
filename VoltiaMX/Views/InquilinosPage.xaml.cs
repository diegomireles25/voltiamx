using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;
using Windows.System;

namespace VoltiaMX.Views
{
    public sealed partial class InquilinosPage : Page
    {
        public InquilinosPage()
        {
            ViewModel = App.Servicios.GetRequiredService<InquilinosViewModel>();
            this.InitializeComponent();
#if DEBUG
            AgregarCarpetaDePrueba();
#endif
        }

        public InquilinosViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }

        // Enter guarda el inquilino.
        private void AlSoltarTeclaEnCampo(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            ViewModel.ValidarAhora();
            if (ViewModel.GuardarCommand.CanExecute(null))
            {
                ViewModel.GuardarCommand.Execute(null);
            }
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
