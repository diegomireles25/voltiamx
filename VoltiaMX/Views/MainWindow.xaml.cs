using Microsoft.Windows.ApplicationModel.Resources;
using VoltiaMX.Helpers;

namespace VoltiaMX.Views
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            Title = new ResourceLoader().GetString("Ventana_Titulo");
            EstiloVentana.Aplicar(this, BarraTitulo);
        }
    }
}
