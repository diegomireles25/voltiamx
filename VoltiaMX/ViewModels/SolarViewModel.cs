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
/// Nada se guarda. Desde la Fase 9e se abre también desde Inicio ("¿Y con paneles solares?"): primero la respuesta
/// (paneles, espacio, producción y ahorro) y los supuestos plegados en "Ajustar supuestos".
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

    // ---- Fase 9e ----

    /// <summary>
    /// Se abrió desde Inicio: "Volver" regresa ahí.
    /// </summary>
    [ObservableProperty] public partial bool DesdeInicio { get; set; }

    [ObservableProperty] public partial bool PreciosDeEjemplo { get; set; }

    /// <summary>
    /// D2: se usa el consumo del lugar ("Estamos usando tu consumo del recibo; puedes cambiarlo.").
    /// </summary>
    [ObservableProperty] public partial bool UsaConsumoDelLugar { get; set; }

    /// <summary>
    /// D2: el consumo de partida sale de un recibo por monto (APROXIMADO).
    /// </summary>
    [ObservableProperty] public partial bool ConsumoAproximado { get; set; }

    /// <summary>
    /// "Ajustar supuestos": plegado si ya hay consumo; sin consumo hay que escribirlo, así que empieza abierto.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TextoAjustar))]
    public partial bool MostrarSupuestos { get; set; }

    public string TextoAjustar => textos[MostrarSupuestos ? "Solar_OcultarSupuestos" : "Solar_AjustarSupuestos"];

    [RelayCommand]
    private void AlternarSupuestos() => MostrarSupuestos = !MostrarSupuestos;

    [RelayCommand]
    private void Volver() => WeakReferenceMessenger.Default.Send(new IrADashboardMensaje());

    [ObservableProperty] public partial double SuperficiePanel { get; set; } = (double)ValoresSolarPorDefecto.SuperficiePanelM2;
    [ObservableProperty] public partial double EspacioPaso { get; set; } = (double)ValoresSolarPorDefecto.EspacioPasoPorciento;

    partial void OnSuperficiePanelChanged(double value) => ProgramarCalculo();

    partial void OnEspacioPasoChanged(double value) => ProgramarCalculo();

    // Filas del resultado sobre el lienzo (E6).
    [ObservableProperty] public partial string EspacioTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProduccionTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string CubreTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string SobraTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneAhorro { get; set; }
    [ObservableProperty] public partial bool AhorroAproximado { get; set; }
    [ObservableProperty] public partial string AhorroMesTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string AhorroAnioTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneInversion { get; set; }
    [ObservableProperty] public partial string InversionTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string RecuperacionTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string PotenciaInstaladaTexto { get; set; } = string.Empty;

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

    /// <summary>
    /// <paramref name="seccion"/> es <see cref="SeccionesInicio.Solar"/> si se abrió desde Inicio.
    /// </summary>
    public async Task CargarAsync(string? seccion = null)
    {
        DesdeInicio = seccion == SeccionesInicio.Solar;
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _datos = await solar.ObtenerDatosAsync(lugar?.Id, hoy);
        EsDemo = _datos.EsDemo;
        PreciosDeEjemplo = _datos.Tarifa?.EsDeEjemplo == true;
        SinConsumoMedido = _datos.KwhMedidoPorMes is not > 0m;
        UsaConsumoDelLugar = !SinConsumoMedido;
        ConsumoAproximado = UsaConsumoDelLugar && _datos.ConsumoAproximado;
        MostrarSupuestos = SinConsumoMedido;
        Consumo = _datos.KwhMedidoPorMes is > 0m and decimal medido ? (double)Math.Round(medido, 2) : double.NaN;
        _cargando = false;
        Calcular();
    }

    partial void OnConsumoChanged(double value)
    {
        _consumoTocado |= !_cargando;

        // Si la persona cambia el consumo, ya no es el del recibo.
        if (!_cargando && _datos.KwhMedidoPorMes is decimal medido && Math.Abs(value - (double)Math.Round(medido, 2)) > 0.001)
        {
            UsaConsumoDelLugar = ConsumoAproximado = false;
        }

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
        Opcional(Consumo), Numero(Porcentaje), Numero(Horas), Numero(Eficiencia), Numero(WattsPanel), Opcional(Costo),
        Numero(SuperficiePanel), Numero(EspacioPaso));

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

        // Fase 9e (E6): las filas sobre el lienzo, el espacio primero.
        EspacioTexto = textos.Formato("Solar_EspacioValor", r.EspacioM2);
        ProduccionTexto = textos.Formato("Solar_Kwh", r.GeneracionMensualKwh);
        CubreTexto = textos.Formato("Solar_Kwh", r.KwhAprovechadosMes);
        SobraTexto = textos.Formato("Solar_Kwh", r.ExcedenteKwhMes);
        TieneAhorro = r.AhorroMes is not null && r.AhorroAnio is not null;
        AhorroAproximado = TieneAhorro && ConsumoAproximado;
        AhorroMesTexto = r.AhorroMes is decimal ahorroMes ? textos.Formato("Solar_Pesos", ahorroMes) : string.Empty;
        AhorroAnioTexto = r.AhorroAnio is decimal ahorroAnio ? textos.Formato("Solar_Pesos", ahorroAnio) : string.Empty;
        // D5: sin costo no hay inversión ni recuperación en pantalla.
        TieneInversion = r.Inversion is not null;
        InversionTexto = r.Inversion is decimal inversionTotal ? textos.Formato("Solar_Pesos", inversionTotal) : string.Empty;
        RecuperacionTexto = TieneInversion ? GeneradorPdfSolar.Recuperacion(r, Texto) : string.Empty;
        PotenciaInstaladaTexto = textos.Formato("Solar_PotenciaInstalada", r.KwpInstalados);

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

    [RelayCommand]
    private void IrATarifas() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Tarifas));

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
