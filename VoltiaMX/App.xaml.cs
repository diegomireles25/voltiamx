using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Navigation;

namespace VoltiaMX
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private MainWindow? window;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            window ??= new MainWindow();

            if (window.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }

            window.Contenido.NavigationFailed += OnNavigationFailed;
#if DEBUG
            // Galería del sistema de diseño: solo existe como pantalla en compilaciones Debug.
            _ = window.Contenido.Navigate(typeof(DesignGalleryPage), e.Arguments);
#else
            _ = window.Contenido.Navigate(typeof(MainPage), e.Arguments);
#endif
            window.Activate();
        }

        /// <summary>
        /// Invoked when Navigation to a certain page fails
        /// </summary>
        /// <param name="sender">The Frame which failed navigation</param>
        /// <param name="e">Details about the navigation failure</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }
    }
}
