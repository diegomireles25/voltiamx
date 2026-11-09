using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Fase 9k: la confianza de un dato leído del recibo y su etiqueta (LEÍDO, REVISAR o NO ENCONTRADO).
/// </summary>
public sealed record EstadoCampoLeido(Confianza Confianza, string Texto)
{
    public static EstadoCampoLeido Vacio { get; } = new(Confianza.NoEncontrado, string.Empty);
}

/// <summary>
/// Agregar un recibo por lo que se pagó (Fase 9c, D1): el mismo formulario en el onboarding, en Inicio y en Mis
/// recibos. Dos caminos, "Escribir cuánto pagué" (preseleccionado) y "Escanear mi recibo", que solo prellena el
/// total, la fecha de corte y la tarifa; todo se puede cambiar. En el primer recibo del lugar pregunta "¿Cada cuánto
/// te llega el recibo?" (D3). Se guarda con <see cref="IRecibosService.AgregarPorMontoAsync"/> y los impuestos y
/// cargos por defecto (<see cref="EstimadorPorMonto.CargosPorDefecto"/>).
/// Fase 9k: lo escaneado prellena el importe del recibo, la fecha en que termina el periodo y el consumo, cada uno
/// con su confianza; muestra los tramos, los avisos y la comparación con la tarifa del lugar. Con un consumo leído y
/// coherente, el recibo se guarda con esos kWh (<see cref="IRecibosService.AgregarConConsumoAsync"/>), no aproximado.
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

    // Fase 9k: el lugar (para comparar con su tarifa) y la energía leída del recibo.
    private int? _lugarId;
    private decimal? _energiaLeida;
    private int _comparacionesPedidas;

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
        ConsumoKwh = double.NaN;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeGuardar))]
    public partial double TotalPagado { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PuedeGuardar))]
    public partial DateTimeOffset? FechaCorte { get; set; }

    /// <summary>
    /// Fase 9k (D9): Guardar solo se apaga sin importe o sin fecha.
    /// </summary>
    public bool PuedeGuardar => Elegido() is not null;

    // ---- Fase 9k: lo leído del recibo, cada dato con su confianza ----

    /// <summary>
    /// Ya se leyó un recibo: se muestran las etiquetas LEÍDO / REVISAR / NO ENCONTRADO y el detalle.
    /// </summary>
    [ObservableProperty] public partial bool Leido { get; set; }

    [ObservableProperty] public partial EstadoCampoLeido EstadoImporte { get; set; } = EstadoCampoLeido.Vacio;
    [ObservableProperty] public partial EstadoCampoLeido EstadoFecha { get; set; } = EstadoCampoLeido.Vacio;
    [ObservableProperty] public partial EstadoCampoLeido EstadoConsumo { get; set; } = EstadoCampoLeido.Vacio;
    [ObservableProperty] public partial EstadoCampoLeido EstadoTarifa { get; set; } = EstadoCampoLeido.Vacio;

    /// <summary>
    /// "Del 12 de junio al 10 de agosto", si se leyó el periodo facturado.
    /// </summary>
    [ObservableProperty] public partial string PeriodoLeidoTexto { get; set; } = string.Empty;

    [ObservableProperty] public partial string TarifaLeidaTexto { get; set; } = string.Empty;

    /// <summary>
    /// El consumo del recibo en kWh; vacío (NaN) si no se leyó o no es confiable. Con valor, se guarda tal cual.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsaConsumo), nameof(NoUsaConsumo), nameof(ConsumoLeidoTexto))]
    public partial double ConsumoKwh { get; set; }

    /// <summary>
    /// El recibo se guarda con su consumo (exacto), no aproximado por su monto.
    /// </summary>
    public bool UsaConsumo => ConsumoUsado() is not null;

    public bool NoUsaConsumo => !UsaConsumo;

    public string ConsumoLeidoTexto => ConsumoUsado() is decimal kwh ? textos.Formato("FormularioRecibo_ConsumoLeido", kwh) : string.Empty;

    public ObservableCollection<string> Tramos { get; } = [];

    [ObservableProperty] public partial bool TieneTramos { get; set; }

    public ObservableCollection<string> Avisos { get; } = [];

    [ObservableProperty] public partial bool TieneAvisos { get; set; }

    // Comparación con la tarifa guardada del lugar (D8): no cambia nada.
    [ObservableProperty] public partial bool TieneComparacion { get; set; }
    [ObservableProperty] public partial VeredictoRecibo Veredicto { get; set; }
    [ObservableProperty] public partial string VeredictoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string ComparacionTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string ComparacionTemporada { get; set; } = string.Empty;

    private decimal? ConsumoUsado() => double.IsNaN(ConsumoKwh) || ConsumoKwh <= 0 ? null : (decimal)ConsumoKwh;

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
        _lugarId = lugarId;
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
            _ = CompararAsync();
        }
    }

    partial void OnCadaDosMesesChanged(bool? value)
    {
        if (value == true)
        {
            CadaMes = false;
            _periodicidadElegida |= !_sugiriendo;
            _ = CompararAsync();
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
            MostrarLeido(leido);
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

            // D4: el importe de este recibo (sin saldos anteriores); el total a pagar solo si no hay desglose.
            if (leido.ImporteRecibo.Valor is decimal total)
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

            await CompararAsync();
        }
        finally
        {
            Leyendo = false;
        }
    }

    /// <summary>
    /// Fase 9k (D9): cada dato leído con su confianza, el periodo, los tramos y los avisos de coherencia. El consumo
    /// solo se prellena si es confiable (<see cref="ReciboCoherencia.ConsumoConfiable"/>).
    /// </summary>
    private void MostrarLeido(ReciboLeido leido)
    {
        Leido = true;
        EstadoImporte = Estado(leido.ImporteRecibo.Valor is null ? Confianza.NoEncontrado : leido.ImporteRecibo.Confianza);
        EstadoFecha = Estado(leido.FechaCorte.Valor is null ? Confianza.NoEncontrado : leido.FechaCorte.Confianza);
        PeriodoLeidoTexto = leido is { PeriodoInicio.Valor: DateOnly inicio, PeriodoFin.Valor: DateOnly fin }
            ? textos.Formato("FormularioRecibo_PeriodoLeido", inicio.ToDateTime(TimeOnly.MinValue), fin.ToDateTime(TimeOnly.MinValue))
            : string.Empty;

        var tarifaRegistrada = leido.Tarifa.Valor is { } nombre && _tiposDeTarifa.ContainsKey(nombre);
        EstadoTarifa = Estado(tarifaRegistrada ? leido.Tarifa.Confianza : Confianza.NoEncontrado);
        TarifaLeidaTexto = leido.Tarifa.Valor is { } tarifa ? textos.Formato("FormularioRecibo_TarifaLeida", tarifa.ToUpperInvariant()) : string.Empty;

        var confiable = ReciboCoherencia.ConsumoConfiable(leido);
        ConsumoKwh = confiable is decimal kwh ? (double)kwh : double.NaN;
        EstadoConsumo = Estado(confiable is not null ? Confianza.Alta
            : leido.ConsumoKwh.Valor is not null ? Confianza.Baja
            : Confianza.NoEncontrado);
        _energiaLeida = leido.ImporteEnergia.Valor;

        Tramos.Clear();
        foreach (var rango in leido.Rangos)
        {
            Tramos.Add(rango is { Kwh.Valor: decimal k, Precio.Valor: decimal precio, Importe.Valor: decimal subtotal }
                ? textos.Formato("FormularioRecibo_Tramo", rango.Nombre, k, precio, subtotal)
                : textos.Formato("FormularioRecibo_TramoSinPrecio", rango.Nombre, rango.Kwh.Valor ?? 0m));
        }

        TieneTramos = Tramos.Count > 0;

        Avisos.Clear();
        foreach (var aviso in ReciboCoherencia.Comprobar(leido, _tiposDeTarifa.Keys))
        {
            Avisos.Add(aviso.Codigo == CodigoAvisoRecibo.TarifaNoRegistrada
                ? textos.Formato("AvisoRecibo_TarifaNoRegistrada", leido.Tarifa.Valor)
                : textos.Formato($"AvisoRecibo_{aviso.Codigo}", aviso.Calculado, aviso.Leido));
        }

        TieneAvisos = Avisos.Count > 0;
    }

    private EstadoCampoLeido Estado(Confianza confianza) => new(confianza, textos[$"Confianza_{confianza}"]);

    /// <summary>
    /// D8: la energía del recibo contra la tarifa guardada del lugar (sin lugar, como en el primer uso, no hay con
    /// qué). Solo informa; no cambia la tarifa.
    /// </summary>
    private async Task CompararAsync()
    {
        var pedido = ++_comparacionesPedidas;
        ComparacionContraTarifa? comparacion = null;
        if (Leido && _lugarId is int lugarId && ConsumoUsado() is decimal kwh && _energiaLeida is decimal energia)
        {
            comparacion = await recibos.CompararConTarifaAsync(lugarId, kwh, energia, MesesElegidos);
        }

        if (pedido != _comparacionesPedidas)
        {
            return;
        }

        TieneComparacion = comparacion is not null;
        if (comparacion is null || _energiaLeida is not decimal deEnergia)
        {
            return;
        }

        Veredicto = comparacion.Veredicto;
        VeredictoTexto = textos[$"Validar_Veredicto_{comparacion.Veredicto}"];
        ComparacionTexto = textos.Formato("FormularioRecibo_Comparacion", comparacion.SubtotalEnergia, ConsumoUsado(), deEnergia);
        ComparacionTemporada = comparacion.Veredicto == VeredictoRecibo.Coincide ? string.Empty : textos["FormularioRecibo_OtraTemporada"];
    }

    partial void OnConsumoKwhChanged(double value) => _ = CompararAsync();

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
            var previa = ConsumoUsado() is decimal kwh
                ? await recibos.PrevisualizarConConsumoAsync(id, kwh, recibo.Total, recibo.Fecha, Hoy, MesesElegidos)
                : await recibos.PrevisualizarPorMontoAsync(id, recibo.Total, recibo.Fecha, EstimadorPorMonto.CargosPorDefecto, Hoy, MesesElegidos);
            if (previa.Error is not null)
            {
                return TextosRecibo.Error(textos, previa);
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

        // D6: con el consumo del recibo se guardan esos kWh; si no, aproximado por el monto.
        var resultado = ConsumoUsado() is decimal kwh
            ? await recibos.AgregarConConsumoAsync(lugarId, kwh, recibo.Total, recibo.Fecha, Hoy, MesesElegidos)
            : await recibos.AgregarPorMontoAsync(lugarId, recibo.Total, recibo.Fecha, EstimadorPorMonto.CargosPorDefecto, Hoy, MesesElegidos);
        ErrorRecibo = resultado.Error is not null ? TextosRecibo.Error(textos, resultado) : string.Empty;
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
