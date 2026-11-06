using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Agregar un recibo por lo que se pagó (Fase 9c, D1): el mismo formulario en el onboarding, en Inicio y en Mis
/// recibos. Dos caminos, "Escribir cuánto pagué" (preseleccionado) y "Escanear mi recibo", que solo prellena el
/// total, la fecha de corte y la tarifa; todo se puede cambiar. En el primer recibo del lugar pregunta "¿Cada cuánto
/// te llega el recibo?" (D3). Se guarda con <see cref="IRecibosService.AgregarPorMontoAsync"/> y los impuestos y
/// cargos por defecto (<see cref="EstimadorPorMonto.CargosPorDefecto"/>).
/// </summary>
public sealed partial class AgregarReciboViewModel : ObservableObject
{
    private readonly IRecibosService recibos;
    private readonly ITarifaService tarifas;
    private readonly IReciboOcr ocr;
    private readonly ISelectorArchivos selector;
    private readonly ITextos textos;

    // Nombre → tipo de cada tarifa, para saber si la tarifa leída es comercial.
    private Dictionary<string, TipoTarifa> _tiposDeTarifa = new(StringComparer.OrdinalIgnoreCase);

    // La periodicidad que se preselecciona sin datos del recibo (la tarifa del lugar o la elegida en el onboarding).
    private TipoTarifa? _tipoTarifa;

    // Lo leído del recibo: el periodo y la tarifa mandan sobre la sugerencia por la tarifa del lugar.
    private DateOnly? _inicioLeido;
    private DateOnly? _finLeido;
    private TipoTarifa? _tipoLeido;

    // La persona ya eligió la periodicidad: las sugerencias ya no la cambian.
    private bool _periodicidadElegida;
    private bool _sugiriendo;

    // El escáner leyó una fecha de corte posterior a hoy y el calendario quedó vacío: se sigue explicando eso.
    private bool _fechaFuturaLeida;

    public AgregarReciboViewModel(
        IRecibosService recibos,
        ITarifaService tarifas,
        IReciboOcr ocr,
        ISelectorArchivos selector,
        ITextos textos)
    {
        this.recibos = recibos;
        this.tarifas = tarifas;
        this.ocr = ocr;
        this.selector = selector;
        this.textos = textos;

        // Las tarjetas empiezan sin marcar (null se vería como estado indeterminado), salvo "Escribir cuánto pagué"
        // y "Cada 2 meses", que van preseleccionadas.
        ReciboPorMonto = true;
        ReciboEscanear = false;
        CadaDosMeses = true;
        CadaMes = false;
        TotalPagado = double.NaN;
    }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    // ---- Los dos caminos ----

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

    /// <summary>
    /// El nombre de la tarifa que se leyó del recibo (el onboarding la preselecciona en Tu tarifa).
    /// </summary>
    [ObservableProperty] public partial string? TarifaLeida { get; set; }

    // ---- "¿Cada cuánto te llega el recibo?" (D3): solo en el primer recibo del lugar ----

    [ObservableProperty] public partial bool PreguntarPeriodicidad { get; set; } = true;
    [ObservableProperty] public partial bool? CadaDosMeses { get; set; }
    [ObservableProperty] public partial bool? CadaMes { get; set; }

    /// <summary>
    /// Los meses del recibo que se mandan al guardar: la respuesta, o null si ya no se pregunta.
    /// </summary>
    public int? MesesElegidos => PreguntarPeriodicidad ? (CadaMes == true ? 1 : 2) : null;

    /// <summary>
    /// Prepara el formulario para un lugar (null en el primer uso, cuando todavía no existe): con recibos ya
    /// guardados no se pregunta la periodicidad. <paramref name="tipoTarifa"/> es el de la tarifa del lugar.
    /// </summary>
    public async Task IniciarAsync(int? lugarId, TipoTarifa? tipoTarifa)
    {
        _tiposDeTarifa = (await tarifas.ObtenerTarifasAsync())
            .GroupBy(t => t.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Tipo, StringComparer.OrdinalIgnoreCase);
        PreguntarPeriodicidad = lugarId is not int id || !await recibos.TieneRecibosAsync(id);
        _tipoTarifa = tipoTarifa;
        SugerirPeriodicidad();
    }

    /// <summary>
    /// El onboarding avisa la tarifa que eligió la persona: si es comercial y no se eligió periodicidad, "Cada mes".
    /// </summary>
    public void UsarTipoDeTarifa(TipoTarifa? tipoTarifa)
    {
        _tipoTarifa = tipoTarifa;
        SugerirPeriodicidad();
    }

