using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Campo numérico del recibo que la persona puede corregir. <see cref="Valor"/> es NaN si está vacío.
/// </summary>
public sealed partial class CampoReciboNumero(string titulo) : ObservableObject
{
    public string Titulo { get; } = titulo;

    [ObservableProperty] public partial double Valor { get; set; } = double.NaN;
    [ObservableProperty] public partial Confianza Confianza { get; set; } = Confianza.NoEncontrado;
    [ObservableProperty] public partial string EtiquetaConfianza { get; set; } = string.Empty;

    public decimal? Decimal => double.IsNaN(Valor) ? null : (decimal)Valor;
}

/// <summary>
/// Fecha del recibo que la persona puede corregir.
/// </summary>
public sealed partial class CampoReciboFecha(string titulo) : ObservableObject
{
    public string Titulo { get; } = titulo;

    [ObservableProperty] public partial DateTimeOffset? Fecha { get; set; }
    [ObservableProperty] public partial Confianza Confianza { get; set; } = Confianza.NoEncontrado;
    [ObservableProperty] public partial string EtiquetaConfianza { get; set; } = string.Empty;

    public DateOnly? Dia => Fecha is DateTimeOffset f ? DateOnly.FromDateTime(f.Date) : null;
}

/// <summary>
/// Renglón de energía del recibo, editable.
/// </summary>
public sealed partial class RangoReciboEditable : ObservableObject
{
    public string Nombre { get; init; } = string.Empty;

    [ObservableProperty] public partial double Kwh { get; set; } = double.NaN;
    [ObservableProperty] public partial double Precio { get; set; } = double.NaN;
    [ObservableProperty] public partial double Importe { get; set; } = double.NaN;
    [ObservableProperty] public partial Confianza Confianza { get; set; } = Confianza.NoEncontrado;
    [ObservableProperty] public partial string EtiquetaConfianza { get; set; } = string.Empty;
}

/// <summary>
/// Aviso de coherencia; con <see cref="AbrirTarifas"/>, la fila ofrece abrir Tarifas.
/// </summary>
public sealed record FilaAvisoRecibo(string Texto, bool AbrirTarifas);

/// <summary>
/// Escanear el recibo: OCR local, análisis en Core (ReciboParser), formulario editable con la confianza
/// de cada dato y avisos de coherencia. Nada se guarda hasta "Guardar lectura"; el texto reconocido no se
/// conserva y el formulario se descarta al salir.
/// </summary>
public sealed partial class EscanearReciboViewModel : ObservableObject
{
    private static readonly TimeSpan EsperaComprobacion = TimeSpan.FromMilliseconds(300);

    private readonly IReciboOcr _ocr;
    private readonly ISelectorArchivos _selector;
    private readonly ILecturaService _lecturas;
    private readonly ITarifaService _tarifas;
    private readonly IResumenDashboardService _resumenes;
    private readonly IValidadorRecibo _validador;
    private readonly ITextos _textos;

    private int? _lugarId;
    private IReadOnlyList<string> _tarifasRegistradas = [];
    private CancellationTokenSource? _lectura;
    private CancellationTokenSource? _comprobacion;
    private bool _llenando;

    public EscanearReciboViewModel(
        IReciboOcr ocr,
        ISelectorArchivos selector,
        ILecturaService lecturas,
        ITarifaService tarifas,
        IResumenDashboardService resumenes,
        IValidadorRecibo validador,
        ITextos textos)
    {
        (_ocr, _selector, _lecturas, _tarifas, _resumenes, _validador, _textos) =
            (ocr, selector, lecturas, tarifas, resumenes, validador, textos);

        Dias = Numero("CampoRecibo_Dias");
        LecturaAnterior = Numero("CampoRecibo_LecturaAnterior");
        LecturaActual = Numero("CampoRecibo_LecturaActual");
        ConsumoKwh = Numero("CampoRecibo_Consumo");
        ImporteEnergia = Numero("CampoRecibo_ImporteEnergia");
        Total = Numero("CampoRecibo_Total");
        PeriodoInicio = Fecha("CampoRecibo_PeriodoInicio");
        PeriodoFin = Fecha("CampoRecibo_PeriodoFin");
        FechaCorte = Fecha("CampoRecibo_FechaCorte");
        FechaLectura = Fecha("CampoRecibo_FechaLectura");
        Numeros = [LecturaAnterior, LecturaActual, ConsumoKwh, Dias, ImporteEnergia, Total];
        Fechas = [FechaLectura, PeriodoInicio, PeriodoFin, FechaCorte];

        CampoReciboNumero Numero(string clave)
        {
            var campo = new CampoReciboNumero(_textos[clave]);
            campo.PropertyChanged += AlCambiarCampo;
            return campo;
        }

        CampoReciboFecha Fecha(string clave)
        {
            var campo = new CampoReciboFecha(_textos[clave]);
            campo.PropertyChanged += AlCambiarCampo;
            return campo;
        }
    }

