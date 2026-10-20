using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;
using VoltiaMX.ViewModels.Onboarding;
using Windows.UI.ViewManagement;
using VirtualKey = Windows.System.VirtualKey;

namespace VoltiaMX.Views.Onboarding
{
    /// <summary>
    /// Bienvenida con la escena 3D decorativa (Assets/Escena3D en WebView2). El código de aquí es
    /// solo de presentación: arrancar la escena, recibir su aviso y hacer aparecer "Empezar" con los rasgos.
    /// </summary>
    public sealed partial class BienvenidaPage : Page
    {
        private const string HostEscena = "appassets.voltiamx";
        private const string PaginaEscena = "https://" + HostEscena + "/index.html";

        // Si la escena no avisa en este tiempo, "Empezar" aparece de todos modos.
        private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(4);

        // Evita que soltar un archivo sobre la escena navegue hacia él.
        private const string BloquearArrastre =
            "window.addEventListener('dragover', e => e.preventDefault());" +
            "window.addEventListener('drop', e => e.preventDefault());";

        private DispatcherQueueTimer? _espera;
        private bool _accionVisible;

        public BienvenidaPage()
        {
            this.InitializeComponent();
            Loaded += AlCargar;
            Unloaded += AlDescargar;
            PreviewKeyDown += AlPresionarTecla;
        }

        public OnboardingViewModel ViewModel { get; private set; } = null!;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel = (OnboardingViewModel)e.Parameter;
            base.OnNavigatedTo(e);
        }

        private async void AlCargar(object sender, RoutedEventArgs e)
        {
            // El foco de teclado empieza en "Empezar" (programático: sin anillo hasta usar el teclado).
            BotonEmpezar.Focus(FocusState.Programmatic);

            _espera = DispatcherQueue.CreateTimer();
            _espera.Interval = EsperaMaxima;
            _espera.IsRepeating = false;
            _espera.Tick += (_, _) => MostrarAccion(conFundido: false);
            _espera.Start();

            try
            {
                await IniciarEscenaAsync();
            }
            catch (Exception)
            {
                // Sin WebView2 (o si falla al iniciar) queda el lienzo con el titular y el botón.
                MostrarSinEscena();
            }
        }

        private async Task IniciarEscenaAsync()
        {
            Escena.DefaultBackgroundColor = Windows.UI.Color.FromArgb(255, 0xE5, 0xE5, 0xE5);

            var datos = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoltiaMX", "WebView2");
            var entorno = await CoreWebView2Environment.CreateWithOptionsAsync(null, datos, null);
            await Escena.EnsureCoreWebView2Async(entorno);

            var core = Escena.CoreWebView2;
            var ajustes = core.Settings;
            ajustes.AreDefaultContextMenusEnabled = false;
            ajustes.IsZoomControlEnabled = false;
            ajustes.IsPinchZoomEnabled = false;
            ajustes.IsSwipeNavigationEnabled = false;
            ajustes.AreBrowserAcceleratorKeysEnabled = false;
            ajustes.IsStatusBarEnabled = false;
#if DEBUG
            ajustes.AreDevToolsEnabled = true;
#else
            ajustes.AreDevToolsEnabled = false;
#endif

            core.SetVirtualHostNameToFolderMapping(
                HostEscena,
                Path.Combine(AppContext.BaseDirectory, "Assets", "Escena3D"),
                CoreWebView2HostResourceAccessKind.Deny);
            core.NavigationStarting += AlIniciarNavegacion;
            core.NewWindowRequested += AlPedirVentana;
            core.WebMessageReceived += AlRecibirMensaje;
            await core.AddScriptToExecuteOnDocumentCreatedAsync(BloquearArrastre);

            core.Navigate(PaginaEscena);
        }

        // La escena solo puede mostrar su propia página: nada de navegar a archivos soltados o enlaces.
        private void AlIniciarNavegacion(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs args)
        {
            if (!args.Uri.StartsWith($"https://{HostEscena}/", StringComparison.OrdinalIgnoreCase))
            {
                args.Cancel = true;
            }
        }

        private void AlPedirVentana(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
            => args.Handled = true;

        private void AlRecibirMensaje(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            switch (args.TryGetWebMessageAsString())
            {
                case "escena-lista":
                    MostrarAccion(conFundido: true);
                    break;
                case "escena-error":
                    MostrarSinEscena();
                    break;
            }
        }

        // Quien usa el teclado no espera la animación.
        private void AlPresionarTecla(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Tab or VirtualKey.Enter or VirtualKey.Space)
            {
                MostrarAccion(conFundido: false);
            }
        }

        private void MostrarSinEscena()
        {
            Escena.Visibility = Visibility.Collapsed;
            MostrarAccion(conFundido: false);
        }

        private void MostrarAccion(bool conFundido)
        {
            if (_accionVisible)
            {
                return;
            }

            _accionVisible = true;
            _espera?.Stop();

            if (conFundido && new UISettings().AnimationsEnabled)
            {
                AparecerAccion.Begin();
            }
            else
            {
                AparecerAccion.Stop();
                Accion.Opacity = 1;
            }
        }

        // Libera GPU y memoria de la escena al salir de la página.
        private void AlDescargar(object sender, RoutedEventArgs e)
        {
            _espera?.Stop();
            if (Escena.CoreWebView2 is { } core)
            {
                core.NavigationStarting -= AlIniciarNavegacion;
                core.NewWindowRequested -= AlPedirVentana;
                core.WebMessageReceived -= AlRecibirMensaje;
            }

            Escena.Close();
        }
    }
}
