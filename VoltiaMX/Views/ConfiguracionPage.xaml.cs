using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class ConfiguracionPage : Page
    {
        public ConfiguracionPage()
        {
            ViewModel = App.Servicios.GetRequiredService<ConfiguracionViewModel>();
            this.InitializeComponent();
        }

        public ConfiguracionViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }
    }
}
