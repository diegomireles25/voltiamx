using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Fila de "Tus tarifas" con sus enlaces.
/// </summary>
public sealed record FilaTarifa(
    string Nombre, string Detalle, bool EnUso, IRelayCommand Editar, IRelayCommand Duplicar, IAsyncRelayCommand Eliminar);

/// <summary>
/// Tarifa del lugar principal (el mismo del Dashboard) y la lista de tarifas editables.
/// </summary>
public sealed partial class TarifasViewModel(
    ITarifaService tarifas,
    ILecturaService lecturas,
    IResumenDashboardService resumenes,
    IDialogos dialogos,
    ITextos textos,
    INavegacionShell navegacion,
    IValidadorRecibo validador) : ObservableObject
{
    // ---- Validar con tu recibo ----

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompararCommand))]
    public partial OpcionTarifa? TarifaValidar { get; set; }

    [ObservableProperty] public partial bool? ValidarBimestral { get; set; } = true;
    [ObservableProperty] public partial bool? ValidarMensual { get; set; } = false;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompararCommand))]
    public partial double KwhRecibo { get; set; } = double.NaN;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CompararCommand))]
    public partial double ImporteRecibo { get; set; } = double.NaN;

    [ObservableProperty] public partial bool TieneComparacion { get; set; }
    [ObservableProperty] public partial VeredictoRecibo Veredicto { get; set; }
    [ObservableProperty] public partial string VeredictoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string DiferenciaTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool NoCoincide { get; set; }

    public ObservableCollection<FilaRecibo> FilasComparacion { get; } = [];

    private bool PuedeComparar()
        => TarifaValidar is not null && !double.IsNaN(KwhRecibo) && KwhRecibo >= 0 && !double.IsNaN(ImporteRecibo) && ImporteRecibo >= 0;

    [RelayCommand(CanExecute = nameof(PuedeComparar))]
    private async Task CompararAsync()
    {
        if (TarifaValidar is not { } opcion || await tarifas.ObtenerParaEditarAsync(opcion.Id) is not { } editable)
        {
            return;
        }

        var comparacion = validador.Comparar(
            editable.ComoTarifa(), ValidarMensual == true ? 1 : 2, (decimal)KwhRecibo, (decimal)ImporteRecibo);

        // Filas a centavos; la energía calculada es la suma de lo que se ve (sin cargo fijo).
        var redondeado = DesgloseRedondeado.Desde(comparacion.Calculo);
        FilasComparacion.Clear();
        foreach (var rango in redondeado.Rangos)
        {
            FilasComparacion.Add(new FilaRecibo(
                rango.Nombre,
                textos.Formato("Predictor_FilaRango", rango.Kwh, rango.PrecioPorKwh, rango.Subtotal),
                EsTotal: false));
        }

        FilasComparacion.Add(new FilaRecibo(
            textos["Validar_EnergiaCalculada"],
            textos.Formato("Formato_Centavos", redondeado.Rangos.Sum(r => r.Subtotal)),
            EsTotal: true));
        FilasComparacion.Add(new FilaRecibo(
            textos["Validar_EnergiaRecibo"], textos.Formato("Formato_Centavos", (decimal)ImporteRecibo), EsTotal: false));

        Veredicto = comparacion.Veredicto;
        VeredictoTexto = textos[$"Validar_Veredicto_{comparacion.Veredicto}"];
        DiferenciaTexto = comparacion.DiferenciaPorcentaje is decimal porcentaje
            ? textos.Formato("Validar_Diferencia", comparacion.Diferencia, porcentaje * 100)
            : textos.Formato("Validar_DiferenciaPesos", comparacion.Diferencia);
        NoCoincide = comparacion.Veredicto == VeredictoRecibo.NoCoincide;
        TieneComparacion = true;
    }

    private bool PuedeRecalcular() => TieneLugar && !EsDemo;

    [RelayCommand(CanExecute = nameof(PuedeRecalcular))]
    private async Task RecalcularAsync()
    {
        if (_lugarId is not int lugarId)
        {
            return;
        }

        var conteo = await tarifas.ContarParaRecalcularAsync(lugarId);
        var confirmado = await dialogos.ConfirmarAsync(
            textos["Recalcular_Titulo"],
            textos.Formato("Recalcular_Texto", conteo.ARecalcular, conteo.ConMontoDeRecibo, _tarifaDelLugar),
            textos["Recalcular_Confirmar"],
            textos["Eliminar_Cancelar"]);
        if (!confirmado)
        {
            return;
        }

        var resultado = await tarifas.RecalcularPeriodosAsync(lugarId);
        MostrarAviso(textos.Formato("Recalcular_Hecho", resultado.Recalculados, resultado.Cambiados, resultado.SaltadosPorRecibo));
    }

    private static readonly TimeSpan DuracionAviso = TimeSpan.FromSeconds(6);

    private int? _lugarId;
    private string _tarifaDelLugar = string.Empty;
    private CancellationTokenSource? _aviso;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecalcularCommand))]
    public partial bool TieneLugar { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RecalcularCommand))]
    public partial bool EsDemo { get; set; }
    [ObservableProperty] public partial string TarifaActualTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;
    [ObservableProperty] public partial IReadOnlyList<OpcionTarifa> Opciones { get; set; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UsarTarifaCommand))]
    public partial OpcionTarifa? TarifaElegida { get; set; }

    public ObservableCollection<FilaTarifa> Filas { get; } = [];

    public async Task CargarAsync()
    {
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;
        EsDemo = lugar?.EsDemo == true;

        var resumen = await resumenes.ObtenerAsync(DateOnly.FromDateTime(DateTime.Today));
        _tarifaDelLugar = resumen?.NombreTarifa ?? string.Empty;
        TarifaActualTexto = resumen is null
            ? string.Empty
            : textos.Formato(
                resumen.TarifaPorConfirmar ? "Tarifas_ActualPorConfirmar" : "Tarifas_Actual",
                resumen.NombreLugar,
                resumen.NombreTarifa);

        var lista = await tarifas.ListarConUsoAsync();
        Opciones = lista.Select(t => new OpcionTarifa(t.Id, t.Nombre)).ToList();
        TarifaElegida = Opciones.FirstOrDefault(o => o.Nombre == resumen?.NombreTarifa);
        UsarTarifaCommand.NotifyCanExecuteChanged();

        // Para validar, por defecto la tarifa y la periodicidad del lugar.
        TarifaValidar = TarifaElegida ?? Opciones.FirstOrDefault();
        ValidarMensual = resumen?.MesesDelPeriodo == 1;
        ValidarBimestral = !ValidarMensual;

        Filas.Clear();
        foreach (var tarifa in lista)
        {
            var id = tarifa.Id;
            Filas.Add(new FilaTarifa(
                tarifa.Nombre,
                textos.Formato("Tarifas_Detalle", textos[$"TipoTarifa_{tarifa.Tipo}"], tarifa.NumeroDeRangos),
                tarifa.EnUso,
                new RelayCommand(() => navegacion.AbrirEditorDeTarifa(id)),
                new AsyncRelayCommand(() => DuplicarAsync(id)),
                new AsyncRelayCommand(() => EliminarAsync(id, tarifa.Nombre))));
        }
    }

    private bool PuedeUsar() => TieneLugar && !EsDemo && TarifaElegida is not null;

    [RelayCommand(CanExecute = nameof(PuedeUsar))]
    private async Task UsarTarifaAsync()
    {
        if (_lugarId is not int lugarId || TarifaElegida is not { } tarifa)
        {
            return;
        }

        await tarifas.AsignarALugarAsync(lugarId, tarifa.Id);
        await CargarAsync();
        MostrarAviso(textos.Formato("Tarifas_Asignada", tarifa.Nombre));
    }

    [RelayCommand]
    private void NuevaTarifa() => navegacion.AbrirEditorDeTarifa(null);

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    private async Task DuplicarAsync(int id)
    {
        await tarifas.DuplicarAsync(id);
        await CargarAsync();
        MostrarAviso(textos["Tarifas_Duplicada"]);
    }

    private async Task EliminarAsync(int id, string nombre)
    {
        var confirmada = await dialogos.ConfirmarAsync(
            textos["Tarifas_EliminarTitulo"],
            textos.Formato("Tarifas_EliminarTexto", nombre),
            textos["Eliminar_Confirmar"],
            textos["Eliminar_Cancelar"]);
        if (!confirmada)
        {
            return;
        }

        var resultado = await tarifas.EliminarAsync(id);
        if (resultado.Motivo is MotivoNoEliminarTarifa motivo)
        {
            await dialogos.AvisarAsync(
                textos["Tarifas_NoSePuedeEliminar"],
                textos.Formato($"Tarifas_NoEliminar_{motivo}", string.Join(", ", resultado.LugaresQueLaUsan)),
                textos["Dialogo_Entendido"]);
            return;
        }

        await CargarAsync();
        MostrarAviso(textos.Formato("Tarifas_Eliminada", nombre));
    }

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
