using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using VoltiaMX.ViewModels;
using VirtualKey = Windows.System.VirtualKey;

namespace VoltiaMX.Views
{
    public sealed partial class AparatosPage : Page
    {
        public AparatosPage()
        {
            ViewModel = App.Servicios.GetRequiredService<AparatosViewModel>();
            this.InitializeComponent();

            // Fase 9g (D5): el editor está arriba de la lista. Al abrirlo desde una fila de abajo ("Editar" o el nombre),
            // la página no se movía y parecía que no pasaba nada: se trae a la vista y se pone el foco en el nombre.
            ViewModel.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(AparatosViewModel.EnEdicion) && ViewModel.EnEdicion && IsLoaded)
                {
                    UpdateLayout();
                    Editor.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, AnimationDesired = false });
                    CampoNombre.Focus(FocusState.Programmatic);
                }
            };
        }

        public AparatosViewModel ViewModel { get; }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            await ViewModel.CargarAsync();
        }

        // Enter guarda. Va en KeyUp porque el NumberBox confirma su valor al bajar la tecla; se valida
        // en ese momento para no esperar los 300 ms de la validación en vivo.
        private void AlSoltarTeclaEnCampo(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != VirtualKey.Enter)
            {
                return;
            }

            ViewModel.ValidarAhora();
            if (ViewModel.GuardarCommand.CanExecute(null))
            {
                ViewModel.GuardarCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
