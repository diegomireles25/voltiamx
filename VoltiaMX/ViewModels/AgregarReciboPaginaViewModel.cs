using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// "Agrega un recibo" desde Inicio o Mis recibos (Fase 9c, D1): el formulario común con Guardar y Cancelar. Al
/// terminar vuelve a la pantalla de origen; en Inicio ya aparece "Si sigues igual…".
/// </summary>
public sealed partial class AgregarReciboPaginaViewModel(
    AgregarReciboViewModel recibo,
    ILecturaService lecturas,
    IPerfilService perfiles) : ObservableObject
{
    private int? _lugarId;
    private string? _seccion;

    public AgregarReciboViewModel Recibo { get; } = recibo;

    [ObservableProperty] public partial bool TieneLugar { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Guardando { get; set; }

    /// <summary>
    /// <paramref name="seccion"/> dice de dónde se abrió (<see cref="SeccionesLecturas.AgregarDesdeInicio"/> o
    /// <see cref="SeccionesLecturas.AgregarDesdeMisRecibos"/>) y si empieza en "Escanear mi recibo".
    /// </summary>
    public async Task CargarAsync(string? seccion)
    {
        _seccion = seccion;
        // Fase 9k (D9): Guardar se apaga solo sin importe o sin fecha.
        Recibo.PropertyChanged -= AlCambiarRecibo;
        Recibo.PropertyChanged += AlCambiarRecibo;
        if (SeccionesLecturas.EmpiezaEscaneando(seccion))
        {
            Recibo.ReciboEscanear = true;
        }

        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;

        // La tarifa del lugar preselecciona la periodicidad en el primer recibo (comercial = cada mes).
        var principal = await perfiles.ObtenerLugarPrincipalAsync();
        await Recibo.IniciarAsync(_lugarId, principal?.Id == _lugarId ? principal?.Tarifa?.Tipo : null);
    }

    private bool PuedeGuardar() => !Guardando && Recibo.PuedeGuardar;

    private void AlCambiarRecibo(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgregarReciboViewModel.PuedeGuardar))
        {
            GuardarCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (_lugarId is not int lugarId)
        {
            return;
        }

        Guardando = true;
        try
        {
            if (await Recibo.ValidarAsync(lugarId) is not null || await Recibo.GuardarAsync(lugarId) is not null)
            {
                return;
            }
        }
        finally
        {
            Guardando = false;
        }

        WeakReferenceMessenger.Default.Send(new EvaluarAvisosMensaje());
        Volver(guardado: true);
    }

    [RelayCommand]
    private void Cancelar() => Volver(guardado: false);

    private void Volver(bool guardado)
    {
        if (SeccionesLecturas.VuelveAInicio(_seccion))
        {
            WeakReferenceMessenger.Default.Send(new IrADashboardMensaje());
            return;
        }

        // En Mis recibos, al historial, donde ya aparece el recibo (marcado como aproximado).
        WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, guardado ? SeccionesLecturas.Historial : null));
    }
}
