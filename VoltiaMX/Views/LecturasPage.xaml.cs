using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;
using VirtualKey = Windows.System.VirtualKey;

namespace VoltiaMX.Views
{
    public sealed partial class LecturasPage : Page
    {
        public LecturasPage()
        {
            ViewModel = App.Servicios.GetRequiredService<LecturasViewModel>();
            this.InitializeComponent();
        }

        public LecturasViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();

            // "Ver historial" del Dashboard abre la página en la sección HISTORIAL. Antes se termina de
            // medir el contenido recién cargado; si no, el ScrollViewer todavía no sabe dónde queda.
            if (e.Parameter as string == SeccionesLecturas.Historial)
            {
                UpdateLayout();
                SeccionHistorial.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, AnimationDesired = false });
            }
        }

        // Enter guarda. Va en KeyUp porque el NumberBox confirma su valor al bajar la tecla.
        private void AlSoltarTeclaEnCampo(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && ViewModel.GuardarCommand.CanExecute(null))
            {
                ViewModel.GuardarCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
