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

        // Tocar la pestaña ya seleccionada no cambia la selección: el ViewModel decide si vuelve a abrir su página
        // (por ejemplo "Más" mientras se ve una sección como Tarifas).
        private void AlInvocarElemento(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.InvokedItemContainer?.DataContext is ElementoNavegacion elemento)
            {
                ViewModel.AlTocarElemento(elemento);
            }
        }
    }
}
