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
/// (o se prellena escaneándolo) y se guarda como recibo aproximado con <see cref="IRecibosService"/>. Al terminar
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
    private readonly IRecibosService recibos;
    private readonly IReciboOcr ocr;
    private readonly ISelectorArchivos selector;
    private readonly ITextos textos;
    private readonly INavegacionOnboarding navegacion;

    public OnboardingViewModel(
        IPerfilService perfiles,
        IDatosDemoService datosDemo,
        ITarifaService tarifas,
        IRecibosService recibos,
        IReciboOcr ocr,
        ISelectorArchivos selector,
        ITextos textos,
        INavegacionOnboarding navegacion)
    {
        this.perfiles = perfiles;
        this.datosDemo = datosDemo;
        this.tarifas = tarifas;
        this.recibos = recibos;
        this.ocr = ocr;
        this.selector = selector;
        this.textos = textos;
        this.navegacion = navegacion;

        // Las casillas y tarjetas empiezan sin marcar (null se vería como estado indeterminado), salvo
        // "Escribir cuánto pagué", que va preseleccionada.
        UsaCasa = UsaNegocio = UsaRentas = UsaSolar = false;
        Municipio = NombreLugar = string.Empty;
        NoSeMiTarifa = false;
        ReciboPorMonto = true;
        ReciboEscanear = false;
        TotalPagado = double.NaN;
    }

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

    // Tu recibo: "Escribir cuánto pagué" o "Escanear mi recibo"; los dos llenan el total y la fecha de corte.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarEscanear))]
    public partial bool? ReciboPorMonto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarEscanear))]
    public partial bool? ReciboEscanear { get; set; }

    public bool MostrarEscanear => ReciboEscanear == true;

    [ObservableProperty] public partial double TotalPagado { get; set; }
    [ObservableProperty] public partial DateTimeOffset? FechaCorte { get; set; }

    /// <summary>
    /// La fecha de corte no puede ser posterior a hoy (Fase 9b, D5).
    /// </summary>
    public DateTimeOffset FechaMaxima { get; } = new(DateTime.Today);

    [ObservableProperty] public partial string ErrorRecibo { get; set; } = string.Empty;
    [ObservableProperty] public partial string AvisoEscaneo { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ElegirReciboCommand))]
    public partial bool Leyendo { get; set; }

    // "Todavía no tengo mi recibo": se sigue sin recibo.
    private bool _sinRecibo;

    // El escáner leyó una fecha de corte posterior a hoy y el calendario quedó vacío: se sigue explicando eso.
    private bool _fechaFuturaLeida;

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
            Tarifas.Add(new OpcionTarifa(tarifa.Id, tarifa.Nombre));
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
                // Lo escrito se queda en la pantalla si hay un error.
                ErrorRecibo = await ValidarReciboAsync() ?? string.Empty;
                if (ErrorRecibo.Length > 0)
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
        ErrorRecibo = string.Empty;
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
            if (ReciboElegido() is { } recibo && await perfiles.ObtenerLugarPrincipalAsync() is { } lugar)
            {
                // Impuestos y cargos por defecto (EstimadorPorMonto.CargosPorDefecto); no se piden aquí.
                var resultado = await recibos.AgregarPorMontoAsync(lugar.Id, recibo.Total, recibo.Fecha, EstimadorPorMonto.CargosPorDefecto, Hoy);
                if (resultado.Error is { } error)
                {
                    Error = textos[$"ReciboMonto_Error_{error}"];
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

    // ---- Escanear: solo prellena el total, la fecha de corte y la tarifa ----

    private bool PuedeElegir() => !Leyendo;

    [RelayCommand(CanExecute = nameof(PuedeElegir))]
    private async Task ElegirReciboAsync()
    {
        if (await selector.ElegirReciboAsync() is { } ruta)
        {
            await LeerAsync(ruta);
        }
    }

#if DEBUG
    // Solo para pruebas automatizadas: lee una ruta sin pasar por el selector de archivos.
    [ObservableProperty] public partial string RutaPrueba { get; set; } = string.Empty;

    [RelayCommand]
    private Task LeerRutaAsync() => LeerAsync(RutaPrueba.Trim().Trim('"'));
#endif

    private async Task LeerAsync(string ruta)
    {
        Leyendo = true;
        ErrorRecibo = AvisoEscaneo = string.Empty;
        try
        {
            var resultado = await ocr.LeerAsync(ruta);
            if (resultado.Error is ErrorOcr error)
            {
                ErrorRecibo = error == ErrorOcr.SinIdioma ? textos["Escanear_SinIdiomaTexto/Text"] : textos[$"ErrorOcr_{error}"];
                return;
            }

            // Las líneas reconocidas solo viven aquí: no se guardan ni se registran.
            var leido = ReciboParser.Analizar(resultado.Lineas);
            var fechaFutura = false;
            if (leido.FechaCorte.Valor is DateOnly fecha)
            {
                // El calendario no acepta fechas posteriores a hoy y la cambiaría sin avisar: se deja vacía y se
                // explica (D5).
                fechaFutura = ValidadorFechaCorte.Validar(fecha, Hoy) is not null;
                FechaCorte = fechaFutura ? null : new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue));
                _fechaFuturaLeida = fechaFutura;
            }

            if (leido.Tarifa.Valor is { } nombre
                && Tarifas.FirstOrDefault(t => string.Equals(t.Nombre, nombre, StringComparison.OrdinalIgnoreCase)) is { } tarifa)
            {
                TarifaSeleccionada = tarifa;
                NoSeMiTarifa = false;
            }

            if (leido.Total.Valor is decimal total)
            {
                TotalPagado = (double)total;
                AvisoEscaneo = textos["OnboardingRecibo_Revisa"];
            }
            else
            {
                ErrorRecibo = textos["OnboardingRecibo_SinTotal"];
            }

            if (fechaFutura)
            {
                ErrorRecibo = textos[$"ReciboMonto_Error_{ErrorReciboPorMonto.FechaFutura}"];
            }
        }
        finally
        {
            Leyendo = false;
        }
    }

    // ---- Validación del recibo (Core: EstimadorPorMonto.ValidarRecibo y el traslape de RecibosService) ----

    private (decimal Total, DateOnly Fecha)? ReciboElegido()
        => _sinRecibo || double.IsNaN(TotalPagado) || FechaCorte is not DateTimeOffset fecha
            ? null
            : ((decimal)TotalPagado, DateOnly.FromDateTime(fecha.Date));

    private async Task<string?> ValidarReciboAsync()
    {
        if (ReciboElegido() is not { } recibo)
        {
            return FechaCorte is null && _fechaFuturaLeida
                ? textos[$"ReciboMonto_Error_{ErrorReciboPorMonto.FechaFutura}"]
                : textos["OnboardingRecibo_Incompleto"];
        }

        if (EstimadorPorMonto.ValidarRecibo(recibo.Total, recibo.Fecha, Hoy) is { } error)
        {
            return textos[$"ReciboMonto_Error_{error}"];
        }

        // Al repetir el onboarding ya hay un lugar: el recibo no se puede cruzar con uno guardado.
        if (await perfiles.ObtenerLugarPrincipalAsync() is { } lugar)
        {
            var previa = await recibos.PrevisualizarPorMontoAsync(lugar.Id, recibo.Total, recibo.Fecha, EstimadorPorMonto.CargosPorDefecto, Hoy);
            if (previa.Error is { } errorPrevia)
            {
                return textos[$"ReciboMonto_Error_{errorPrevia}"];
            }
        }

        return null;
    }

    partial void OnFechaCorteChanged(DateTimeOffset? value)
    {
        if (value is not null)
        {
            _fechaFuturaLeida = false;
        }
    }

    partial void OnReciboPorMontoChanged(bool? value)
    {
        if (value == true)
        {
            ReciboEscanear = false;
        }
    }

    partial void OnReciboEscanearChanged(bool? value)
    {
        if (value == true)
        {
            ReciboPorMonto = false;
        }
    }

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
