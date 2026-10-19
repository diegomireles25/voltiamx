using Microsoft.UI.Xaml.Navigation;

namespace VoltiaMX.Views
{
    /// <summary>
    /// Recibe como parámetro de navegación el título del módulo.
    /// </summary>
    public sealed partial class ModuloPendientePage : Page
    {
        public ModuloPendientePage()
        {
            this.InitializeComponent();
        }

        public string Titulo { get; private set; } = string.Empty;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Titulo = e.Parameter as string ?? string.Empty;
            base.OnNavigatedTo(e);
        }
    }
}
