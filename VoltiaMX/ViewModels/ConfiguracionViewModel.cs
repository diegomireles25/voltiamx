using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Configuración: modo simple/avanzado, repetir el onboarding y datos de demo.
/// </summary>
public sealed partial class ConfiguracionViewModel(
    IPerfilService perfiles,
    IDatosDemoService datosDemo,
    INavegacionShell navegacion) : ObservableObject
{
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
        ModoAvanzado = (await perfiles.ObtenerPerfilAsync())?.ModoAvanzado == true;
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
}
