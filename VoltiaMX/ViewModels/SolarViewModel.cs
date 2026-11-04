using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Simulador solar del lugar principal: supuestos editables (con valores de ejemplo cada vez que se abre),
/// cálculo 300 ms después de cada cambio con <see cref="SimuladorSolar"/> y PDF del último resultado.
/// Nada se guarda.
/// </summary>
public sealed partial class SolarViewModel(
    ISimuladorSolarService solar,
    ILecturaService lecturas,
    IGuardadorArchivos guardador,
    OpcionesPdf fuentes,
    ITextos textos) : ObservableObject
{
    private static readonly TimeSpan EsperaCalculo = TimeSpan.FromMilliseconds(300);

    private DatosLugarSolar _datos = DatosLugarSolar.SinLugar;
    private bool _cargando = true;
    private bool _consumoTocado;
    private CancellationTokenSource? _calculo;
    private (ParametrosSolar Parametros, ResultadoSolar Resultado)? _ultimo;

    [ObservableProperty] public partial bool EsDemo { get; set; }
    [ObservableProperty] public partial bool SinConsumoMedido { get; set; }

    // ---- Supuestos (NaN = vacío) ----

    [ObservableProperty] public partial double Consumo { get; set; } = double.NaN;
    [ObservableProperty] public partial double Porcentaje { get; set; } = (double)ValoresSolarPorDefecto.PorcentajeCubrir;
    [ObservableProperty] public partial double Horas { get; set; } = (double)ValoresSolarPorDefecto.HorasSol;
    [ObservableProperty] public partial double Eficiencia { get; set; } = (double)ValoresSolarPorDefecto.Eficiencia;
    [ObservableProperty] public partial double WattsPanel { get; set; } = (double)ValoresSolarPorDefecto.WattsPanel;
    [ObservableProperty] public partial double Costo { get; set; } = double.NaN;

    [ObservableProperty] public partial bool TieneErrores { get; set; }

    public ObservableCollection<string> Errores { get; } = [];

    // ---- Resultado ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    public partial bool TieneResultado { get; set; }

    [ObservableProperty] public partial string PanelesTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string SinPesosTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneDac { get; set; }
    [ObservableProperty] public partial NivelRiesgoDac DacAntesNivel { get; set; }
    [ObservableProperty] public partial string DacAntesEtiqueta { get; set; } = string.Empty;
    [ObservableProperty] public partial string DacAntesTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial NivelRiesgoDac DacDespuesNivel { get; set; }
    [ObservableProperty] public partial string DacDespuesEtiqueta { get; set; } = string.Empty;
    [ObservableProperty] public partial string DacDespuesTexto { get; set; } = string.Empty;

    public ObservableCollection<FilaDetalle> FilasResultado { get; } = [];

    // ---- PDF ----

    [ObservableProperty] public partial string Referencia { get; set; } = string.Empty;
    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand))]
    public partial bool Exportando { get; set; }

    public async Task CargarAsync()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _datos = await solar.ObtenerDatosAsync(lugar?.Id, hoy);
        EsDemo = _datos.EsDemo;
        SinConsumoMedido = _datos.KwhMedidoPorMes is not > 0m;
        Consumo = _datos.KwhMedidoPorMes is > 0m and decimal medido ? (double)Math.Round(medido, 2) : double.NaN;
        _cargando = false;
        Calcular();
    }

    partial void OnConsumoChanged(double value)
    {
        _consumoTocado |= !_cargando;
        ProgramarCalculo();
    }

    partial void OnPorcentajeChanged(double value) => ProgramarCalculo();

    partial void OnHorasChanged(double value) => ProgramarCalculo();

    partial void OnEficienciaChanged(double value) => ProgramarCalculo();

    partial void OnWattsPanelChanged(double value) => ProgramarCalculo();

    partial void OnCostoChanged(double value) => ProgramarCalculo();

    private async void ProgramarCalculo()
    {
        if (_cargando)
        {
            return;
        }

        _calculo?.Cancel();
        var cancelacion = _calculo = new CancellationTokenSource();
        try
        {
            await Task.Delay(EsperaCalculo, cancelacion.Token);
            Calcular();
        }
        catch (OperationCanceledException)
        {
            // Llegó otro cambio.
        }
    }

    // Un campo obligatorio vacío (NaN) se toma como 0 para que el validador diga qué está mal. Se redondea a
    // 4 decimales para quitar el ruido de convertir un double (0.8000000119 → 0.8).
    private static decimal Numero(double valor) => Opcional(valor) ?? 0m;

    private static decimal? Opcional(double valor)
    {
        if (double.IsNaN(valor))
        {
            return null;
        }

        // Un número enorme no cabe en decimal: se manda fuera de rango para que el validador lo marque.
        if (double.IsInfinity(valor) || Math.Abs(valor) > 1e15)
        {
            return valor < 0 ? decimal.MinValue : decimal.MaxValue;
        }

        return Math.Round((decimal)valor, 4);
    }

    private ParametrosSolar Parametros() => new(
        Opcional(Consumo), Numero(Porcentaje), Numero(Horas), Numero(Eficiencia), Numero(WattsPanel), Opcional(Costo));

    private void Calcular()
    {
        var parametros = Parametros();
        var errores = SimuladorSolarValidador.Validar(parametros);
        Errores.Clear();
        // Al abrir sin consumo medido, la pantalla ya pide escribirlo: el error aparece hasta que se toca el campo.
        foreach (var error in errores.Where(e => _consumoTocado || e != CodigoErrorSolar.ConsumoRequerido))
        {
            Errores.Add(textos[$"Solar_Error_{error}"]);
        }

        TieneErrores = Errores.Count > 0;
        if (errores.Count > 0)
        {
            _ultimo = null;
            TieneResultado = false;
            return;
        }

        var r = solar.Calcular(parametros, _datos);
        _ultimo = (parametros, r);
        Mostrar(r);
    }

    private void Mostrar(ResultadoSolar r)
    {
        PanelesTexto = r.Paneles.ToString(System.Globalization.CultureInfo.InvariantCulture);
        FilasResultado.Clear();
        FilasResultado.Add(new(textos["Solar_FilaKwp"], textos.Formato("Solar_Kwp", r.KwpInstalados)));
        FilasResultado.Add(new(textos["Solar_FilaGeneracion"], textos.Formato("Solar_Kwh", r.GeneracionMensualKwh)));
        FilasResultado.Add(new(textos["Solar_FilaAprovechados"], textos.Formato("Solar_Kwh", r.KwhAprovechadosMes)));
        FilasResultado.Add(new(textos["Solar_FilaExcedente"], textos.Formato("Solar_Kwh", r.ExcedenteKwhMes)));
        if (r.AhorroMes is decimal mes && r.AhorroAnio is decimal anio)
        {
            FilasResultado.Add(new(textos["Solar_FilaAhorroMes"], textos.Formato("Solar_Pesos", mes)));
            FilasResultado.Add(new(textos["Solar_FilaAhorroAnio"], textos.Formato("Solar_Pesos", anio)));
            SinPesosTexto = string.Empty;
        }
        else
        {
            SinPesosTexto = textos[$"Solar_SinPesos_{r.EstadoPesos}"];
        }

        if (r.Inversion is decimal inversion)
        {
            FilasResultado.Add(new(textos["Solar_FilaInversion"], textos.Formato("Solar_Pesos", inversion)));
            FilasResultado.Add(new(textos["Solar_FilaRecuperacion"], GeneradorPdfSolar.Recuperacion(r, Texto)));
        }
        else
        {
            FilasResultado.Add(new(textos["Solar_FilaInversion"], textos["SolarPdf_SinCosto"]));
        }

        TieneDac = r.DacAntes is not null && r.DacDespues is not null;
        if (r is { DacAntes: { } antes, DacDespues: { } despues })
        {
            DacAntesNivel = antes.Nivel;
            DacAntesEtiqueta = textos[$"Dac_{antes.Nivel}"];
            DacAntesTexto = textos.Formato("Simulador_DacPromedio", antes.PromedioMensualKwh);
            DacDespuesNivel = despues.Nivel;
            DacDespuesEtiqueta = textos[$"Dac_{despues.Nivel}"];
            DacDespuesTexto = textos.Formato("Simulador_DacPromedio", despues.PromedioMensualKwh);
        }

        TieneResultado = true;
    }

    private bool PuedeExportar() => TieneResultado && !Exportando;

    [RelayCommand(CanExecute = nameof(PuedeExportar))]
    private async Task ExportarPdfAsync()
    {
        if (_ultimo is not { } ultimo)
        {
            return;
        }

        Aviso = Error = string.Empty;
        Exportando = true;
        try
        {
            // Las cifras del PDF son las de la pantalla: el último resultado mostrado.
            var datos = new DatosPdfSolar(_datos, ultimo.Parametros, ultimo.Resultado, Referencia, DateTime.Now);
            var resultado = await guardador.GuardarAsync(
                $"VoltiaMX-estimacion-solar-{DateTime.Today:yyyy-MM-dd}.pdf",
                TipoArchivo.Pdf,
                () => Task.Run(() => GeneradorPdfSolar.Generar(datos, Texto, fuentes)));
            if (resultado.Ruta is { } ruta)
            {
                Aviso = textos.Formato("Reportes_Guardado", ruta);
            }
            else if (resultado.Error is { } error)
            {
                Error = textos[$"Reportes_Error_{error}"];
            }
        }
        finally
        {
            Exportando = false;
        }
    }

    private string Texto(string clave) => textos[clave];

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

#if DEBUG
    /// <summary>
    /// Solo para pruebas automatizadas: carpeta donde se exporta sin abrir el selector.
    /// </summary>
    public string CarpetaPrueba
    {
        get => guardador.CarpetaPrueba ?? string.Empty;
        set => guardador.CarpetaPrueba = value;
    }
#endif
}
