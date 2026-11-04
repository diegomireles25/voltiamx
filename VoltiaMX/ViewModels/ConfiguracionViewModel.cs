using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Configuración: modo simple/avanzado, notificaciones, repetir el onboarding y datos de demo.
/// </summary>
public sealed partial class ConfiguracionViewModel(
    IPerfilService perfiles,
    IDatosDemoService datosDemo,
    INavegacionShell navegacion,
    ProgramadorAvisos avisos) : ObservableObject
{
    [ObservableProperty] public partial bool NotificacionesActivas { get; set; }

    /// <summary>
    /// Windows acepta notificaciones en esta instalación (la copia sin empaquetar, por ejemplo, no).
    /// </summary>
    public bool NotificacionesDisponibles => avisos.NotificacionesDisponibles;

    public bool NotificacionesNoDisponibles => !NotificacionesDisponibles;

    partial void OnNotificacionesActivasChanged(bool value)
    {
        if (!_cargando)
        {
            _ = CambiarNotificacionesAsync(value);
        }
    }

    private async Task CambiarNotificacionesAsync(bool activas)
    {
        await perfiles.CambiarNotificacionesAsync(activas);
        WeakReferenceMessenger.Default.Send(new EvaluarAvisosMensaje());
    }

    [RelayCommand(CanExecute = nameof(NotificacionesDisponibles))]
    private void EnviarPrueba() => avisos.EnviarPrueba();

    // Evita guardar el modo mientras se carga el valor inicial.
    private bool _cargando;

    [ObservableProperty] public partial bool ModoAvanzado { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CargarDemoCommand), nameof(QuitarDemoCommand))]
    public partial bool HayDemo { get; set; }

    /// <summary>
    /// La galería de diseño solo se ofrece en compilaciones Debug.
    /// </summary>
#if DEBUG
    public bool EsDebug => true;
#else
    public bool EsDebug => false;
#endif

    public async Task CargarAsync()
    {
        _cargando = true;
        var perfil = await perfiles.ObtenerPerfilAsync();
        ModoAvanzado = perfil?.ModoAvanzado == true;
        NotificacionesActivas = perfil?.NotificacionesActivas == true;
        HayDemo = await datosDemo.HayDemoAsync();
        _cargando = false;
    }

    partial void OnModoAvanzadoChanged(bool value)
    {
        if (!_cargando)
        {
            _ = CambiarModoAsync(value);
        }
    }

    private async Task CambiarModoAsync(bool modoAvanzado)
    {
        await perfiles.CambiarModoAvanzadoAsync(modoAvanzado);
        WeakReferenceMessenger.Default.Send(new ModulosCambiadosMensaje());
    }

    [RelayCommand]
    private async Task RepetirOnboardingAsync()
    {
        await perfiles.ReiniciarOnboardingAsync();
        WeakReferenceMessenger.Default.Send(new RepetirOnboardingMensaje());
    }

    [RelayCommand(CanExecute = nameof(PuedeCargarDemo))]
    private async Task CargarDemoAsync()
    {
        await datosDemo.CargarAsync(DateOnly.FromDateTime(DateTime.Today));
        HayDemo = true;
    }

    private bool PuedeCargarDemo() => !HayDemo;

    [RelayCommand(CanExecute = nameof(HayDemo))]
    private async Task QuitarDemoAsync()
    {
        await datosDemo.QuitarAsync();
        HayDemo = false;
    }

    [RelayCommand]
    private void AbrirGaleria() => navegacion.AbrirGaleriaDeDiseno();

    [RelayCommand]
    private void AbrirReportes()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Reportes));

    [RelayCommand]
    private void AbrirRespaldo()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Respaldo));

    [RelayCommand]
    private void AbrirTarifas()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Tarifas));
}
