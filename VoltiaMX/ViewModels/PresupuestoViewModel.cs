using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Tope por recibo y presupuesto inverso del lugar principal (el mismo del Dashboard). Las cifras salen
/// de <see cref="IResumenDashboardService"/>, así coinciden con las del Dashboard.
/// </summary>
public sealed partial class PresupuestoViewModel(
    IResumenDashboardService resumenes,
    IPerfilService perfiles,
    ITextos textos) : ObservableObject
{
    private const double MontoMaximo = 1_000_000;
    private static readonly TimeSpan DuracionAviso = TimeSpan.FromSeconds(6);

    private CancellationTokenSource? _aviso;

    [ObservableProperty] public partial bool EsDemo { get; set; }
    [ObservableProperty] public partial string EtiquetaMonto { get; set; } = string.Empty;
    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(QuitarCommand))]
    public partial bool TienePresupuesto { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial double Monto { get; set; } = double.NaN;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand), nameof(QuitarCommand))]
    public partial bool Guardando { get; set; }

    // Tarjeta de resultado
    [ObservableProperty] public partial string KwhPorDiaTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string TextoInverso { get; set; } = string.Empty;
    [ObservableProperty] public partial string KwhRestantesTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneRitmo { get; set; }
    [ObservableProperty] public partial string RitmoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string DiferenciaTexto { get; set; } = string.Empty;

    // Filas
    [ObservableProperty] public partial string KwhMaximosTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string ProyeccionTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool NoCubreCargoFijo { get; set; }
    [ObservableProperty] public partial bool TieneConsumo { get; set; }
    [ObservableProperty] public partial double ProgresoConsumo { get; set; }
    [ObservableProperty] public partial string ProgresoTexto { get; set; } = string.Empty;

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    public async Task CargarAsync()
    {
        var resumen = await resumenes.ObtenerAsync(Hoy);
        EsDemo = resumen?.EsDemo == true;
        EtiquetaMonto = textos[resumen?.MesesDelPeriodo == 1 ? "Tope_MontoMensual" : "Tope_MontoBimestral"];
        TienePresupuesto = resumen?.PresupuestoMax is not null;
        Monto = resumen?.PresupuestoMax is decimal tope ? (double)tope : double.NaN;
        if (resumen is not null && resumen.PresupuestoMax is decimal monto)
        {
            Aplicar(resumen, monto);
        }
    }

    private bool MontoValido() => !double.IsNaN(Monto) && Monto > 0 && Monto <= MontoMaximo && !Guardando;

    private bool PuedeQuitar() => TienePresupuesto && !Guardando;

    [RelayCommand(CanExecute = nameof(MontoValido))]
    private async Task GuardarAsync()
    {
        Guardando = true;
        try
        {
            await perfiles.GuardarPresupuestoAsync((decimal)Monto);
            await CargarAsync();
            MostrarAviso(textos["Tope_Guardado"]);
        }
        catch (ArgumentOutOfRangeException)
        {
            MostrarAviso(textos["Tope_FueraDeRango"]);
        }
        finally
        {
            Guardando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeQuitar))]
    private async Task QuitarAsync()
    {
        Guardando = true;
        try
        {
            await perfiles.GuardarPresupuestoAsync(null);
            await CargarAsync();
            MostrarAviso(textos["Tope_Quitado"]);
        }
        finally
        {
            Guardando = false;
        }
    }

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    private void Aplicar(ResumenDashboard resumen, decimal monto)
    {
        KwhPorDiaTexto = textos.Formato("Dashboard_KwhAlDia", resumen.KwhPorDiaRestante ?? 0m);
        TextoInverso = textos.Formato("Dashboard_Inverso", monto);
        KwhRestantesTexto = textos.Formato("Tope_KwhRestantes", resumen.KwhRestantes ?? 0m);

        TieneRitmo = resumen.PromedioDiarioKwh is not null && resumen.VariacionRitmoPresupuesto is not null;
        if (resumen.PromedioDiarioKwh is decimal ritmo && resumen.VariacionRitmoPresupuesto is decimal variacion)
        {
            RitmoTexto = textos.Formato("Tope_Ritmo", ritmo, resumen.KwhPorDiaPermitido ?? 0m);
            DiferenciaTexto = textos.Formato(
                variacion > 0 ? "Tope_RitmoArriba" : "Tope_RitmoAbajo",
                Math.Abs(variacion) * 100);
        }

        KwhMaximosTexto = textos.Formato("Formato_KwhUnidad", resumen.KwhMaximosPeriodo ?? 0m);
        ProyeccionTexto = (resumen.MontoProyectado, resumen.DiferenciaContraPresupuesto) switch
        {
            (decimal proyectado, decimal diferencia) when diferencia > 0
                => textos.Formato("Tope_ProyeccionArriba", proyectado, monto, diferencia),
            (decimal proyectado, decimal diferencia)
                => textos.Formato("Tope_ProyeccionAbajo", proyectado, monto, -diferencia),
            _ => textos["Tope_SinProyeccion"],
        };
        NoCubreCargoFijo = !resumen.PresupuestoCubreCargoFijo;

        TieneConsumo = resumen.KwhHastaHoy is not null && resumen.KwhMaximosPeriodo > 0m;
        if (resumen.KwhHastaHoy is decimal hastaHoy && resumen.KwhMaximosPeriodo is decimal maximos && maximos > 0m)
        {
            ProgresoConsumo = (double)Math.Min(hastaHoy / maximos * 100, 100m);
            ProgresoTexto = textos.Formato("Tope_ProgresoTexto", hastaHoy, maximos);
        }
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
