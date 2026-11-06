using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels.Onboarding;

/// <summary>
/// Primer uso (Fase 9b): Bienvenida, Tu recibo (1/2), Tu tarifa (2/2) y Listo. El recibo se escribe por su total
/// (o se prellena escaneándolo) con el formulario común <see cref="AgregarReciboViewModel"/> (Fase 9c), el mismo de
/// Inicio y Mis recibos, y se guarda como recibo aproximado con <see cref="IRecibosService"/>. Al terminar
/// el perfil queda como Casa. Las pantallas viejas de uso y ubicación ya no están en el flujo; sus propiedades se
/// conservan porque sus páginas siguen en el proyecto, y al repetir el onboarding se devuelven tal cual.
/// </summary>
public sealed partial class OnboardingViewModel : ObservableObject
{
    private static readonly PasoOnboarding[] Flujo = [PasoOnboarding.Bienvenida, PasoOnboarding.Recibo, PasoOnboarding.Tarifa, PasoOnboarding.Listo];

    /// <summary>
    /// Las preguntas que cuenta el indicador "PASO n/m".
    /// </summary>
    private static readonly PasoOnboarding[] Preguntas = [PasoOnboarding.Recibo, PasoOnboarding.Tarifa];

    private readonly IPerfilService perfiles;
    private readonly IDatosDemoService datosDemo;
    private readonly ITarifaService tarifas;
    private readonly ITextos textos;
    private readonly INavegacionOnboarding navegacion;

