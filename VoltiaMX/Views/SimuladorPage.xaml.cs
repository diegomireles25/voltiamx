using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class SimuladorPage : Page
    {
        public SimuladorPage()
        {
            ViewModel = App.Servicios.GetRequiredService<SimuladorViewModel>();
            this.InitializeComponent();
        }

        public SimuladorViewModel ViewModel { get; }

        // El parámetro es el aparato con el que se abre (enlace "Simular" de su fila), o null.
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync(e.Parameter as int?);
        }
    }
}
