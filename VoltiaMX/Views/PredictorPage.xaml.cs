using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class PredictorPage : Page
    {
        public PredictorPage()
        {
            ViewModel = App.Servicios.GetRequiredService<PredictorViewModel>();
            this.InitializeComponent();
        }

        public PredictorViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }
    }
}
