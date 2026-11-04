using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Reportes y exportación del lugar principal: el reporte del periodo en PDF y "Mis datos" en Excel.
/// Todo se genera en el equipo; se guarda solo donde la persona elija.
/// </summary>
public sealed partial class ReportesViewModel(
    IReporteService reportes,
    ILecturaService lecturas,
    IGuardadorArchivos guardador,
    OpcionesPdf fuentes,
    ITextos textos) : ObservableObject
{
    private int? _lugarId;

    [ObservableProperty] public partial bool EsDemo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportarPdfCommand), nameof(ExportarExcelCommand))]
    public partial bool Generando { get; set; }

    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AbrirCarpetaCommand))]
    public partial string UltimaRuta { get; set; } = string.Empty;

    public async Task CargarAsync()
    {
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        EsDemo = lugar?.EsDemo == true;
    }

    private bool PuedeExportar() => !Generando;

    [RelayCommand(CanExecute = nameof(PuedeExportar))]
    private Task ExportarPdfAsync() => ExportarAsync(
        $"VoltiaMX-reporte-{DateTime.Today:yyyy-MM-dd}.pdf",
        TipoArchivo.Pdf,
        datos => GeneradorPdf.Generar(datos, Texto, fuentes));

    [RelayCommand(CanExecute = nameof(PuedeExportar))]
    private Task ExportarExcelAsync() => ExportarAsync(
        $"VoltiaMX-datos-{DateTime.Today:yyyy-MM-dd}.xlsx",
        TipoArchivo.Excel,
        datos => GeneradorExcel.Generar(datos, Texto));

    private string Texto(string clave) => textos[clave];

    private async Task ExportarAsync(string nombre, TipoArchivo tipo, Func<ReporteDatos, byte[]> generar)
    {
        Generando = true;
        Aviso = Error = string.Empty;
        try
        {
            // Los datos se leen y el documento se arma fuera del hilo de la interfaz.
            var resultado = await guardador.GuardarAsync(nombre, tipo, async () =>
            {
                var datos = await reportes.ObtenerAsync(_lugarId, DateOnly.FromDateTime(DateTime.Today));
                return await Task.Run(() => generar(datos));
            });

            if (resultado.Ruta is { } ruta)
            {
                UltimaRuta = ruta;
                Aviso = textos.Formato("Reportes_Guardado", ruta);
            }
            else if (resultado.Error is ErrorGuardado error)
            {
                Error = textos[$"Reportes_Error_{error}"];
            }
        }
        finally
        {
            Generando = false;
        }
    }

    private bool HayArchivo() => UltimaRuta.Length > 0;

    [RelayCommand(CanExecute = nameof(HayArchivo))]
    private async Task AbrirCarpetaAsync() => await guardador.AbrirCarpetaAsync(UltimaRuta);

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

#if DEBUG
    /// <summary>
    /// Solo para pruebas automatizadas: guardar en esta carpeta sin abrir el selector.
    /// </summary>
    public string CarpetaPrueba
    {
        get => guardador.CarpetaPrueba ?? string.Empty;
        set => guardador.CarpetaPrueba = value;
    }
#endif
}
