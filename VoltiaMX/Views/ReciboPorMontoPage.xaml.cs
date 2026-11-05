using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class ReciboPorMontoPage : Page
    {
        public ReciboPorMontoPage()
        {
            ViewModel = App.Servicios.GetRequiredService<ReciboPorMontoViewModel>();
            this.InitializeComponent();
        }

        public ReciboPorMontoViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }
    }
}
