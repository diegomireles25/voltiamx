using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class AgregarReciboPage : Page
    {
        public AgregarReciboPage()
        {
            ViewModel = App.Servicios.GetRequiredService<AgregarReciboPaginaViewModel>();
            this.InitializeComponent();
        }

        public AgregarReciboPaginaViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync(e.Parameter as string);
        }
    }
}
