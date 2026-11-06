using VoltiaMX.ViewModels;

namespace VoltiaMX.Views
{
    /// <summary>
    /// El formulario común para agregar un recibo (Fase 9c, D1). Solo bindings; la lógica vive en
    /// <see cref="AgregarReciboViewModel"/>.
    /// </summary>
    public sealed partial class FormularioRecibo : UserControl
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
            nameof(ViewModel), typeof(AgregarReciboViewModel), typeof(FormularioRecibo), new PropertyMetadata(null, AlCambiarViewModel));

        public FormularioRecibo()
        {
            this.InitializeComponent();
        }

        public AgregarReciboViewModel ViewModel
        {
            get => (AgregarReciboViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        private static void AlCambiarViewModel(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var formulario = (FormularioRecibo)d;
            formulario.Bindings.Update();
#if DEBUG
            formulario.MostrarRutaDePrueba();
#endif
        }

#if DEBUG
        // Solo en Debug, para pruebas automatizadas sin el selector de archivos (como en Escanear recibo). No
        // existe en Release, por eso sus textos no están en Resources.resw.
        private void MostrarRutaDePrueba()
        {
            if (ViewModel is not { } vm)
            {
                return;
            }

            CampoRutaPrueba.Header = "Ruta del archivo (solo pruebas)";
            CampoRutaPrueba.Text = vm.RutaPrueba;
            CampoRutaPrueba.TextChanged += (_, _) => vm.RutaPrueba = CampoRutaPrueba.Text;
            BotonRutaPrueba.Content = "Leer ruta";
            BotonRutaPrueba.Command = vm.LeerRutaCommand;
            ZonaRutaPrueba.Visibility = Visibility.Visible;
        }
#endif
    }
}