    private void SugerirPeriodicidad()
    {
        if (_periodicidadElegida)
        {
            return;
        }

        var meses = PeriodicidadRecibo.Sugerir(_inicioLeido, _finLeido, _tipoLeido ?? _tipoTarifa);
        _sugiriendo = true;
        CadaMes = meses == 1;
        CadaDosMeses = meses != 1;
        _sugiriendo = false;
    }

    partial void OnCadaMesChanged(bool? value)
    {
        if (value == true)
        {
            CadaDosMeses = false;
            _periodicidadElegida |= !_sugiriendo;
        }
    }

    partial void OnCadaDosMesesChanged(bool? value)
    {
        if (value == true)
        {
            CadaMes = false;
            _periodicidadElegida |= !_sugiriendo;
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
                // explica (Fase 9b, D5).
                fechaFutura = ValidadorFechaCorte.Validar(fecha, Hoy) is not null;
                FechaCorte = fechaFutura ? null : new DateTimeOffset(fecha.ToDateTime(TimeOnly.MinValue));
                _fechaFuturaLeida = fechaFutura;
            }

            if (leido.Tarifa.Valor is { } nombre && _tiposDeTarifa.TryGetValue(nombre, out var tipo))
            {
                TarifaLeida = _tiposDeTarifa.Keys.First(k => string.Equals(k, nombre, StringComparison.OrdinalIgnoreCase));
                _tipoLeido = tipo;
            }

            // D3: el periodo leído (o la tarifa leída) preselecciona la periodicidad.
            _inicioLeido = leido.PeriodoInicio.Valor;
            _finLeido = leido.PeriodoFin.Valor;
            SugerirPeriodicidad();

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

    // ---- Validar y guardar (Core: EstimadorPorMonto.ValidarRecibo y el traslape de RecibosService) ----

    /// <summary>
    /// El total y la fecha de corte escritos, o null si falta alguno.
    /// </summary>
    public (decimal Total, DateOnly Fecha)? Elegido()
        => double.IsNaN(TotalPagado) || FechaCorte is not DateTimeOffset fecha
            ? null
            : ((decimal)TotalPagado, DateOnly.FromDateTime(fecha.Date));

    /// <summary>
    /// El error en español, o null si el recibo se puede guardar. Sin lugar (primer uso) no hay con qué cruzarlo.
    /// También lo deja en <see cref="ErrorRecibo"/>.
    /// </summary>
    public async Task<string?> ValidarAsync(int? lugarId)
    {
        ErrorRecibo = await ErrorAsync(lugarId) ?? string.Empty;
        return ErrorRecibo.Length > 0 ? ErrorRecibo : null;
    }

    private async Task<string?> ErrorAsync(int? lugarId)
    {
        if (Elegido() is not { } recibo)
        {
            return FechaCorte is null && _fechaFuturaLeida
                ? textos[$"ReciboMonto_Error_{ErrorReciboPorMonto.FechaFutura}"]
                : textos["OnboardingRecibo_Incompleto"];
        }

        if (EstimadorPorMonto.ValidarRecibo(recibo.Total, recibo.Fecha, Hoy) is { } error)
        {
            return textos[$"ReciboMonto_Error_{error}"];
        }

        if (lugarId is int id)
        {
            var previa = await recibos.PrevisualizarPorMontoAsync(id, recibo.Total, recibo.Fecha, EstimadorPorMonto.CargosPorDefecto, Hoy, MesesElegidos);
            if (previa.Error is { } errorPrevia)
            {
                return textos[$"ReciboMonto_Error_{errorPrevia}"];
            }
        }

        return null;
    }

    /// <summary>
    /// Guarda el recibo en el lugar (con la periodicidad si es el primero). Devuelve el error en español o null.
    /// </summary>
    public async Task<string?> GuardarAsync(int lugarId)
    {
        if (Elegido() is not { } recibo)
        {
            return await ValidarAsync(lugarId);
        }

        var resultado = await recibos.AgregarPorMontoAsync(
            lugarId, recibo.Total, recibo.Fecha, EstimadorPorMonto.CargosPorDefecto, Hoy, MesesElegidos);
        ErrorRecibo = resultado.Error is { } error ? textos[$"ReciboMonto_Error_{error}"] : string.Empty;
        return ErrorRecibo.Length > 0 ? ErrorRecibo : null;
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
}
