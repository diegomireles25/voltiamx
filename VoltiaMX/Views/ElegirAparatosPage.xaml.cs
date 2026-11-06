using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class ElegirAparatosPage : Page
    {
        public ElegirAparatosPage()
        {
            ViewModel = App.Servicios.GetRequiredService<ElegirAparatosViewModel>();
            this.InitializeComponent();
        }

        public ElegirAparatosViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync(e.Parameter as string);
        }
    }
}
