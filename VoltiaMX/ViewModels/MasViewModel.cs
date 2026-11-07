using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Una fila de "Herramientas avanzadas" de Configuración que abre su pantalla.
/// </summary>
public sealed record FilaMas(string Titulo, string Descripcion, string TextoBoton, IRelayCommand Abrir);

/// <summary>
/// Configuración (la página que antes se llamaba Más; la clase conserva el nombre). Fase 9a: tu tarifa, avisos,
/// respaldo, ayuda, datos de demostración, volver a contestar y el modo avanzado; con el modo avanzado activo,
/// el bloque de herramientas avanzadas (reportes, meta, simulador solar e inquilinos).
/// </summary>
public sealed partial class MasViewModel(
    IPerfilService perfiles,
    IDatosDemoService datosDemo,
    INavegacionShell navegacion,
    ProgramadorAvisos avisos,
    IRespaldoService respaldo,
    IDialogos dialogos,
    IAplicacion aplicacion,
    ITextos textos) : ObservableObject
{
    /// <summary>
    /// Herramientas avanzadas: solo con el modo avanzado, en este orden y solo las de módulos activos.
    /// </summary>
    public ObservableCollection<FilaMas> Herramientas { get; } = [];

    private static readonly ModuloApp[] OrdenHerramientas = [ModuloApp.Reportes, ModuloApp.Metas, ModuloApp.Solar, ModuloApp.Inquilinos];

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarHerramientas))]
    public partial bool ModoAvanzado { get; set; }

    /// <summary>
    /// La fila "Modo avanzado" y la galería de diseño: ocultas en el modo presentación (<see cref="OpcionesDePresentacion"/>).
    /// </summary>
    public bool MostrarModoAvanzado => !OpcionesDePresentacion.ModoPresentacion;

    public bool MostrarGaleria => EsDebug && !OpcionesDePresentacion.ModoPresentacion;

    /// <summary>
    /// "HERRAMIENTAS AVANZADAS": con el modo avanzado y fuera del modo presentación.
    /// </summary>
    public bool MostrarHerramientas => ModoAvanzado && !OpcionesDePresentacion.ModoPresentacion;

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

        await CargarHerramientasAsync();
        _cargando = false;
    }

    // Reportes siempre está en el bloque avanzado (como antes en Más); Metas, Solar e Inquilinos si su módulo
    // está activo.
    private async Task CargarHerramientasAsync()
    {
        Herramientas.Clear();
        if (!ModoAvanzado)
        {
            return;
        }

        var activos = await perfiles.ObtenerModulosActivosAsync();
        foreach (var modulo in OrdenHerramientas.Where(m => m == ModuloApp.Reportes || activos.Contains(m)))
        {
            IRelayCommand abrir = modulo == ModuloApp.Reportes
                ? AbrirReportesCommand
                : new RelayCommand(() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(modulo)));
            Herramientas.Add(new FilaMas(textos[$"Mas_Modulo_{modulo}_Titulo"], textos[$"Mas_Modulo_{modulo}_Texto"], textos["Mas_Abrir"], abrir));
        }
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
        await CargarHerramientasAsync();
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

    // ---- Borrar mis datos y empezar de nuevo (Fase 9b, D8) ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BorrarDatosCommand))]
    public partial bool Borrando { get; set; }

    [ObservableProperty] public partial string ErrorBorrado { get; set; } = string.Empty;

    private bool PuedeBorrar() => !Borrando;

    /// <summary>
    /// Pregunta, guarda una copia previa en Respaldos (si falla, no borra nada), borra los datos y conserva las
    /// tarifas; después solo queda cerrar y volver a abrir la app, que pide el primer uso.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeBorrar))]
    private async Task BorrarDatosAsync()
    {
        ErrorBorrado = string.Empty;
        if (!await dialogos.ConfirmarAsync(
                textos["Mas_BorrarTitulo/Text"],
                textos.Formato("Mas_BorrarConfirmarTexto", respaldo.CarpetaRespaldos),
                textos["Mas_BorrarConfirmar"],
                textos["Respaldo_Cancelar"]))
        {
            return;
        }

        Borrando = true;
        ResultadoBorrado resultado;
        try
        {
            await aplicacion.PausarTareasAsync();
            resultado = await respaldo.BorrarDatosAsync();
        }
        finally
        {
            Borrando = false;
        }

        if (resultado.Error is { } error)
        {
            aplicacion.ReanudarTareas();
            var motivo = textos[$"Respaldo_Error_{error}"];
            ErrorBorrado = resultado.CopiaPrevia is null
                ? textos.Formato("Mas_BorrarSinCopia", motivo)
                : textos.Formato("Mas_BorrarFallo", motivo, resultado.CopiaPrevia);
            return;
        }

        await dialogos.AvisarAsync(textos["Mas_BorradoTitulo"], textos["Mas_BorradoTexto"], textos["Respaldo_CerrarApp"]);
        aplicacion.Cerrar();
    }

    [RelayCommand]
    private void AbrirReportes()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Reportes));

    [RelayCommand]
    private void AbrirRespaldo()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Respaldo));

    [RelayCommand]
    private void AbrirAyuda()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Ayuda));

    [RelayCommand]
    private void AbrirTarifas()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Tarifas));
}
