using VoltiaMX.Core.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VoltiaMX.Services;

/// <summary>
/// Elegir un archivo del equipo. Los ViewModels piden la ruta sin conocer el selector.
/// </summary>
public interface ISelectorArchivos
{
    /// <summary>
    /// Imagen (.png, .jpg, .jpeg) o PDF del recibo; null si la persona cancela.
    /// </summary>
    Task<string?> ElegirReciboAsync();

    /// <summary>
    /// Un respaldo .voltiabak para restaurar; null si la persona cancela.
    /// </summary>
    Task<string?> ElegirRespaldoAsync();

    /// <summary>
    /// Dónde guardar un respaldo nuevo; null si la persona cancela. No escribe nada.
    /// </summary>
    Task<string?> ElegirDestinoRespaldoAsync(string nombreSugerido);

#if DEBUG
    /// <summary>
    /// Solo para pruebas: si tiene valor, el respaldo nuevo se guarda en esta carpeta sin abrir el selector.
    /// </summary>
    string? CarpetaPrueba { get; set; }

    /// <summary>
    /// Solo para pruebas: si tiene valor, se elige este respaldo sin abrir el selector.
    /// </summary>
    string? RespaldoPrueba { get; set; }
#endif
}

public sealed class SelectorArchivos(ITextos textos) : ISelectorArchivos
{
#if DEBUG
    public string? CarpetaPrueba { get; set; }

    public string? RespaldoPrueba { get; set; }
#endif

    public async Task<string?> ElegirReciboAsync()
    {
        var selector = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            ViewMode = PickerViewMode.Thumbnail,
        };
        foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".pdf" })
        {
            selector.FileTypeFilter.Add(extension);
        }

        AsociarALaVentana(selector);
        var archivo = await selector.PickSingleFileAsync();
        return archivo?.Path;
    }

    public async Task<string?> ElegirRespaldoAsync()
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(RespaldoPrueba))
        {
            return RespaldoPrueba.Trim().Trim('"');
        }
#endif
        var selector = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        selector.FileTypeFilter.Add(RespaldoService.Extension);
        AsociarALaVentana(selector);
        var archivo = await selector.PickSingleFileAsync();
        return archivo?.Path;
    }

    public async Task<string?> ElegirDestinoRespaldoAsync(string nombreSugerido)
    {
#if DEBUG
        if (!string.IsNullOrWhiteSpace(CarpetaPrueba))
        {
            return Path.Combine(CarpetaPrueba.Trim().Trim('"'), nombreSugerido);
        }
#endif
        var selector = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = Path.GetFileNameWithoutExtension(nombreSugerido),
        };
        selector.FileTypeChoices.Add(textos["Respaldo_TipoArchivo"], [RespaldoService.Extension]);
        AsociarALaVentana(selector);
        var archivo = await selector.PickSaveFileAsync();
        return archivo?.Path;
    }

    /// <summary>
    /// En WinUI 3 el selector necesita la ventana que lo abre.
    /// </summary>
    private static void AsociarALaVentana(object selector)
    {
        if (App.VentanaPrincipal is { } ventana)
        {
            InitializeWithWindow.Initialize(selector, WindowNative.GetWindowHandle(ventana));
        }
    }
}
