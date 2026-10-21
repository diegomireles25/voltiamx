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
    INavegacionShell navegacion) : ObservableObject
{
    private static readonly TimeSpan DuracionAviso = TimeSpan.FromSeconds(6);

    private int? _lugarId;
    private CancellationTokenSource? _aviso;

    [ObservableProperty] public partial bool TieneLugar { get; set; }
    [ObservableProperty] public partial bool EsDemo { get; set; }
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
