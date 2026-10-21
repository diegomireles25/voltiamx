using System.Windows.Input;

namespace VoltiaMX.Views
{
    public sealed partial class AvisoDatosEjemplo : UserControl
    {
        public static readonly DependencyProperty ComandoProperty = DependencyProperty.Register(
            nameof(Comando), typeof(ICommand), typeof(AvisoDatosEjemplo), new PropertyMetadata(null));

        public AvisoDatosEjemplo()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Comando del enlace "Ir a Configuración" (el del ViewModel de la página).
        /// </summary>
        public ICommand? Comando
        {
            get => (ICommand?)GetValue(ComandoProperty);
            set => SetValue(ComandoProperty, value);
        }
    }
}
