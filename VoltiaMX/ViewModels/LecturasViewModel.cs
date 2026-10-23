using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

public enum TipoEtiquetaLectura
{
    Estimado,
    Corte,
    Alerta,
}

/// <summary>
/// Fila de la vista previa. <see cref="EnTarjetaOscura"/>: la vista previa cierra periodos y va en
/// tarjeta negra.
/// </summary>
public sealed record FilaVistaPrevia(
    string Titulo, string? Detalle, string? Etiqueta, TipoEtiquetaLectura TipoEtiqueta, bool EnTarjetaOscura)
{
    public bool TieneDetalle => !string.IsNullOrEmpty(Detalle);

    public bool TieneEtiqueta => Etiqueta is not null;
}

/// <summary>
/// Fila de "Últimas lecturas".
/// </summary>
public sealed record FilaLectura(string Fecha, string Valor, string Consumo, string? Etiqueta, TipoEtiquetaLectura TipoEtiqueta)
{
    public bool TieneEtiqueta => Etiqueta is not null;
}

/// <summary>
/// Barra de la gráfica del historial (Helpers/Graficas la convierte en series de LiveCharts2).
/// </summary>
public sealed record BarraPeriodo(string Etiqueta, double Valor, string ValorTexto, bool EsActual, bool Estimado);

/// <summary>
/// Punto de una serie del periodo actual: <paramref name="Dia"/> cuenta desde el corte anterior.
/// </summary>
public sealed record PuntoSerie(double Dia, double Valor);

/// <summary>
/// Consumo acumulado del periodo actual y la recta del presupuesto (vacía si no hay presupuesto).
/// </summary>
public sealed record GraficaAcumulado(
    IReadOnlyList<PuntoSerie> Consumo,
    IReadOnlyList<PuntoSerie> Presupuesto,
    int DiasDelPeriodo,
    string FormatoDia,
    string FormatoKwh,
    IFormatProvider Cultura);

/// <summary>
/// Fila de la versión en texto de la gráfica del historial.
/// </summary>
public sealed record FilaHistorial(string Periodo, string Kwh, string Pesos, string KwhPorDia, string? Etiqueta, TipoEtiquetaLectura TipoEtiqueta)
{
    public bool TieneEtiqueta => Etiqueta is not null;
}

/// <summary>
/// Secciones de LecturasPage a las que se puede navegar directo (IrAModuloMensaje.Seccion).
/// </summary>
public static class SeccionesLecturas
{
    public const string Historial = "Historial";

    /// <summary>
    /// Escanear recibo: se abre bajo Lecturas (no es un módulo del menú).
    /// </summary>
    public const string Escanear = "Escanear";

    private const string PrefijoGuardada = "Guardada:";

    /// <summary>
    /// Volver a Lecturas después de guardar una lectura escaneada, con su aviso.
    /// </summary>
    public static string Guardada(int cierres) => PrefijoGuardada + cierres;

    public static int? CierresDe(string? seccion)
        => seccion?.StartsWith(PrefijoGuardada, StringComparison.Ordinal) == true
            && int.TryParse(seccion[PrefijoGuardada.Length..], out var cierres)
                ? cierres
                : null;
}

