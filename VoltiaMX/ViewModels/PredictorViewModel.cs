using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Fila del recibo explicado. <see cref="EsTotal"/> se muestra con más peso.
/// </summary>
public sealed record FilaRecibo(string Concepto, string Detalle, bool EsTotal);

/// <summary>
/// Pago proyectado explicado por rangos y detalle del riesgo DAC del lugar principal (el mismo del
/// Dashboard). Las cifras salen de <see cref="IResumenDashboardService"/>.
/// </summary>
public sealed partial class PredictorViewModel(
    IResumenDashboardService resumenes,
    IPerfilService perfiles,
    ITextos textos) : ObservableObject
{
    [ObservableProperty] public partial bool EsDemo { get; set; }

    /// <summary>
    /// La tarifa del lugar trae precios de ejemplo: se muestra el aviso con el enlace a Tarifas (D3).
    /// </summary>
    [ObservableProperty] public partial bool PreciosDeEjemplo { get; set; }

    [RelayCommand]
    private void IrATarifas() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Tarifas));
    [ObservableProperty] public partial bool TieneProyeccion { get; set; }
    [ObservableProperty] public partial string MontoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string KwhTexto { get; set; } = string.Empty;

    // DAC
    [ObservableProperty] public partial bool TieneDac { get; set; }
    [ObservableProperty] public partial bool DacNoAplica { get; set; }
    [ObservableProperty] public partial bool DacSinDatos { get; set; }
    [ObservableProperty] public partial NivelRiesgoDac NivelDac { get; set; }
    [ObservableProperty] public partial string TextoNivelDac { get; set; } = string.Empty;
    [ObservableProperty] public partial string PromedioDacTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string MargenDacTexto { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrALecturasCommand))]
    public partial bool LecturasHabilitadas { get; set; }

    public ObservableCollection<FilaRecibo> Recibo { get; } = [];

    public async Task CargarAsync()
    {
        var modulos = await perfiles.ObtenerModulosActivosAsync();
        LecturasHabilitadas = modulos.Contains(ModuloApp.Lecturas);

        var resumen = await resumenes.ObtenerAsync(DateOnly.FromDateTime(DateTime.Today));
        EsDemo = resumen?.EsDemo == true;
        PreciosDeEjemplo = resumen?.PreciosDeEjemplo == true;
        AplicarProyeccion(resumen);
        AplicarDac(resumen);
    }

    [RelayCommand(CanExecute = nameof(LecturasHabilitadas))]
    private void IrALecturas() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas));

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    private void AplicarProyeccion(ResumenDashboard? resumen)
    {
        Recibo.Clear();
        TieneProyeccion = resumen?.DesgloseProyectado is not null;
        if (resumen?.DesgloseProyectado is not { } calculo || resumen.KwhProyectados is not decimal kwh)
        {
            return;
        }

        // Cada fila a centavos; el total es la suma de lo que se ve, así las filas cuadran con él.
        var redondeado = DesgloseRedondeado.Desde(calculo);
        // La cifra grande va en pesos enteros, igual que en el Dashboard.
        MontoTexto = textos.Formato("Formato_Monto", resumen.MontoProyectado ?? redondeado.Total);
        KwhTexto = textos.Formato("Predictor_KwhProyectados", kwh);
        foreach (var rango in redondeado.Rangos)
        {
            Recibo.Add(new FilaRecibo(
                rango.Nombre,
                textos.Formato("Predictor_FilaRango", rango.Kwh, rango.PrecioPorKwh, rango.Subtotal),
                EsTotal: false));
        }

        Recibo.Add(new FilaRecibo(textos["Predictor_CargoFijo"], textos.Formato("Formato_Centavos", redondeado.CargoFijo), EsTotal: false));
        Recibo.Add(new FilaRecibo(textos["Predictor_Total"], textos.Formato("Formato_Centavos", redondeado.Total), EsTotal: true));
    }

    private void AplicarDac(ResumenDashboard? resumen)
    {
        DacNoAplica = resumen is not null && resumen.LimiteDacKwhMensual is null;
        TieneDac = resumen?.Dac is not null;
        DacSinDatos = resumen is not null && !DacNoAplica && !TieneDac;
        if (resumen?.Dac is not { } dac || resumen.LimiteDacKwhMensual is not decimal limite)
        {
            return;
        }

        NivelDac = dac.Nivel;
        TextoNivelDac = textos[$"Dac_{dac.Nivel}"];
        PromedioDacTexto = textos.Formato("Predictor_DacPromedio", dac.PromedioMensualKwh, limite, resumen.MesesEnPromedioDac);
        MargenDacTexto = dac.ExcedeLimite
            ? textos.Formato("Predictor_DacExcede", -dac.MargenKwh)
            : textos.Formato("Predictor_DacMargen", dac.MargenKwh);
    }
}
