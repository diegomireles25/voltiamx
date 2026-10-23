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
}

public sealed class SelectorArchivos : ISelectorArchivos
{
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

        // En WinUI 3 el selector necesita la ventana que lo abre.
        if (App.VentanaPrincipal is { } ventana)
        {
            InitializeWithWindow.Initialize(selector, WindowNative.GetWindowHandle(ventana));
        }

        var archivo = await selector.PickSingleFileAsync();
        return archivo?.Path;
    }
}
