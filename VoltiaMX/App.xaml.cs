using System.Reflection;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;
using VoltiaMX.ViewModels;
using VoltiaMX.ViewModels.Onboarding;
using VoltiaMX.Views.Onboarding;

namespace VoltiaMX
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        private readonly IHost _host;
        private Window? _ventana;

        /// <summary>
        /// Initializes the singleton application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            this.InitializeComponent();
            _host = CrearHost();
        }

        /// <summary>
        /// Contenedor de servicios para las vistas que crea XAML (ventanas y páginas).
        /// </summary>
        public static IServiceProvider Servicios => ((App)Current)._host.Services;

        /// <summary>
        /// Ventana abierta (onboarding o Shell); el selector de archivos necesita su handle.
        /// </summary>
        public static Window? VentanaPrincipal => ((App)Current)._ventana;

        /// <summary>
        /// Invoked when the application is launched normally by the end user.  Other entry points
        /// will be used such as when the application is launched to open a specific file.
        /// </summary>
        /// <param name="e">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            // Antes de migrar, una copia de la base si tiene datos y hay migraciones pendientes. Si no se
            // puede crear, se registra la causa (sin datos de la persona) y la app sigue.
            var copia = await Servicios.GetRequiredService<IRespaldoService>().CrearCopiaAntesDeMigrarAsync();
            if (copia.Estado == EstadoCopiaPrevia.Fallo)
            {
                RegistroDepuracion.Escribir($"[Respaldo] copia antes de migrar: {copia.Error}");
            }

            var fabrica = Servicios.GetRequiredService<IDbContextFactory<VoltiaDbContext>>();
            await using (var db = await fabrica.CreateDbContextAsync())
            {
                await new DbInicializador(db).InicializarAsync();
            }

            WeakReferenceMessenger.Default.Register<App, OnboardingTerminadoMensaje>(this, static (app, _) => app.Mostrar(new ShellWindow()));
            WeakReferenceMessenger.Default.Register<App, RepetirOnboardingMensaje>(this, static (app, _) => app.Mostrar(new OnboardingWindow()));

            // Avisos: se registran las notificaciones y se evalúan mientras la app está abierta.
            var programador = Servicios.GetRequiredService<ProgramadorAvisos>();
            if (AppInstance.GetCurrent().GetActivatedEventArgs() is { Kind: ExtendedActivationKind.AppNotification, Data: AppNotificationActivatedEventArgs desdeNotificacion })
            {
                // Se abrió la app tocando una notificación: al estar listo el Shell, abre su pantalla.
                programador.AbrirAlIniciar(Notificador.TipoDe(desdeNotificacion.Arguments));
            }

            var perfil = await Servicios.GetRequiredService<IPerfilService>().ObtenerPerfilAsync();
            Mostrar(perfil?.OnboardingCompletado == true ? new ShellWindow() : new OnboardingWindow());
            programador.Iniciar(DispatcherQueue.GetForCurrentThread());
        }

        /// <summary>
        /// Abre la ventana nueva maximizada y cierra la anterior.
        /// </summary>
        private void Mostrar(Window ventana)
        {
            if (ventana.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.Maximize();
            }

            var anterior = _ventana;
            _ventana = ventana;
            ventana.Activate();
            anterior?.Close();
        }

        private static IHost CrearHost()
        {
            var builder = Host.CreateApplicationBuilder();
            var servicios = builder.Services;

            // En MSIX, Windows redirige LocalApplicationData a la carpeta del paquete.
            var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoltiaMX");
            Directory.CreateDirectory(carpeta);
            var rutaBase = Path.Combine(carpeta, "voltia.db");
            servicios.AddDbContextFactory<VoltiaDbContext>(
                opciones => opciones.UseSqlite($"Data Source={rutaBase}"));
            servicios.AddSingleton(new OpcionesRespaldo(rutaBase, Path.Combine(carpeta, "Respaldos"), VersionDeLaApp()));

            // Core
            servicios.AddSingleton<ITarifaCalculator, TarifaCalculator>();
            servicios.AddSingleton<IProyeccionService, ProyeccionService>();
            servicios.AddSingleton<IDacService, DacService>();
            servicios.AddSingleton<IPresupuestoInversoService, PresupuestoInversoService>();
            servicios.AddSingleton<ITarifaService, TarifaService>();
            servicios.AddSingleton<IPerfilService, PerfilService>();
            servicios.AddSingleton<IDatosDemoService, DatosDemoService>();
            servicios.AddSingleton<IResumenDashboardService, ResumenDashboardService>();
            servicios.AddSingleton<ILecturaService, LecturaService>();
            servicios.AddSingleton<IHistorialService, HistorialService>();
            servicios.AddSingleton<IValidadorRecibo, ValidadorRecibo>();
            servicios.AddSingleton<IAvisoService, AvisoService>();
            servicios.AddSingleton<IReporteService, ReporteService>();
            servicios.AddSingleton<IAparatoService, AparatoService>();
            servicios.AddSingleton<ISimuladorAhorroService, SimuladorAhorroService>();
            servicios.AddSingleton<IMetaService, MetaService>();
            servicios.AddSingleton<IRecibosService, RecibosService>();
            servicios.AddSingleton<IInquilinoService, InquilinoService>();
            servicios.AddSingleton<ISimuladorSolarService, SimuladorSolarService>();
            servicios.AddSingleton<IRepartoService>(sp => new RepartoService(sp.GetRequiredService<IDbContextFactory<VoltiaDbContext>>()));
            servicios.AddSingleton<IRespaldoService>(sp => new RespaldoService(sp.GetRequiredService<OpcionesRespaldo>()));

            // Servicios de UI
            servicios.AddSingleton<ITextos, TextosRecursos>();
            servicios.AddSingleton<NavegacionOnboarding>();
            servicios.AddSingleton<INavegacionOnboarding>(sp => sp.GetRequiredService<NavegacionOnboarding>());
            servicios.AddSingleton<NavegacionShell>();
            servicios.AddSingleton<INavegacionShell>(sp => sp.GetRequiredService<NavegacionShell>());
            servicios.AddSingleton<IDialogos, Dialogos>();
            servicios.AddSingleton<IReciboOcr, ReciboOcr>();
            servicios.AddSingleton<ISelectorArchivos, SelectorArchivos>();
            servicios.AddSingleton<INotificador, Notificador>();
            servicios.AddSingleton<ProgramadorAvisos>();
            servicios.AddSingleton<IGuardadorArchivos, GuardadorArchivos>();
            servicios.AddSingleton(_ => FuentesDelReporte.Cargar());
            servicios.AddSingleton<IAplicacion, Aplicacion>();

            // ViewModels
            servicios.AddTransient<OnboardingViewModel>();
            servicios.AddTransient<ShellViewModel>();
            servicios.AddTransient<DashboardViewModel>();
            servicios.AddTransient<MasViewModel>();
            servicios.AddTransient<AyudaViewModel>();
            servicios.AddTransient<LecturasViewModel>();
            servicios.AddTransient<PredictorViewModel>();
            servicios.AddTransient<TarifasViewModel>();
            servicios.AddTransient<TarifaEditorViewModel>();
            servicios.AddTransient<AparatosViewModel>();
            servicios.AddTransient<SimuladorViewModel>();
            servicios.AddTransient<MetasViewModel>();
            servicios.AddTransient<EscanearReciboViewModel>();
            servicios.AddTransient<ReciboPorMontoViewModel>();
            servicios.AddTransient<AgregarReciboViewModel>();
            servicios.AddTransient<AgregarReciboPaginaViewModel>();
            servicios.AddTransient<ReportesViewModel>();
            servicios.AddTransient<RespaldoViewModel>();
            servicios.AddTransient<InquilinosViewModel>();
            servicios.AddTransient<RepartoViewModel>();
            servicios.AddTransient<SolarViewModel>();

            return builder.Build();
        }

        /// <summary>
        /// Versión del ensamblado (VoltiaMX.csproj), sin el identificador de compilación que agrega .NET tras "+".
        /// </summary>
        private static string VersionDeLaApp()
        {
            var version = typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(App).Assembly.GetName().Version?.ToString()
                ?? string.Empty;
            return version.Split('+')[0];
        }
    }
}
