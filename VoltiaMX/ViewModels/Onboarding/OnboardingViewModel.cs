using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels.Onboarding;

/// <summary>
/// Estado y comandos del onboarding. Todos los pasos se pueden omitir; al terminar se guarda
/// el perfil con <see cref="IPerfilService"/> y se avisa a la app para abrir el Shell.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private const int PreguntasNumeradas = 3;

    private readonly IPerfilService perfiles;
    private readonly IDatosDemoService datosDemo;
    private readonly ITarifaService tarifas;
    private readonly ITextos textos;
    private readonly INavegacionOnboarding navegacion;

    public OnboardingViewModel(
        IPerfilService perfiles,
        IDatosDemoService datosDemo,
        ITarifaService tarifas,
        ITextos textos,
        INavegacionOnboarding navegacion)
    {
        this.perfiles = perfiles;
        this.datosDemo = datosDemo;
        this.tarifas = tarifas;
        this.textos = textos;
        this.navegacion = navegacion;

        // Las casillas y tarjetas empiezan sin marcar (null se vería como estado indeterminado).
        UsaCasa = UsaNegocio = UsaRentas = UsaSolar = false;
        Municipio = NombreLugar = string.Empty;
        NoSeMiTarifa = false;
        ReciboManual = ReciboSaltar = ReciboMensual = CargarDemo = false;
        ReciboBimestral = true;
        KwhRecibo = MontoRecibo = LecturaMedidorRecibo = double.NaN;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoPaso), nameof(MostrarIndicador), nameof(MostrarNavegacion))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    public partial PasoOnboarding PasoActual { get; set; }

    // Uso (selección múltiple)
    [ObservableProperty] public partial bool? UsaCasa { get; set; }
    [ObservableProperty] public partial bool? UsaNegocio { get; set; }
    [ObservableProperty] public partial bool? UsaRentas { get; set; }
    [ObservableProperty] public partial bool? UsaSolar { get; set; }

    // Ubicación
    [ObservableProperty] public partial object? EstadoSeleccionado { get; set; }
    [ObservableProperty] public partial string Municipio { get; set; }
    [ObservableProperty] public partial string NombreLugar { get; set; }
    [ObservableProperty] public partial object? TarifaSeleccionada { get; set; }
    [ObservableProperty] public partial bool? NoSeMiTarifa { get; set; }

    // Recibo
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReciboIncompleto))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    public partial bool? ReciboManual { get; set; }

    [ObservableProperty] public partial bool? ReciboSaltar { get; set; }
    [ObservableProperty] public partial bool? ReciboBimestral { get; set; }
    [ObservableProperty] public partial bool? ReciboMensual { get; set; }
    [ObservableProperty] public partial bool? CargarDemo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReciboIncompleto))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    public partial DateTimeOffset? FechaCorte { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReciboIncompleto))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    public partial double KwhRecibo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ReciboIncompleto))]
    [NotifyCanExecuteChangedFor(nameof(SiguienteCommand))]
    public partial double MontoRecibo { get; set; }

    /// <summary>
    /// "Lectura actual" del recibo (opcional): base real del siguiente periodo.
    /// </summary>
    [ObservableProperty] public partial double LecturaMedidorRecibo { get; set; }

    /// <summary>
    /// Las notificaciones empiezan apagadas en un perfil nuevo y se encienden desde Más. Al repetir el
    /// onboarding se conserva lo que el perfil ya tenía.
    /// </summary>
    private bool _notificaciones;

    // Al repetir el onboarding se devuelven tal cual (ya no se preguntan) para no borrar datos guardados.
    private TipoObjetivo? _objetivoGuardado;
    private decimal? _presupuestoGuardado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TerminarCommand))]
    public partial bool Guardando { get; set; }

    [ObservableProperty] public partial string? Error { get; set; }

    public IReadOnlyList<string> Estados { get; } = EstadosMexico.Todos;

    public ObservableCollection<OpcionTarifa> Tarifas { get; } = [];

    public ObservableCollection<FilaResumen> Resumen { get; } = [];

    public string TextoPaso => MostrarIndicador
        ? textos.Formato("Onboarding_Paso", (int)PasoActual, PreguntasNumeradas)
        : string.Empty;

    /// <summary>
    /// Indicador "PASO n/3" y barra Atrás/Siguiente: solo en las tres preguntas.
    /// </summary>
    public bool MostrarIndicador => PasoActual is > PasoOnboarding.Bienvenida and < PasoOnboarding.Listo;

    public bool MostrarNavegacion => MostrarIndicador;

    public bool ReciboIncompleto => ReciboManual == true && ReciboCapturado() is null;

    /// <summary>
    /// Carga las tarifas y, si el onboarding se repite, las respuestas anteriores.
    /// </summary>
    public async Task IniciarAsync()
    {
        Tarifas.Clear();
        foreach (var tarifa in await tarifas.ObtenerTarifasAsync())
        {
            Tarifas.Add(new OpcionTarifa(tarifa.Id, tarifa.Nombre));
        }

        await PrecargarRespuestasAsync();
        PasoActual = PasoOnboarding.Bienvenida;
        navegacion.IrA(PasoActual, this);
    }

    [RelayCommand(CanExecute = nameof(PuedeSeguir))]
    private void Siguiente()
    {
        if (PasoActual == PasoOnboarding.Listo)
        {
            return;
        }

        IrA(PasoActual + 1);
    }

    private bool PuedeSeguir() => !(PasoActual == PasoOnboarding.Recibo && ReciboIncompleto);

    [RelayCommand]
    private void Atras()
    {
        if (PasoActual > PasoOnboarding.Bienvenida)
        {
            IrA(PasoActual - 1);
        }
    }

    /// <summary>
    /// Salta la pregunta actual sin guardar su respuesta.
    /// </summary>
    [RelayCommand]
    private void Omitir()
    {
        LimpiarPaso(PasoActual);
        IrA(PasoActual + 1);
    }

    [RelayCommand(CanExecute = nameof(PuedeTerminar))]
    private async Task TerminarAsync()
    {
        Guardando = true;
        Error = null;
        try
        {
            await perfiles.GuardarOnboardingAsync(ConstruirDatos());
            if (ReciboSaltar == true && CargarDemo == true)
            {
                await datosDemo.CargarAsync(DateOnly.FromDateTime(DateTime.Today));
            }

            WeakReferenceMessenger.Default.Send(new OnboardingTerminadoMensaje());
        }
        catch (Exception)
        {
            Error = textos["Onboarding_ErrorGuardar"];
        }
        finally
        {
            Guardando = false;
        }
    }

    private bool PuedeTerminar() => !Guardando;

    partial void OnReciboManualChanged(bool? value)
    {
        if (value == true)
        {
            ReciboSaltar = false;
        }
    }

    partial void OnReciboSaltarChanged(bool? value)
    {
        if (value == true)
        {
            ReciboManual = false;
        }
    }

    partial void OnNoSeMiTarifaChanged(bool? value)
    {
        if (value == true)
        {
            TarifaSeleccionada = null;
        }
    }

    private void IrA(PasoOnboarding paso)
    {
        if (paso == PasoOnboarding.Listo)
        {
            ConstruirResumen();
        }

        PasoActual = paso;
        navegacion.IrA(paso, this);
    }

    private void LimpiarPaso(PasoOnboarding paso)
    {
        switch (paso)
        {
            case PasoOnboarding.Uso:
                UsaCasa = UsaNegocio = UsaRentas = UsaSolar = false;
                break;
            case PasoOnboarding.Ubicacion:
                EstadoSeleccionado = null;
                Municipio = NombreLugar = string.Empty;
                TarifaSeleccionada = null;
                NoSeMiTarifa = false;
                break;
            case PasoOnboarding.Recibo:
                ReciboManual = ReciboSaltar = false;
                CargarDemo = false;
                break;
        }
    }

    private async Task PrecargarRespuestasAsync()
    {
        var perfil = await perfiles.ObtenerPerfilAsync();
        if (perfil is not null)
        {
            var usos = perfil.Usos.Select(u => u.Uso).ToHashSet();
            UsaCasa = usos.Contains(UsoApp.Casa);
            UsaNegocio = usos.Contains(UsoApp.Negocio);
            UsaRentas = usos.Contains(UsoApp.Rentas);
            UsaSolar = usos.Contains(UsoApp.Solar);

            _objetivoGuardado = perfil.Objetivo?.Tipo;
            _presupuestoGuardado = perfil.Objetivo?.PresupuestoMax;
            _notificaciones = perfil.NotificacionesActivas;
        }

        var lugar = await perfiles.ObtenerLugarPrincipalAsync();
        if (lugar is not null)
        {
            NombreLugar = lugar.Nombre;
            EstadoSeleccionado = lugar.Estado;
            Municipio = lugar.Municipio ?? string.Empty;
            NoSeMiTarifa = lugar.TarifaPorConfirmar;
            TarifaSeleccionada = lugar.TarifaPorConfirmar ? null : Tarifas.FirstOrDefault(t => t.Id == lugar.TarifaId);
        }
    }

    private DatosOnboarding ConstruirDatos() => new()
    {
        Usos = UsosElegidos(),
        NombreLugar = NombreLugar,
        Estado = EstadoSeleccionado as string,
        Municipio = string.IsNullOrWhiteSpace(Municipio) ? null : Municipio.Trim(),
        TarifaId = NoSeMiTarifa == true ? null : (TarifaSeleccionada as OpcionTarifa)?.Id,
        Recibo = ReciboManual == true ? ReciboCapturado() : null,
        Objetivo = _objetivoGuardado,
        PresupuestoMax = _presupuestoGuardado,
        NotificacionesActivas = _notificaciones,
    };

    private List<UsoApp> UsosElegidos()
    {
        var usos = new List<UsoApp>();
        if (UsaCasa == true) usos.Add(UsoApp.Casa);
        if (UsaNegocio == true) usos.Add(UsoApp.Negocio);
        if (UsaRentas == true) usos.Add(UsoApp.Rentas);
        if (UsaSolar == true) usos.Add(UsoApp.Solar);
        return usos;
    }

    /// <summary>
    /// El recibo capturado a mano, o null si falta la fecha, los kWh o el monto.
    /// </summary>
    private ReciboCapturado? ReciboCapturado()
    {
        if (FechaCorte is not DateTimeOffset fecha
            || double.IsNaN(KwhRecibo) || KwhRecibo < 0
            || double.IsNaN(MontoRecibo) || MontoRecibo < 0)
        {
            return null;
        }

        return new ReciboCapturado(
            DateOnly.FromDateTime(fecha.Date),
            ReciboMensual == true ? 1 : 2,
            (decimal)KwhRecibo,
            (decimal)MontoRecibo,
            double.IsNaN(LecturaMedidorRecibo) || LecturaMedidorRecibo < 0 ? null : (decimal)LecturaMedidorRecibo);
    }

    private void ConstruirResumen()
    {
        Resumen.Clear();

        var usos = UsosElegidos();
        Resumen.Add(new FilaResumen(
            textos["Resumen_Usos"],
            usos.Count == 0
                ? textos["Resumen_UsosSinElegir"]
                : string.Join(", ", usos.Select(u => textos[$"Uso_{u}_Titulo/Text"]))));

        var nombre = string.IsNullOrWhiteSpace(NombreLugar) ? textos["Resumen_LugarSinNombre"] : NombreLugar.Trim();
        var ubicacion = string.Join(", ", new[] { EstadoSeleccionado as string, Municipio }.Where(t => !string.IsNullOrWhiteSpace(t)));
        Resumen.Add(new FilaResumen(
            textos["Resumen_Lugar"],
            ubicacion.Length == 0 ? nombre : textos.Formato("Resumen_LugarConUbicacion", nombre, ubicacion)));

        Resumen.Add(new FilaResumen(
            textos["Resumen_Tarifa"],
            NoSeMiTarifa != true && TarifaSeleccionada is OpcionTarifa tarifa
                ? tarifa.Nombre
                : textos["Resumen_TarifaPorConfirmar"]));

        var recibo = ReciboManual == true ? ReciboCapturado() : null;
        Resumen.Add(new FilaResumen(
            textos["Resumen_Recibo"],
            recibo is not null
                ? textos.Formato(
                    recibo.MesesDelPeriodo == 1 ? "Resumen_ReciboMensual" : "Resumen_ReciboBimestral",
                    recibo.Kwh,
                    recibo.Monto)
                : ReciboSaltar == true && CargarDemo == true
                    ? textos["Resumen_ReciboDemo"]
                    : textos["Resumen_ReciboDespues"]));

        Resumen.Add(new FilaResumen(
            textos["Resumen_Avisos"],
            _notificaciones ? textos["Resumen_AvisosActivos"] : textos["Resumen_AvisosInactivos"]));
    }
}
