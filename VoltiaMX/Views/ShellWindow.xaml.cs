using Microsoft.Extensions.DependencyInjection;
using Microsoft.Windows.ApplicationModel.Resources;
using VoltiaMX.Helpers;
using VoltiaMX.Services;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    public sealed partial class ShellWindow : Window
    {
        public ShellWindow()
        {
            ViewModel = App.Servicios.GetRequiredService<ShellViewModel>();
            this.InitializeComponent();
            Title = new ResourceLoader().GetString("Ventana_Titulo");
            EstiloVentana.Aplicar(this, BarraTitulo);

            App.Servicios.GetRequiredService<NavegacionShell>().Marco = Contenido;
            _ = ViewModel.IniciarAsync();
        }

        public ShellViewModel ViewModel { get; }
    }
}