    public OnboardingViewModel(
        IPerfilService perfiles,
        IDatosDemoService datosDemo,
        ITarifaService tarifas,
        AgregarReciboViewModel recibo,
        ITextos textos,
        INavegacionOnboarding navegacion)
    {
        this.perfiles = perfiles;
        this.datosDemo = datosDemo;
        this.tarifas = tarifas;
        this.textos = textos;
        this.navegacion = navegacion;
        Recibo = recibo;

        // Las casillas empiezan sin marcar (null se vería como estado indeterminado).
        UsaCasa = UsaNegocio = UsaRentas = UsaSolar = false;
        Municipio = NombreLugar = string.Empty;
        NoSeMiTarifa = false;

        // La tarifa que se lee del recibo se preselecciona en Tu tarifa.
        Recibo.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(AgregarReciboViewModel.TarifaLeida)
                && Recibo.TarifaLeida is { } nombre
                && Tarifas.FirstOrDefault(t => string.Equals(t.Nombre, nombre, StringComparison.OrdinalIgnoreCase)) is { } tarifa)
            {
                TarifaSeleccionada = tarifa;
                NoSeMiTarifa = false;
            }
        };
    }

    /// <summary>
    /// Tu recibo: el formulario común (dos caminos, total, fecha de corte y, en el primer recibo, la periodicidad).
    /// </summary>
    public AgregarReciboViewModel Recibo { get; }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoPaso), nameof(MostrarIndicador), nameof(MostrarNavegacion))]
    public partial PasoOnboarding PasoActual { get; set; }

    // Uso y ubicación: ya no se preguntan (Fase 9b). Se conservan para las páginas viejas y para devolver lo
    // guardado al repetir el onboarding.
    [ObservableProperty] public partial bool? UsaCasa { get; set; }
    [ObservableProperty] public partial bool? UsaNegocio { get; set; }
    [ObservableProperty] public partial bool? UsaRentas { get; set; }
    [ObservableProperty] public partial bool? UsaSolar { get; set; }
    [ObservableProperty] public partial object? EstadoSeleccionado { get; set; }
    [ObservableProperty] public partial string Municipio { get; set; }
    [ObservableProperty] public partial string NombreLugar { get; set; }

    // Tu tarifa
    [ObservableProperty] public partial object? TarifaSeleccionada { get; set; }
    [ObservableProperty] public partial bool? NoSeMiTarifa { get; set; }

    // "Todavía no tengo mi recibo": se sigue sin recibo.
    private bool _sinRecibo;

    // El lugar que ya existe al repetir el onboarding (null en el primer uso).
    private int? _lugarId;

    /// <summary>
    /// Las notificaciones empiezan apagadas en un perfil nuevo y se encienden desde Configuración. Al repetir
    /// el onboarding se conserva lo que el perfil ya tenía.
    /// </summary>
    private bool _notificaciones;

    // Al repetir el onboarding se devuelven tal cual (ya no se preguntan) para no borrar datos guardados.
    private TipoObjetivo? _objetivoGuardado;
    private decimal? _presupuestoGuardado;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TerminarCommand), nameof(ProbarDemoCommand), nameof(SiguienteCommand))]
    public partial bool Guardando { get; set; }

    [ObservableProperty] public partial string? Error { get; set; }

    public IReadOnlyList<string> Estados { get; } = EstadosMexico.Todos;

    public ObservableCollection<OpcionTarifa> Tarifas { get; } = [];

    public ObservableCollection<FilaResumen> Resumen { get; } = [];

    public string TextoPaso => MostrarIndicador
        ? textos.Formato("Onboarding_Paso", Array.IndexOf(Preguntas, PasoActual) + 1, Preguntas.Length)
        : string.Empty;

    /// <summary>
    /// Indicador "PASO n/2" y barra Atrás/Siguiente: solo en Tu recibo y Tu tarifa.
    /// </summary>
    public bool MostrarIndicador => Preguntas.Contains(PasoActual);

    public bool MostrarNavegacion => MostrarIndicador;

    /// <summary>
    /// Carga las tarifas y, si el onboarding se repite, las respuestas anteriores.
    /// </summary>
    public async Task IniciarAsync()
    {
        Tarifas.Clear();
        foreach (var tarifa in await tarifas.ObtenerTarifasAsync())
        {
            Tarifas.Add(new OpcionTarifa(tarifa.Id, tarifa.Nombre, tarifa.Tipo));
        }

        await PrecargarRespuestasAsync();
        PasoActual = PasoOnboarding.Bienvenida;
        navegacion.IrA(PasoActual, this);
    }

    private bool PuedeSeguir() => !Guardando;

    [RelayCommand(CanExecute = nameof(PuedeSeguir))]
    private async Task SiguienteAsync()
    {
        switch (PasoActual)
        {
            case PasoOnboarding.Recibo:
                // Lo escrito se queda en la pantalla si hay un error. Al repetir el onboarding ya hay un lugar: el
                // recibo no se puede cruzar con uno guardado.
                if (await Recibo.ValidarAsync(_lugarId) is not null)
                {
                    return;
                }

                _sinRecibo = false;
                IrA(PasoOnboarding.Tarifa);
                break;
            case PasoOnboarding.Listo:
                break;
            default:
                IrA(Flujo[Array.IndexOf(Flujo, PasoActual) + 1]);
                break;
        }
    }

    [RelayCommand]
    private void Atras()
    {
        var indice = Array.IndexOf(Flujo, PasoActual);
        if (indice > 0)
        {
            IrA(Flujo[indice - 1]);
        }
    }

    /// <summary>
    /// "Omitir esta pregunta": en Tu recibo es seguir sin recibo; en Tu tarifa, "No sé mi tarifa".
    /// </summary>
    [RelayCommand]
    private void Omitir()
    {
        if (PasoActual == PasoOnboarding.Recibo)
        {
            SinRecibo();
            return;
        }

        if (PasoActual == PasoOnboarding.Tarifa)
        {
            NoSeMiTarifa = true;
            IrA(PasoOnboarding.Listo);
        }
    }

    /// <summary>
    /// "Todavía no tengo mi recibo".
    /// </summary>
    [RelayCommand]
    private void SinRecibo()
    {
        _sinRecibo = true;
        Recibo.ErrorRecibo = string.Empty;
        IrA(PasoOnboarding.Tarifa);
    }

    /// <summary>
    /// "Probar con datos de ejemplo": guarda un perfil de casa sin recibo, carga la demo y abre Inicio.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeSeguir))]
    private async Task ProbarDemoAsync()
    {
        Guardando = true;
        Error = null;
        try
        {
            _sinRecibo = true;
            await perfiles.GuardarOnboardingAsync(ConstruirDatos());
            await datosDemo.CargarAsync(Hoy);
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

    [RelayCommand(CanExecute = nameof(PuedeTerminar))]
    private async Task TerminarAsync()
    {
        Guardando = true;
        Error = null;
        try
        {
            await perfiles.GuardarOnboardingAsync(ConstruirDatos());
            if (ReciboElegido() is not null && await perfiles.ObtenerLugarPrincipalAsync() is { } lugar)
            {
                // Impuestos y cargos por defecto y la periodicidad elegida (si es el primer recibo).
                if (await Recibo.GuardarAsync(lugar.Id) is { } error)
                {
                    Error = error;
                    return;
                }
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

    // ---- Tu recibo: lo valida y lo guarda el formulario común (AgregarReciboViewModel) ----

    private (decimal Total, DateOnly Fecha)? ReciboElegido() => _sinRecibo ? null : Recibo.Elegido();

    partial void OnNoSeMiTarifaChanged(bool? value)
    {
        if (value == true)
        {
            TarifaSeleccionada = null;
        }
    }

    partial void OnTarifaSeleccionadaChanged(object? value)
    {
        if (value is not null)
        {
            NoSeMiTarifa = false;
        }

        // D3: una tarifa comercial preselecciona "Cada mes" si la persona no eligió otra cosa.
        Recibo.UsarTipoDeTarifa((value as OpcionTarifa)?.Tipo);
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

    private async Task PrecargarRespuestasAsync()
    {
        var perfil = await perfiles.ObtenerPerfilAsync();
        if (perfil is not null)
        {
            _objetivoGuardado = perfil.Objetivo?.Tipo;
            _presupuestoGuardado = perfil.Objetivo?.PresupuestoMax;
            _notificaciones = perfil.NotificacionesActivas;
        }

        var lugar = await perfiles.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        await Recibo.IniciarAsync(_lugarId, lugar?.Tarifa?.Tipo);
        if (lugar is not null)
        {
            NombreLugar = lugar.Nombre;
            EstadoSeleccionado = lugar.Estado;
            Municipio = lugar.Municipio ?? string.Empty;
            NoSeMiTarifa = lugar.TarifaPorConfirmar;
            TarifaSeleccionada = lugar.TarifaPorConfirmar ? null : Tarifas.FirstOrDefault(t => t.Id == lugar.TarifaId);
        }
    }

    // Fase 9b (D1): el perfil queda como Casa; el día de corte sale de la fecha del recibo.
    private DatosOnboarding ConstruirDatos() => new()
    {
        Usos = [UsoApp.Casa],
        NombreLugar = NombreLugar,
        Estado = EstadoSeleccionado as string,
        Municipio = string.IsNullOrWhiteSpace(Municipio) ? null : Municipio.Trim(),
        TarifaId = NoSeMiTarifa == true ? null : (TarifaSeleccionada as OpcionTarifa)?.Id,
        FechaCorteRecibo = ReciboElegido()?.Fecha,
        Objetivo = _objetivoGuardado,
        PresupuestoMax = _presupuestoGuardado,
        NotificacionesActivas = _notificaciones,
    };

    private void ConstruirResumen()
    {
        Resumen.Clear();
        Resumen.Add(new FilaResumen(
            textos["Resumen_Recibo"],
            ReciboElegido() is { } recibo
                ? textos.Formato("Resumen_ReciboTotalYFecha", recibo.Total, recibo.Fecha)
                : textos["Resumen_SinRecibo"]));

        Resumen.Add(new FilaResumen(
            textos["Resumen_Tarifa"],
            NoSeMiTarifa != true && TarifaSeleccionada is OpcionTarifa tarifa
                ? tarifa.Nombre
                : textos["Resumen_TarifaPorConfirmar"]));

        Resumen.Add(new FilaResumen(
            textos["Resumen_Avisos"],
            _notificaciones ? textos["Resumen_AvisosActivos"] : textos["Resumen_AvisosInactivos"]));
    }
}
