using Microsoft.Extensions.DependencyInjection;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class AyudaPage : Page
    {
        public AyudaPage()
        {
            ViewModel = App.Servicios.GetRequiredService<AyudaViewModel>();
            this.InitializeComponent();
        }

        public AyudaViewModel ViewModel { get; }
    }
}
