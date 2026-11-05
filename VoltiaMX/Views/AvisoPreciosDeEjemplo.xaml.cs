using System.Windows.Input;

namespace VoltiaMX.Views
{
    public sealed partial class AvisoPreciosDeEjemplo : UserControl
    {
        public static readonly DependencyProperty ComandoProperty = DependencyProperty.Register(
            nameof(Comando), typeof(ICommand), typeof(AvisoPreciosDeEjemplo), new PropertyMetadata(null));

        public AvisoPreciosDeEjemplo()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Comando del enlace "Ir a Tarifas" (el del ViewModel de la página).
        /// </summary>
        public ICommand? Comando
        {
            get => (ICommand?)GetValue(ComandoProperty);
            set => SetValue(ComandoProperty, value);
        }
    }
}
