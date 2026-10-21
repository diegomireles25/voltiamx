using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;
using VirtualKey = Windows.System.VirtualKey;

namespace VoltiaMX.Views
{
    public sealed partial class PresupuestoPage : Page
    {
        public PresupuestoPage()
        {
            ViewModel = App.Servicios.GetRequiredService<PresupuestoViewModel>();
            this.InitializeComponent();
        }

        public PresupuestoViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }

        // Enter guarda. Va en KeyUp porque el NumberBox confirma su valor al bajar la tecla.
        private void AlSoltarTeclaEnMonto(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ViewModel.GuardarCommand.CanExecute(null))
            {
                ViewModel.GuardarCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
