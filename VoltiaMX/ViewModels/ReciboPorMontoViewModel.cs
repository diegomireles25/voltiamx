using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// "Agregar un recibo anterior por su monto" (D9): total pagado, fecha de corte e impuestos y cargos en %.
/// Antes de guardar muestra cuántos kWh habría sido (aproximado); calcula 300 ms después de cada cambio.
/// </summary>
public sealed partial class ReciboPorMontoViewModel(
    IRecibosService recibos,
    ILecturaService lecturas,
    ITextos textos) : ObservableObject
{
    private static readonly TimeSpan EsperaCalculo = TimeSpan.FromMilliseconds(300);

    private int? _lugarId;
    private CancellationTokenSource? _calculo;

    [ObservableProperty] public partial bool TieneLugar { get; set; }

    [ObservableProperty] public partial double Total { get; set; } = double.NaN;

    [ObservableProperty] public partial DateTimeOffset? Fecha { get; set; }

    [ObservableProperty] public partial double Porcentaje { get; set; } = (double)EstimadorPorMonto.CargosPorDefecto;

    public DateTimeOffset FechaMaxima { get; } = new(DateTime.Today);

    [ObservableProperty] public partial bool TieneConfirmacion { get; set; }
    [ObservableProperty] public partial string Confirmacion { get; set; } = string.Empty;
    [ObservableProperty] public partial string FechasRecibo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool PuedeGuardar { get; set; }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    public async Task CargarAsync()
    {
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;
        if (lugar is not null && await recibos.UltimoCorteAsync(lugar.Id, Hoy) is DateOnly corte)
        {
            Fecha = new DateTimeOffset(corte.ToDateTime(TimeOnly.MinValue));
        }
    }

    partial void OnTotalChanged(double value) => ProgramarCalculo();

    partial void OnFechaChanged(DateTimeOffset? value) => ProgramarCalculo();

    partial void OnPorcentajeChanged(double value) => ProgramarCalculo();

    private async void ProgramarCalculo()
    {
        _calculo?.Cancel();
        var cancelacion = _calculo = new CancellationTokenSource();
        try
        {
            await Task.Delay(EsperaCalculo, cancelacion.Token);
            await CalcularAsync(cancelacion.Token);
        }
        catch (OperationCanceledException)
        {
            // Llegó otro cambio.
        }
    }

    private async Task CalcularAsync(CancellationToken cancellationToken)
    {
        PuedeGuardar = TieneConfirmacion = false;
        Confirmacion = FechasRecibo = Error = string.Empty;
        if (_lugarId is not int lugarId || double.IsNaN(Total))
        {
            return;
        }

        var resultado = await recibos.PrevisualizarPorMontoAsync(lugarId, (decimal)Total, FechaElegida(), PorcentajeElegido(), Hoy, cancellationToken: cancellationToken);
        Mostrar(resultado);
    }

    private void Mostrar(ResultadoReciboPorMonto resultado)
    {
        if (resultado.Error is { } error)
        {
            Error = textos[$"ReciboMonto_Error_{error}"];
            return;
        }

        Confirmacion = textos.Formato("ReciboMonto_Confirmacion", resultado.Kwh);
        FechasRecibo = textos.Formato("ReciboMonto_Fechas", resultado.Inicio, resultado.Fin);
        TieneConfirmacion = PuedeGuardar = true;
    }

    private DateOnly? FechaElegida() => Fecha is DateTimeOffset f ? DateOnly.FromDateTime(f.Date) : null;

    private decimal PorcentajeElegido() => double.IsNaN(Porcentaje) ? EstimadorPorMonto.CargosPorDefecto : (decimal)Porcentaje;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (_lugarId is not int lugarId || double.IsNaN(Total))
        {
            return;
        }

        var resultado = await recibos.AgregarPorMontoAsync(lugarId, (decimal)Total, FechaElegida(), PorcentajeElegido(), Hoy);
        if (resultado.Error is not null)
        {
            PuedeGuardar = false;
            Mostrar(resultado);
            return;
        }

        // De vuelta en Lecturas, en el historial, donde ya aparece el recibo (marcado como aproximado).
        WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, SeccionesLecturas.Historial));
    }

    [RelayCommand]
    private void Cancelar() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas));
}
