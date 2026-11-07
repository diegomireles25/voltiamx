using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
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
            Activated += AlActivarPorPrimeraVez;
        }

        // Al abrirse, WinUI le da el foco a la primera pestaña y la marca con el anillo de foco, como si se hubiera
        // llegado con el teclado. El foco se vuelve a poner con FocusState.Programmatic, que no muestra el anillo:
        // con Tab y las flechas aparece como siempre.
        private void AlActivarPorPrimeraVez(object sender, WindowActivatedEventArgs args)
        {
            Activated -= AlActivarPorPrimeraVez;
            DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
            {
                if (FocusManager.GetFocusedElement(Navegacion.XamlRoot) is NavigationViewItem item)
                {
                    item.Focus(FocusState.Programmatic);
                }
            });
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
