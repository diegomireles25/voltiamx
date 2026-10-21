using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class TarifaEditorPage : Page
    {
        public TarifaEditorPage()
        {
            ViewModel = App.Servicios.GetRequiredService<TarifaEditorViewModel>();
            this.InitializeComponent();
        }

        public TarifaEditorViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync(e.Parameter as int?);
        }
    }
}
