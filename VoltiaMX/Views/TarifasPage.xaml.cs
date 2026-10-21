using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class TarifasPage : Page
    {
        public TarifasPage()
        {
            ViewModel = App.Servicios.GetRequiredService<TarifasViewModel>();
            this.InitializeComponent();
        }

        public TarifasViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }
    }
}
