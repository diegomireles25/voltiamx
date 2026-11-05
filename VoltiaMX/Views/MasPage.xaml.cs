using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class MasPage : Page
    {
        public MasPage()
        {
            ViewModel = App.Servicios.GetRequiredService<MasViewModel>();
            this.InitializeComponent();
        }

        public MasViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }
    }
}