    // ---- Estado ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ElegirReciboCommand))]
    public partial bool TieneLugar { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarLecturaCommand))]
    public partial bool EsDemo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ElegirReciboCommand))]
    public partial bool Leyendo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarLecturaCommand), nameof(CompararCommand))]
    public partial bool TieneFormulario { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarLecturaCommand))]
    public partial bool Guardando { get; set; }

    [ObservableProperty] public partial bool SinIdioma { get; set; }
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;
    [ObservableProperty] public partial string IdiomaTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string PorQueNoSeGuarda { get; set; } = string.Empty;

    // ---- Formulario ----

    [ObservableProperty] public partial string Tarifa { get; set; } = string.Empty;
    [ObservableProperty] public partial Confianza ConfianzaTarifa { get; set; } = Confianza.NoEncontrado;
    [ObservableProperty] public partial string EtiquetaConfianzaTarifa { get; set; } = string.Empty;

    public CampoReciboNumero Dias { get; }
    public CampoReciboNumero LecturaAnterior { get; }
    public CampoReciboNumero LecturaActual { get; }
    public CampoReciboNumero ConsumoKwh { get; }
    public CampoReciboNumero ImporteEnergia { get; }
    public CampoReciboNumero Total { get; }
    public CampoReciboFecha PeriodoInicio { get; }
    public CampoReciboFecha PeriodoFin { get; }
    public CampoReciboFecha FechaCorte { get; }

    /// <summary>
    /// Fecha con la que se guarda la lectura actual: el fin del periodo facturado, para revisar.
    /// </summary>
    public CampoReciboFecha FechaLectura { get; }

    public IReadOnlyList<CampoReciboNumero> Numeros { get; }

    public IReadOnlyList<CampoReciboFecha> Fechas { get; }

    public ObservableCollection<RangoReciboEditable> Rangos { get; } = [];

    public ObservableCollection<FilaAvisoRecibo> Avisos { get; } = [];

    [ObservableProperty] public partial bool TieneRangos { get; set; }
    [ObservableProperty] public partial bool TieneAvisos { get; set; }
    [ObservableProperty] public partial bool TarifaNoRegistrada { get; set; }

    // ---- Comparación con tu tarifa (opcional) ----

    [ObservableProperty] public partial bool TieneComparacion { get; set; }
    [ObservableProperty] public partial VeredictoRecibo Veredicto { get; set; }
    [ObservableProperty] public partial string VeredictoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string DiferenciaTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string ComparacionDetalle { get; set; } = string.Empty;

    public async Task CargarAsync()
    {
        var lugar = await _lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;
        EsDemo = lugar?.EsDemo == true;
        _tarifasRegistradas = (await _tarifas.ListarConUsoAsync()).Select(t => t.Nombre).ToList();
    }

    private bool PuedeElegir() => TieneLugar && !Leyendo;

    [RelayCommand(CanExecute = nameof(PuedeElegir))]
    private async Task ElegirReciboAsync()
    {
        if (await _selector.ElegirReciboAsync() is { } ruta)
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
        _lectura?.Cancel();
        var cancelacion = _lectura = new CancellationTokenSource();
        Descartar(cancelarLectura: false);
        Leyendo = true;
        try
        {
            var resultado = await _ocr.LeerAsync(ruta, cancelacion.Token);
            if (cancelacion.IsCancellationRequested)
            {
                return;
            }

            IdiomaTexto = resultado.Idioma is { } idioma ? _textos.Formato("Escanear_Idioma", idioma) : string.Empty;
            if (resultado.Error is ErrorOcr error)
            {
                SinIdioma = error == ErrorOcr.SinIdioma;
                Error = SinIdioma ? string.Empty : _textos[$"ErrorOcr_{error}"];
                return;
            }

            // Las líneas reconocidas solo viven aquí: no se guardan ni se registran.
            Llenar(ReciboParser.Analizar(resultado.Lineas));
            if (resultado.Lineas.Count == 0)
            {
                Error = _textos["Escanear_SinTexto"];
            }
        }
        catch (OperationCanceledException)
        {
            // Se eligió otro archivo o se salió de la pantalla.
        }
        finally
        {
            if (_lectura == cancelacion)
            {
                Leyendo = false;
            }
        }
    }

    private void Llenar(ReciboLeido recibo)
    {
        _llenando = true;
        Tarifa = recibo.Tarifa.Valor ?? string.Empty;
        (ConfianzaTarifa, EtiquetaConfianzaTarifa) = (recibo.Tarifa.Confianza, EtiquetaDe(recibo.Tarifa.Confianza));
        PonerNumero(Dias, recibo.Dias);
        PonerNumero(LecturaAnterior, recibo.LecturaAnterior);
        PonerNumero(LecturaActual, recibo.LecturaActual);
        PonerNumero(ConsumoKwh, recibo.ConsumoKwh);
        PonerNumero(ImporteEnergia, recibo.ImporteEnergia);
        PonerNumero(Total, recibo.Total);
        PonerFecha(PeriodoInicio, recibo.PeriodoInicio);
        PonerFecha(PeriodoFin, recibo.PeriodoFin);
        PonerFecha(FechaCorte, recibo.FechaCorte);

        // La fecha de la lectura se deduce del fin del periodo: siempre para revisar.
        PonerFecha(FechaLectura, recibo.PeriodoFin.Valor is null ? CampoFecha.NoEncontrado : recibo.PeriodoFin with { Confianza = Confianza.Baja });

        Rangos.Clear();
        foreach (var rango in recibo.Rangos)
        {
            var peor = new[] { rango.Kwh.Confianza, rango.Precio.Confianza, rango.Importe.Confianza }.Max();
            var editable = new RangoReciboEditable
            {
                Nombre = rango.Nombre,
                Kwh = Doble(rango.Kwh.Valor),
                Precio = Doble(rango.Precio.Valor),
                Importe = Doble(rango.Importe.Valor),
                Confianza = peor,
                EtiquetaConfianza = EtiquetaDe(peor),
            };
            editable.PropertyChanged += AlCambiarCampo;
            Rangos.Add(editable);
        }

        TieneRangos = Rangos.Count > 0;
        _llenando = false;
        TieneFormulario = true;
        Comprobar();

        void PonerNumero(CampoReciboNumero campo, CampoNumero leido)
            => (campo.Valor, campo.Confianza, campo.EtiquetaConfianza) = (Doble(leido.Valor), leido.Confianza, EtiquetaDe(leido.Confianza));
    }

    private void PonerFecha(CampoReciboFecha campo, CampoFecha leido)
        => (campo.Fecha, campo.Confianza, campo.EtiquetaConfianza) = (
            leido.Valor is DateOnly dia ? new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue)) : null,
            leido.Confianza,
            EtiquetaDe(leido.Confianza));

    private static double Doble(decimal? valor) => valor is decimal v ? (double)v : double.NaN;

    private string EtiquetaDe(Confianza confianza) => _textos[$"Confianza_{confianza}"];

    partial void OnTarifaChanged(string value) => ProgramarComprobacion();

    private void AlCambiarCampo(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CampoReciboNumero.Valor) or nameof(CampoReciboFecha.Fecha)
            or nameof(RangoReciboEditable.Kwh) or nameof(RangoReciboEditable.Precio) or nameof(RangoReciboEditable.Importe))
        {
            ProgramarComprobacion();
        }
    }

    private async void ProgramarComprobacion()
    {
        if (_llenando || !TieneFormulario)
        {
            return;
        }

        _comprobacion?.Cancel();
        var cancelacion = _comprobacion = new CancellationTokenSource();
        try
        {
            await Task.Delay(EsperaComprobacion, cancelacion.Token);
            Comprobar();
        }
        catch (OperationCanceledException)
        {
            // Llegó otro cambio.
        }
    }

    /// <summary>
    /// Lo que la persona confirma, con sus correcciones.
    /// </summary>
    private ReciboAConfirmar AConfirmar() => new()
    {
        Tarifa = string.IsNullOrWhiteSpace(Tarifa) ? null : Tarifa.Trim(),
        PeriodoInicio = PeriodoInicio.Dia,
        PeriodoFin = PeriodoFin.Dia,
        Dias = Dias.Decimal,
        LecturaAnterior = LecturaAnterior.Decimal,
        LecturaActual = LecturaActual.Decimal,
        ConsumoKwh = ConsumoKwh.Decimal,
        Rangos = Rangos.Select(r => new RangoAConfirmar
        {
            Nombre = r.Nombre,
            Kwh = double.IsNaN(r.Kwh) ? null : (decimal)r.Kwh,
            Precio = double.IsNaN(r.Precio) ? null : (decimal)r.Precio,
            Importe = double.IsNaN(r.Importe) ? null : (decimal)r.Importe,
        }).ToList(),
        ImporteEnergia = ImporteEnergia.Decimal,
        Total = Total.Decimal,
        FechaCorte = FechaCorte.Dia,
        FechaLectura = FechaLectura.Dia,
    };

    /// <summary>
    /// Avisos de coherencia y si se puede guardar, con lo que hay en el formulario.
    /// </summary>
    public void Comprobar()
    {
        var recibo = AConfirmar();
        Avisos.Clear();
        foreach (var aviso in ReciboCoherencia.Comprobar(recibo, _tarifasRegistradas))
        {
            Avisos.Add(new FilaAvisoRecibo(
                aviso.Codigo == CodigoAvisoRecibo.TarifaNoRegistrada
                    ? _textos.Formato("AvisoRecibo_TarifaNoRegistrada", recibo.Tarifa)
                    : _textos.Formato($"AvisoRecibo_{aviso.Codigo}", aviso.Calculado, aviso.Leido),
                aviso.Codigo == CodigoAvisoRecibo.TarifaNoRegistrada));
        }

        TieneAvisos = Avisos.Count > 0;
        TarifaNoRegistrada = Avisos.Any(a => a.AbrirTarifas);

        var errores = recibo.ANuevaLectura(_lugarId ?? 0).Errores;
        PorQueNoSeGuarda = EsDemo
            ? _textos["Escanear_NoSeGuardaDemo"]
            : string.Join(" ", errores.Select(e => _textos[$"Escanear_Error_{e}"]));
        GuardarLecturaCommand.NotifyCanExecuteChanged();
        CompararCommand.NotifyCanExecuteChanged();
    }

    private bool PuedeGuardar()
        => TieneFormulario && !EsDemo && !Guardando && _lugarId is not null
            && AConfirmar().ANuevaLectura(_lugarId.Value).Errores.Count == 0;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarLecturaAsync()
    {
        if (_lugarId is not int lugarId || AConfirmar().ANuevaLectura(lugarId).Lectura is not { } lectura)
        {
            return;
        }

        Guardando = true;
        try
        {
            var resultado = await _lecturas.GuardarAsync(lectura, DateOnly.FromDateTime(DateTime.Today));
            if (resultado.Rechazo is MotivoRechazoLectura motivo)
            {
                Error = _textos[$"Lectura_Rechazo_{motivo}"];
                return;
            }

            Descartar();
            WeakReferenceMessenger.Default.Send(new EvaluarAvisosMensaje());
            WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, SeccionesLecturas.Guardada(resultado.Cierres.Count)));
        }
        finally
        {
            Guardando = false;
        }
    }

    private bool PuedeComparar() => TieneFormulario && ConsumoKwh.Decimal is > 0m && ImporteEnergia.Decimal is >= 0m;

    /// <summary>
    /// Paso aparte y opcional: el kWh y la energía leídos contra la tarifa de tu lugar. No cambia nada.
    /// </summary>
    [RelayCommand(CanExecute = nameof(PuedeComparar))]
    private async Task CompararAsync()
    {
        if (_lugarId is not int lugarId
            || ConsumoKwh.Decimal is not decimal kwh
            || ImporteEnergia.Decimal is not decimal energia
            || await _resumenes.ObtenerDeLugarAsync(lugarId, DateOnly.FromDateTime(DateTime.Today)) is not { } resumen)
        {
            return;
        }

        var tarifa = (await _tarifas.ListarConUsoAsync()).FirstOrDefault(t => t.Nombre == resumen.NombreTarifa);
        if (tarifa is null || await _tarifas.ObtenerParaEditarAsync(tarifa.Id) is not { } editable)
        {
            return;
        }

        var comparacion = _validador.Comparar(editable.ComoTarifa(), resumen.MesesDelPeriodo, kwh, energia);
        Veredicto = comparacion.Veredicto;
        VeredictoTexto = _textos[$"Validar_Veredicto_{comparacion.Veredicto}"];
        DiferenciaTexto = comparacion.DiferenciaPorcentaje is decimal porcentaje
            ? _textos.Formato("Validar_Diferencia", comparacion.Diferencia, porcentaje * 100)
            : _textos.Formato("Validar_DiferenciaPesos", comparacion.Diferencia);
        ComparacionDetalle = _textos.Formato(
            "Escanear_ComparacionDetalle", tarifa.Nombre, comparacion.SubtotalEnergia, energia);
        TieneComparacion = true;
    }

    [RelayCommand]
    private void Cancelar()
    {
        Descartar();
        WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas));
    }

    [RelayCommand]
    private void IrALecturas() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas));

    [RelayCommand]
    private void AbrirTarifas() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Tarifas));

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    /// <summary>
    /// Olvida lo leído (al salir de la pantalla, cancelar o guardar).
    /// </summary>
    public void Descartar(bool cancelarLectura = true)
    {
        if (cancelarLectura)
        {
            _lectura?.Cancel();
            Leyendo = false;
        }

        _comprobacion?.Cancel();
        _llenando = true;
        Tarifa = string.Empty;
        foreach (var campo in Numeros)
        {
            campo.Valor = double.NaN;
        }

        foreach (var campo in Fechas)
        {
            campo.Fecha = null;
        }

        Rangos.Clear();
        Avisos.Clear();
        _llenando = false;
        TieneRangos = TieneAvisos = TieneFormulario = TieneComparacion = SinIdioma = false;
        Error = PorQueNoSeGuarda = IdiomaTexto = string.Empty;
    }
}
