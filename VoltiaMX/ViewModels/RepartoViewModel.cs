using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Un periodo cerrado en el selector.
/// </summary>
public sealed record OpcionPeriodoReparto(PeriodoRepartible Periodo, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Lecturas del submedidor de un inquilino; avisa al ViewModel cuando cambian.
/// </summary>
public sealed partial class FilaLecturaReparto(int inquilinoId, string nombre, Action alCambiar) : ObservableObject
{
    public int InquilinoId { get; } = inquilinoId;

    public string Nombre { get; } = nombre;

    /// <summary>
    /// False al ver un reparto guardado.
    /// </summary>
    public bool Editable { get; init; } = true;

    /// <summary>
    /// Nombres accesibles de los campos ("Lectura anterior de Depto 1").
    /// </summary>
    public string NombreAnterior { get; init; } = string.Empty;

    public string NombreActual { get; init; } = string.Empty;

    [ObservableProperty] public partial double Anterior { get; set; } = double.NaN;
    [ObservableProperty] public partial double Actual { get; set; } = double.NaN;
    [ObservableProperty] public partial string KwhTexto { get; set; } = string.Empty;

    partial void OnAnteriorChanged(double value) => alCambiar();

    partial void OnActualChanged(double value) => alCambiar();
}

/// <summary>
/// Cuota de un inquilino en el resultado; con el reparto guardado se puede exportar su recibo.
/// </summary>
public sealed record FilaCuotaReparto(string Nombre, string Detalle, string Cuota, bool PuedeExportar, IAsyncRelayCommand ExportarRecibo);

/// <summary>
/// Nuevo reparto del lugar principal (o uno guardado, de solo lectura): periodo, total, modo y lecturas de
/// los submedidores. El cálculo es el de <see cref="Repartidor"/>, 300 ms después de cada cambio; al guardar
/// queda una copia fija que se exporta a PDF.
/// </summary>
public sealed partial class RepartoViewModel(
    IRepartoService repartos,
    ILecturaService lecturas,
    IGuardadorArchivos guardador,
    ISelectorArchivos selector,
    IDialogos dialogos,
    OpcionesPdf fuentes,
    ITextos textos) : ObservableObject
{
    private static readonly TimeSpan EsperaCalculo = TimeSpan.FromMilliseconds(300);

    private int? _lugarId;
    private int? _repartoId;
    private bool _cargando;
    private decimal _totalPrecargado;
    private OrigenTotal _origenPrecargado;
    private CancellationTokenSource? _calculo;
    private RepartoGuardado? _guardado;

    [ObservableProperty] public partial string Titulo { get; set; } = string.Empty;
    [ObservableProperty] public partial bool EsDemo { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Editable))]
    public partial bool SoloLectura { get; set; }

    public bool Editable => !SoloLectura;

    [ObservableProperty] public partial bool HayPeriodos { get; set; } = true;
    [ObservableProperty] public partial bool SinPeriodos { get; set; }
    [ObservableProperty] public partial bool SinInquilinos { get; set; }
    [ObservableProperty] public partial IReadOnlyList<OpcionPeriodoReparto> Periodos { get; set; } = [];
    [ObservableProperty] public partial OpcionPeriodoReparto? PeriodoElegido { get; set; }
    [ObservableProperty] public partial string PeriodoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string PrincipalTexto { get; set; } = string.Empty;

    [ObservableProperty] public partial double Total { get; set; } = double.NaN;
    [ObservableProperty] public partial OrigenTotal Origen { get; set; }
    [ObservableProperty] public partial string OrigenTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool OrigenCalculado { get; set; }
    [ObservableProperty] public partial bool OrigenOtro { get; set; }

    [ObservableProperty] public partial bool? ModoSoloMedido { get; set; } = true;
    [ObservableProperty] public partial bool? ModoProporcional { get; set; } = false;

    // ---- Resultado ----

    [ObservableProperty] public partial bool TieneResultado { get; set; }
    [ObservableProperty] public partial string TotalCobradoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string AbsorbeTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string PrecioPromedioTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneAvisos { get; set; }
    [ObservableProperty] public partial string FaltanLecturas { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool PuedeGuardarCalculo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(ExportarPdfCommand), nameof(ExportarTodosCommand))]
    public partial bool Trabajando { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand), nameof(ExportarTodosCommand))]
    public partial bool Guardado { get; set; }

    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;

    public ObservableCollection<FilaLecturaReparto> Filas { get; } = [];

    public ObservableCollection<FilaCuotaReparto> Cuotas { get; } = [];

    public ObservableCollection<string> Avisos { get; } = [];

    /// <summary>
    /// Con <paramref name="repartoId"/> abre ese reparto guardado en solo lectura; sin él, un reparto nuevo.
    /// </summary>
    public async Task CargarAsync(int? repartoId)
    {
        if (repartoId is int id && await repartos.ObtenerAsync(id) is { } guardado)
        {
            MostrarGuardado(guardado);
            return;
        }

        Titulo = textos["Reparto_TituloNuevo"];
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        EsDemo = lugar?.EsDemo == true;
        if (_lugarId is int lugarId)
        {
            Preparar(await repartos.PrepararAsync(lugarId));
        }
        else
        {
            HayPeriodos = false;
            SinPeriodos = true;
        }
    }

    private void Preparar(PreparacionReparto p)
    {
        _cargando = true;
        Periodos = p.Periodos.Select(x => new OpcionPeriodoReparto(x, TextoPeriodo(x))).ToList();
        PeriodoElegido = Periodos.FirstOrDefault(o => o.Periodo == p.Periodo);
        HayPeriodos = p.Periodo is not null;
        SinPeriodos = !HayPeriodos;
        PrincipalTexto = p.Periodo is { } periodo ? textos.Formato("Reparto_Principal", periodo.KwhTotales) : string.Empty;

        _totalPrecargado = p.Total;
        _origenPrecargado = p.Origen;
        Total = (double)p.Total;
        PonerOrigen(p.Origen);

        Filas.Clear();
        foreach (var fila in p.Filas)
        {
            Filas.Add(new FilaLecturaReparto(fila.InquilinoId, fila.Nombre, ProgramarCalculo)
            {
                NombreAnterior = textos.Formato("Reparto_LecturaAnteriorDe", fila.Nombre),
                NombreActual = textos.Formato("Reparto_LecturaActualDe", fila.Nombre),
                Anterior = fila.LecturaAnterior is { } a ? (double)a : double.NaN,
                Actual = fila.LecturaActual is { } b ? (double)b : double.NaN,
            });
        }

        SinInquilinos = HayPeriodos && Filas.Count == 0;
        _cargando = false;
        Guardado = false;
        _repartoId = null;
        Calcular();
    }

    private void MostrarGuardado(RepartoGuardado r)
    {
        _cargando = true;
        _guardado = r;
        _repartoId = r.Id;
        _lugarId = r.LugarId;
        SoloLectura = true;
        Guardado = true;
        EsDemo = r.EsDemo;
        Titulo = textos["Reparto_TituloGuardado"];
        PeriodoTexto = $"{FormatoReporte.Fecha(r.Inicio)} – {FormatoReporte.Fecha(r.Fin)}";
        PrincipalTexto = textos.Formato("Reparto_Principal", r.KwhPrincipal);
        Total = (double)r.TotalRepartido;
        PonerOrigen(r.Origen);
        ModoSoloMedido = r.Modo == ModoReparto.SoloMedido;
        ModoProporcional = !ModoSoloMedido;
        Filas.Clear();
        foreach (var linea in r.Lineas)
        {
            Filas.Add(new FilaLecturaReparto(linea.InquilinoId ?? 0, linea.Nombre, ProgramarCalculo)
            {
                NombreAnterior = textos.Formato("Reparto_LecturaAnteriorDe", linea.Nombre),
                NombreActual = textos.Formato("Reparto_LecturaActualDe", linea.Nombre),
                Anterior = (double)linea.LecturaAnterior,
                Actual = (double)linea.LecturaActual,
                KwhTexto = textos.Formato("Reparto_Kwh", linea.Kwh),
                Editable = false,
            });
        }

        // Las cifras salen de la copia fija: no se recalculan.
        MostrarResultado(
            r.Lineas.Select(l => (l.Nombre, l.Kwh, l.Cuota)).ToList(),
            r.TotalCobrado, r.AbsorbePropietario, r.PrecioPromedioKwh, r.Avisos);
        _cargando = false;
    }

    partial void OnPeriodoElegidoChanged(OpcionPeriodoReparto? value)
    {
        if (_cargando || value is null || _lugarId is not int lugarId || SoloLectura)
        {
            return;
        }

        _ = CambiarPeriodoAsync(lugarId, value.Periodo.Fin);
    }

    private async Task CambiarPeriodoAsync(int lugarId, DateOnly fin)
    {
        var modo = ModoSoloMedido;
        Preparar(await repartos.PrepararAsync(lugarId, fin));
        ModoSoloMedido = modo;
        ModoProporcional = !modo;
    }

    partial void OnTotalChanged(double value)
    {
        if (_cargando || SoloLectura)
        {
            return;
        }

        // Si la persona cambia el total precargado, el origen pasa a "capturado".
        PonerOrigen(!double.IsNaN(value) && (decimal)value == _totalPrecargado ? _origenPrecargado : OrigenTotal.Capturado);
        ProgramarCalculo();
    }

    partial void OnModoSoloMedidoChanged(bool? value) => ProgramarCalculo();

    private void PonerOrigen(OrigenTotal origen)
    {
        Origen = origen;
        OrigenTexto = textos[$"Reparto_Origen_{origen}"];
        OrigenCalculado = origen == OrigenTotal.MontoCalculado;
        OrigenOtro = !OrigenCalculado;
    }

    private string TextoPeriodo(PeriodoRepartible p)
        => textos.Formato(p.TieneReparto ? "Reparto_OpcionPeriodoConReparto" : "Reparto_OpcionPeriodo",
            FormatoReporte.Fecha(p.Inicio), FormatoReporte.Fecha(p.Fin), p.KwhTotales);

    private async void ProgramarCalculo()
    {
        if (_cargando || SoloLectura)
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

    /// <summary>
    /// Calcula sin esperar.
    /// </summary>
    public void CalcularAhora()
    {
        _calculo?.Cancel();
        Calcular();
    }

    private ModoReparto Modo => ModoSoloMedido == true ? ModoReparto.SoloMedido : ModoReparto.ProporcionalAlMedido;

    private void Calcular()
    {
        if (SoloLectura)
        {
            return;
        }

        Guardado = false;
        if (PeriodoElegido is not { } opcion || Filas.Count == 0)
        {
            Limpiar();
            return;
        }

        var faltan = Filas.Any(f => double.IsNaN(f.Anterior) || double.IsNaN(f.Actual));
        foreach (var f in Filas)
        {
            f.KwhTexto = double.IsNaN(f.Anterior) || double.IsNaN(f.Actual) ? string.Empty : textos.Formato("Reparto_Kwh", (decimal)(f.Actual - f.Anterior));
        }

        if (faltan)
        {
            Limpiar();
            FaltanLecturas = textos["Reparto_FaltanLecturas"];
            return;
        }

        var resultado = Repartidor.Calcular(
            double.IsNaN(Total) ? 0m : (decimal)Total,
            Modo,
            opcion.Periodo.KwhTotales,
            Filas.Select(f => new LecturasSubmedidor((decimal)f.Anterior, (decimal)f.Actual)).ToList());

        FaltanLecturas = string.Empty;
        var calculado = resultado.Avisos.Any(Repartidor.Bloquea)
            ? []
            : Filas.Select((f, i) => (f.Nombre, resultado.Kwh[i], resultado.Cuotas[i])).ToList();
        MostrarResultado(calculado, resultado.TotalCobrado, resultado.AbsorbePropietario, resultado.PrecioPromedioKwh, resultado.Avisos);
        PuedeGuardarCalculo = resultado.PuedeGuardar;
    }

    private void Limpiar()
    {
        MostrarResultado([], 0m, 0m, null, []);
        PuedeGuardarCalculo = false;
        FaltanLecturas = string.Empty;
    }

    private void MostrarResultado(
        IReadOnlyList<(string Nombre, decimal Kwh, decimal Cuota)> cuotas, decimal cobrado, decimal absorbe, decimal? precio, IReadOnlyList<AvisoReparto> avisos)
    {
        Cuotas.Clear();
        for (var i = 0; i < cuotas.Count; i++)
        {
            var indice = i;
            Cuotas.Add(new FilaCuotaReparto(
                cuotas[i].Nombre,
                textos.Formato("Reparto_Kwh", cuotas[i].Kwh),
                textos.Formato("Reparto_Pesos", cuotas[i].Cuota),
                Guardado,
                new AsyncRelayCommand(() => ExportarReciboAsync(indice))));
        }

        TieneResultado = Cuotas.Count > 0;
        TotalCobradoTexto = textos.Formato("Reparto_Pesos", cobrado);
        AbsorbeTexto = textos.Formato("Reparto_Pesos", absorbe);
        PrecioPromedioTexto = precio is { } p ? textos.Formato("Reparto_PrecioKwh", p) : string.Empty;

        Avisos.Clear();
        foreach (var aviso in avisos)
        {
            Avisos.Add(textos[$"Reparto_Aviso_{aviso}"]);
        }

        TieneAvisos = Avisos.Count > 0;
    }

    private bool PuedeGuardar() => PuedeGuardarCalculo && !Trabajando && !SoloLectura;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        CalcularAhora();
        if (!PuedeGuardarCalculo || _lugarId is not int lugarId || PeriodoElegido is not { } opcion)
        {
            return;
        }

        var entrada = new EntradaReparto(
            lugarId, opcion.Periodo.Inicio, opcion.Periodo.Fin, (decimal)Total, Origen, Modo,
            Filas.Select(f => new LecturaInquilino(f.InquilinoId, (decimal)f.Anterior, (decimal)f.Actual)).ToList());

        Aviso = Error = string.Empty;
        Trabajando = true;
        try
        {
            var resultado = await repartos.GuardarAsync(entrada, reemplazar: false);
            if (resultado.Estado == EstadoGuardarReparto.YaExiste)
            {
                if (!await dialogos.ConfirmarAsync(
                        textos["Reparto_ReemplazarTitulo"],
                        textos.Formato("Reparto_ReemplazarTexto", FormatoReporte.Fecha(opcion.Periodo.Inicio), FormatoReporte.Fecha(opcion.Periodo.Fin)),
                        textos["Reparto_Reemplazar"],
                        textos["Eliminar_Cancelar"]))
                {
                    return;
                }

                resultado = await repartos.GuardarAsync(entrada, reemplazar: true);
            }

            if (resultado.Estado != EstadoGuardarReparto.Guardado || resultado.Id is not int id)
            {
                Error = textos["Reparto_NoSeGuardo"];
                return;
            }

            _repartoId = id;
            _guardado = await repartos.ObtenerAsync(id);
            Guardado = true;
            var cuotas = Cuotas.ToList();
            Cuotas.Clear();
            foreach (var c in cuotas)
            {
                Cuotas.Add(c with { PuedeExportar = true });
            }

            Aviso = textos["Reparto_Guardado"];
        }
        catch (Exception)
        {
            // Por ejemplo, un inquilino que se eliminó mientras tanto: el reparto no se guarda.
            Error = textos["Reparto_NoSeGuardo"];
        }
        finally
        {
            Trabajando = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Inquilinos));

    private bool PuedeExportar() => Guardado && !Trabajando;

    [RelayCommand(CanExecute = nameof(PuedeExportar))]
    private async Task ExportarPdfAsync()
    {
        if (_guardado is not { } r)
        {
            return;
        }

        Mostrar(await guardador.GuardarAsync(NombreArchivo.Resumen(r.Fin), TipoArchivo.Pdf,
            () => Task.Run(() => GeneradorPdfReparto.Resumen(r, Texto, fuentes))));
    }

    private async Task ExportarReciboAsync(int indice)
    {
        if (_guardado is not { } r || indice >= r.Lineas.Count)
        {
            return;
        }

        Mostrar(await guardador.GuardarAsync(NombreArchivo.Recibo(r.Lineas[indice].Nombre, r.Fin), TipoArchivo.Pdf,
            () => Task.Run(() => GeneradorPdfReparto.Recibo(r, indice, Texto, fuentes))));
    }

    [RelayCommand(CanExecute = nameof(PuedeExportar))]
    private async Task ExportarTodosAsync()
    {
        if (_guardado is not { } r)
        {
            return;
        }

        Aviso = Error = string.Empty;
        var carpeta = await selector.ElegirCarpetaAsync();
        if (carpeta is null)
        {
            return;
        }

        Trabajando = true;
        try
        {
            var archivos = r.Lineas
                .Select((l, i) => (NombreArchivo.Recibo(l.Nombre, r.Fin), (Func<Task<byte[]>>)(() => Task.Run(() => GeneradorPdfReparto.Recibo(r, i, Texto, fuentes)))))
                .Append((NombreArchivo.Resumen(r.Fin), () => Task.Run(() => GeneradorPdfReparto.Resumen(r, Texto, fuentes))))
                .ToList();
            var resultado = await guardador.GuardarEnCarpetaAsync(carpeta, archivos);
            if (resultado.Error is { } error)
            {
                Error = textos[$"Reportes_Error_{error}"];
            }
            else
            {
                Aviso = textos.Formato("Reparto_ExportadosTodos", archivos.Count, carpeta);
            }
        }
        finally
        {
            Trabajando = false;
        }
    }

    private void Mostrar(ResultadoGuardado resultado)
    {
        Aviso = Error = string.Empty;
        if (resultado.Ruta is { } ruta)
        {
            Aviso = textos.Formato("Reportes_Guardado", ruta);
        }
        else if (resultado.Error is { } error)
        {
            Error = textos[$"Reportes_Error_{error}"];
        }
    }

    private string Texto(string clave) => textos[clave];

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

#if DEBUG
    /// <summary>
    /// Solo para pruebas automatizadas: exporta a esta carpeta sin abrir los selectores.
    /// </summary>
    public string CarpetaPrueba
    {
        get => guardador.CarpetaPrueba ?? string.Empty;
        set
        {
            guardador.CarpetaPrueba = value;
            selector.CarpetaPrueba = value;
        }
    }
#endif
}