/// <summary>
/// Captura de lecturas del lugar principal (el mismo del Dashboard): formulario, vista previa de lo que
/// pasará al guardar y las últimas lecturas.
/// </summary>
public sealed partial class LecturasViewModel(
    ILecturaService lecturas,
    IHistorialService historiales,
    ITextos textos,
    IDialogos dialogos) : ObservableObject
{
    private const int LecturasEnLista = 10;
    private const int PeriodosCerradosEnGrafica = 12;
    private const int PeriodosParaHistorial = 2;
    private static readonly CultureInfo Cultura = new("es-MX");
    private static readonly TimeSpan EsperaVistaPrevia = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan DuracionAviso = TimeSpan.FromSeconds(6);

    private int? _lugarId;
    private IReadOnlyList<LecturaListada> _ultimas = [];
    private CancellationTokenSource? _vistaPrevia;
    private CancellationTokenSource? _aviso;

    [ObservableProperty] public partial bool TieneLugar { get; set; }
    [ObservableProperty] public partial bool EsDemo { get; set; }
    [ObservableProperty] public partial bool TieneLecturas { get; set; }
    [ObservableProperty] public partial bool TieneVistaPrevia { get; set; }
    [ObservableProperty] public partial bool VistaPreviaConCierres { get; set; }
    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(PrevisualizarCommand))]
    public partial DateTimeOffset? Fecha { get; set; } = FechaDeHoy();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(PrevisualizarCommand))]
    public partial double ValorKwh { get; set; } = double.NaN;

    [ObservableProperty] public partial double MontoRecibo { get; set; } = double.NaN;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Guardando { get; set; }

    /// <summary>
    /// No se aceptan lecturas con fecha futura.
    /// </summary>
    public DateTimeOffset FechaMaxima { get; } = FechaDeHoy();

    public ObservableCollection<FilaVistaPrevia> VistaPrevia { get; } = [];

    public ObservableCollection<FilaLectura> Lecturas { get; } = [];

    // Historial
    private HistorialLugar? _historial;

    [ObservableProperty] public partial bool TieneHistorial { get; set; }
    [ObservableProperty] public partial IReadOnlyList<BarraPeriodo> BarrasHistorial { get; set; } = [];
    [ObservableProperty] public partial bool? HistorialEnKwh { get; set; } = true;
    [ObservableProperty] public partial bool? HistorialEnPesos { get; set; } = false;
    [ObservableProperty] public partial bool TieneAcumulado { get; set; }
    [ObservableProperty] public partial GraficaAcumulado? Acumulado { get; set; }
    [ObservableProperty] public partial string ResumenAcumulado { get; set; } = string.Empty;

    public ObservableCollection<FilaHistorial> FilasHistorial { get; } = [];

    partial void OnHistorialEnPesosChanged(bool? value) => ArmarBarras();

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    private static DateTimeOffset FechaDeHoy() => new(DateTime.Today);

    public async Task CargarAsync()
    {
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;
        EsDemo = lugar?.EsDemo == true;
        LimpiarFormulario();
        await CargarListaAsync();
        await CargarHistorialAsync();
    }

    private bool ValorValido() => _lugarId is not null && Fecha is not null && !double.IsNaN(ValorKwh) && ValorKwh >= 0;

    private bool PuedeGuardar() => ValorValido() && !Guardando;

    partial void OnFechaChanged(DateTimeOffset? value) => _ = ProgramarVistaPreviaAsync();

    partial void OnValorKwhChanged(double value) => _ = ProgramarVistaPreviaAsync();

    /// <summary>
    /// La vista previa se calcula sola 400 ms después del último cambio de fecha o valor.
    /// </summary>
    private async Task ProgramarVistaPreviaAsync()
    {
        _vistaPrevia?.Cancel();
        if (!ValorValido())
        {
            LimpiarVistaPrevia();
            return;
        }

        var cancelacion = _vistaPrevia = new CancellationTokenSource();
        try
        {
            await Task.Delay(EsperaVistaPrevia, cancelacion.Token);
            await PrevisualizarAhoraAsync(cancelacion.Token);
        }
        catch (OperationCanceledException)
        {
            // Llegó otro cambio: esa vista previa ya no sirve.
        }
    }

    [RelayCommand(CanExecute = nameof(ValorValido))]
    private Task PrevisualizarAsync()
    {
        _vistaPrevia?.Cancel();
        return PrevisualizarAhoraAsync(CancellationToken.None);
    }

    private async Task PrevisualizarAhoraAsync(CancellationToken cancellationToken)
    {
        if (Entrada() is not { } entrada)
        {
            return;
        }

        var resultado = await lecturas.PrevisualizarAsync(entrada, Hoy, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        MostrarVistaPrevia(resultado, entrada);
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (Entrada() is not { } entrada)
        {
            return;
        }

        _vistaPrevia?.Cancel();
        Guardando = true;
        try
        {
            var resultado = await lecturas.GuardarAsync(entrada, Hoy);
            if (!resultado.Aceptada)
            {
                MostrarVistaPrevia(resultado, entrada);
                return;
            }

            LimpiarFormulario();
            await CargarListaAsync();
            await CargarHistorialAsync();
            AvisarGuardada(resultado.Cierres.Count);
        }
        finally
        {
            Guardando = false;
        }
    }

    [RelayCommand]
    private async Task EliminarUltimaAsync()
    {
        if (_lugarId is not int lugarId || _ultimas.Count == 0)
        {
            return;
        }

        var ultima = _ultimas[0];
        if (ultima.EsCorteDePeriodo)
        {
            await AvisarQueNoSePuedeEliminarAsync();
            return;
        }

        var confirmada = await dialogos.ConfirmarAsync(
            textos["Eliminar_Titulo"],
            textos.Formato("Eliminar_Texto", ultima.Fecha, ultima.ValorKwh),
            textos["Eliminar_Confirmar"],
            textos["Eliminar_Cancelar"]);
        if (!confirmada)
        {
            return;
        }

        if (await lecturas.EliminarUltimaAsync(lugarId) == ResultadoEliminacion.EsCorteDePeriodoCerrado)
        {
            await AvisarQueNoSePuedeEliminarAsync();
            return;
        }

        await CargarListaAsync();
        await CargarHistorialAsync();
        MostrarAviso(textos["Lectura_Eliminada"]);
        _ = ProgramarVistaPreviaAsync();
    }

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    private Task AvisarQueNoSePuedeEliminarAsync()
        => dialogos.AvisarAsync(textos["Eliminar_NoPermitidoTitulo"], textos["Eliminar_NoPermitidoTexto"], textos["Dialogo_Entendido"]);

    private NuevaLectura? Entrada()
    {
        if (!ValorValido() || _lugarId is not int lugarId || Fecha is not DateTimeOffset fecha)
        {
            return null;
        }

        decimal? montoRecibo = double.IsNaN(MontoRecibo) || MontoRecibo < 0 ? null : (decimal)MontoRecibo;
        return new NuevaLectura(lugarId, DateOnly.FromDateTime(fecha.Date), (decimal)ValorKwh, OrigenLectura.Manual, montoRecibo);
    }

    private void LimpiarFormulario()
    {
        _vistaPrevia?.Cancel();
        Fecha = FechaDeHoy();
        ValorKwh = double.NaN;
        MontoRecibo = double.NaN;
        LimpiarVistaPrevia();
    }

    private void LimpiarVistaPrevia()
    {
        VistaPrevia.Clear();
        TieneVistaPrevia = false;
        VistaPreviaConCierres = false;
    }

    private void MostrarVistaPrevia(ResultadoLectura resultado, NuevaLectura entrada)
    {
        var oscura = resultado.Aceptada && resultado.Cierres.Count > 0;
        VistaPrevia.Clear();

        void Agregar(string titulo, string? detalle = null, string? etiqueta = null, TipoEtiquetaLectura tipo = TipoEtiquetaLectura.Estimado)
            => VistaPrevia.Add(new FilaVistaPrevia(titulo, detalle, etiqueta, tipo, oscura));

        if (resultado.Rechazo is MotivoRechazoLectura motivo)
        {
            Agregar(textos["Previa_NoSeGuarda"], textos[$"Lectura_Rechazo_{motivo}"], textos["Etiqueta_Revisa"], TipoEtiquetaLectura.Alerta);
        }
        else
        {
            if (resultado.Reemplaza)
            {
                Agregar(textos["Previa_Reemplaza"]);
            }

            foreach (var cierre in resultado.Cierres)
            {
                Agregar(
                    textos.Formato("Previa_Cierre", cierre.Inicio, cierre.Fin),
                    textos.Formato("Previa_CierreDetalle", cierre.Kwh, cierre.Monto),
                    cierre.Estimado ? textos["Etiqueta_Estimado"] : null);
            }

            if (resultado.SinLecturaBase)
            {
                Agregar(textos["Previa_SinBaseTitulo"], textos["Previa_SinBase"]);
            }
            else if (resultado.EsPuntoDePartida && resultado.Cierres.Count == 0)
            {
                Agregar(textos["Previa_PuntoDePartidaTitulo"], textos["Previa_PuntoDePartida"]);
            }
            else if (resultado.Cierres.Count == 0)
            {
                var anterior = _ultimas.FirstOrDefault(l => l.Fecha < entrada.Fecha);
                Agregar(
                    textos["Previa_SeSuma"],
                    anterior is null ? null : textos.Formato("Previa_Consumo", entrada.ValorKwh - anterior.ValorKwh, anterior.Fecha));
            }
        }

        VistaPreviaConCierres = oscura;
        TieneVistaPrevia = true;
    }

    private async Task CargarListaAsync()
    {
        _ultimas = _lugarId is int lugarId ? await lecturas.ListarAsync(lugarId, LecturasEnLista) : [];
        Lecturas.Clear();
        foreach (var lectura in _ultimas)
        {
            var (etiqueta, tipo) = lectura switch
            {
                { EsEstimada: true } => (textos["Etiqueta_Estimada"], TipoEtiquetaLectura.Estimado),
                { EsCorteDePeriodo: true } => (textos["Etiqueta_Corte"], TipoEtiquetaLectura.Corte),
                _ => ((string?)null, TipoEtiquetaLectura.Estimado),
            };

            Lecturas.Add(new FilaLectura(
                textos.Formato("Formato_Fecha", lectura.Fecha),
                textos.Formato("Formato_KwhUnidad", lectura.ValorKwh),
                lectura.ConsumoDesdeAnterior is decimal consumo
                    ? textos.Formato("Formato_ConsumoLectura", consumo)
                    : textos["Lecturas_PuntoDePartida"],
                etiqueta,
                tipo));
        }

        TieneLecturas = Lecturas.Count > 0;
    }

    private async Task CargarHistorialAsync()
    {
        _historial = _lugarId is int lugarId ? await historiales.ObtenerAsync(lugarId, Hoy) : null;
        TieneHistorial = _historial?.PeriodosCerrados >= PeriodosParaHistorial;
        ArmarBarras();
        ArmarTabla();
        ArmarAcumulado();
    }

    /// <summary>
    /// Los últimos periodos cerrados y el actual, en kWh o en pesos.
    /// </summary>
    private IReadOnlyList<PeriodoDeHistorial> PeriodosEnGrafica()
    {
        if (!TieneHistorial || _historial is null)
        {
            return [];
        }

        var cerrados = _historial.Periodos.Where(p => !p.EsActual).TakeLast(PeriodosCerradosEnGrafica);
        return [.. cerrados, .. _historial.Periodos.Where(p => p.EsActual)];
    }

    private void ArmarBarras()
    {
        var enPesos = HistorialEnPesos == true;
        BarrasHistorial = PeriodosEnGrafica()
            .Select(p =>
            {
                var valor = enPesos ? p.Monto : p.Kwh;
                var texto = textos.Formato(enPesos ? "Formato_Monto" : "Formato_Kwh", valor);
                return new BarraPeriodo(
                    EtiquetaDePeriodo(p),
                    (double)valor,
                    p.EsActual ? textos.Formato("Historial_ValorProyectado", texto) : texto,
                    p.EsActual,
                    p.Estimado);
            })
            .ToList();
    }

    private string EtiquetaDePeriodo(PeriodoDeHistorial periodo)
    {
        if (periodo.EsActual)
        {
            return textos["Historial_EtiquetaActual"];
        }

        var corte = textos.Formato("Historial_FechaCorte", periodo.Fin).TrimEnd('.').ToUpper(Cultura);
        return periodo.Estimado ? textos.Formato("Historial_EtiquetaEstimado", corte) : corte;
    }

    private void ArmarTabla()
    {
        FilasHistorial.Clear();
        foreach (var periodo in PeriodosEnGrafica())
        {
            var monto = textos.Formato("Formato_Monto", periodo.Monto);
            var (etiqueta, tipo) = periodo switch
            {
                { EsActual: true } => (textos["Etiqueta_EnCurso"], TipoEtiquetaLectura.Corte),
                { Estimado: true } => (textos["Etiqueta_Estimado"], TipoEtiquetaLectura.Estimado),
                _ => ((string?)null, TipoEtiquetaLectura.Estimado),
            };

            FilasHistorial.Add(new FilaHistorial(
                textos.Formato("Historial_Periodo", periodo.Inicio, periodo.Fin),
                textos.Formato("Formato_KwhUnidad", periodo.Kwh),
                periodo.MontoRecibo is decimal recibo ? textos.Formato("Historial_MontoConRecibo", periodo.Monto, recibo) : monto,
                textos.Formato("Historial_KwhPorDia", periodo.KwhPorDia),
                etiqueta,
                tipo));
        }
    }

    private void ArmarAcumulado()
    {
        if (_historial is not { AcumuladoActual.Count: >= 2 } historial)
        {
            Acumulado = null;
            TieneAcumulado = false;
            ResumenAcumulado = string.Empty;
            return;
        }

        // El día 0 es el corte anterior: ahí empieza la cuenta del periodo.
        var corteAnterior = historial.PeriodoActual.Inicio.AddDays(-1);
        var consumo = historial.AcumuladoActual
            .Select(p => new PuntoSerie(p.Fecha.DayNumber - corteAnterior.DayNumber, (double)p.Kwh))
            .ToList();
        var dias = historial.PeriodoActual.Dias;
        IReadOnlyList<PuntoSerie> presupuesto = historial.KwhPresupuestoPeriodo is decimal tope
            ? [new PuntoSerie(0, 0), new PuntoSerie(dias, (double)tope)]
            : [];

        Acumulado = new GraficaAcumulado(consumo, presupuesto, dias, textos["Grafica_FormatoDia"], textos["Grafica_FormatoKwh"], Cultura);
        var ultimo = historial.AcumuladoActual[^1];
        ResumenAcumulado = historial.KwhPresupuestoPeriodo is decimal maximo
            ? textos.Formato("Historial_AcumuladoConPresupuesto", ultimo.Kwh, maximo)
            : textos.Formato("Historial_AcumuladoSinPresupuesto", ultimo.Kwh, consumo[^1].Dia);
        TieneAcumulado = true;
    }

    /// <summary>
    /// El aviso estándar de lectura guardada (también al volver de Escanear recibo).
    /// </summary>
    public void AvisarGuardada(int cierres) => MostrarAviso(cierres switch
    {
        0 => textos["Lectura_Guardada"],
        1 => textos["Lectura_GuardadaUnCierre"],
        _ => textos.Formato("Lectura_GuardadaCierres", cierres),
    });

    [RelayCommand]
    private void EscanearRecibo()
        => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, SeccionesLecturas.Escanear));

    /// <summary>
    /// Aviso breve (la vista lo anuncia al lector de pantalla); se borra solo.
    /// </summary>
    private async void MostrarAviso(string texto)
    {
        _aviso?.Cancel();
        var cancelacion = _aviso = new CancellationTokenSource();
        Aviso = texto;
        try
        {
            await Task.Delay(DuracionAviso, cancelacion.Token);
            Aviso = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Llegó otro aviso.
        }
    }
}
