using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Una fila de Más que abre un módulo sin pestaña.
/// </summary>
public sealed record FilaMas(string Titulo, string Descripcion, string TextoBoton, IRelayCommand Abrir);

/// <summary>
/// Más: los módulos que no son principales y los ajustes (modo avanzado, notificaciones, demo, tarifas,
/// reportes, respaldo y repetir el onboarding).
/// </summary>
public sealed partial class MasViewModel(
    IPerfilService perfiles,
    IDatosDemoService datosDemo,
    INavegacionShell navegacion,
    ProgramadorAvisos avisos,
    ITextos textos) : ObservableObject
{
    /// <summary>
    /// Módulos sin pestaña. Reportes tiene su propia fila de ajustes, así que no se repite aquí.
    /// </summary>
    public ObservableCollection<FilaMas> Modulos { get; } = [];

    /// <summary>
    /// "Escanear recibo" guarda una lectura: solo con el módulo Lecturas activo.
    /// </summary>
    [ObservableProperty] public partial bool LecturasActiva { get; set; }

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

        var activos = await perfiles.ObtenerModulosActivosAsync();
        var menu = ModulosPrincipales.Calcular(activos, perfil?.Usos.Select(u => u.Uso).ToList() ?? []);
        Modulos.Clear();
        foreach (var modulo in menu.Secundarios.Where(m => m != ModuloApp.Reportes))
        {
            Modulos.Add(new FilaMas(
                textos[$"Mas_Modulo_{modulo}_Titulo"],
                textos[$"Mas_Modulo_{modulo}_Texto"],
                textos["Mas_Abrir"],
                new RelayCommand(() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(modulo)))));
        }

        LecturasActiva = activos.Contains(ModuloApp.Lecturas);
        _cargando = false;
    }

    [RelayCommand]
    private void AbrirEscanear()
        => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, SeccionesLecturas.Escanear));

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
