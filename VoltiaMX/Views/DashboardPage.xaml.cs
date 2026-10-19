using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            ViewModel = App.Servicios.GetRequiredService<DashboardViewModel>();
            this.InitializeComponent();
        }

        public DashboardViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }
    }
}
