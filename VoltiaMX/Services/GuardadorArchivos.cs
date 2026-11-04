using VoltiaMX.Core.Services;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

namespace VoltiaMX.Services;

public enum TipoArchivo
{
    Pdf,
    Excel,
}

public enum ErrorGuardado
{
    /// <summary>
    /// No se pudo generar el documento.
    /// </summary>
    NoSeGenero,
    SinPermiso,
    EnUso,
    DiscoLleno,
    DestinoNoExiste,
    Desconocido,
}

/// <summary>
/// Cómo terminó un guardado: con la ruta, cancelado por la persona (no es un error) o con un error.
/// </summary>
public sealed record ResultadoGuardado(string? Ruta, bool Cancelado, ErrorGuardado? Error)
{
    public static ResultadoGuardado Guardado(string ruta) => new(ruta, false, null);

    public static readonly ResultadoGuardado PorCancelacion = new(null, true, null);

    public static ResultadoGuardado ConError(ErrorGuardado error) => new(null, false, error);
}

/// <summary>
/// Guardar un archivo donde la persona elija. Nada se escribe sin que elija el destino.
/// </summary>
public interface IGuardadorArchivos
{
    /// <summary>
    /// Pide el destino y, solo si la persona lo elige, genera el contenido y lo escribe.
    /// </summary>
    Task<ResultadoGuardado> GuardarAsync(string nombreSugerido, TipoArchivo tipo, Func<Task<byte[]>> generar);

    Task<bool> AbrirCarpetaAsync(string rutaDelArchivo);

    /// <summary>
    /// Genera y escribe varios archivos en <paramref name="carpeta"/> sin sobrescribir los que ya existen
    /// (agrega "-2", "-3"…). Devuelve la ruta del último archivo o el primer error.
    /// </summary>
    Task<ResultadoGuardado> GuardarEnCarpetaAsync(string carpeta, IReadOnlyList<(string Nombre, Func<Task<byte[]>> Generar)> archivos);

    /// <summary>
    /// Abre la carpeta en el Explorador; false si no existe o no se pudo abrir.
    /// </summary>
    Task<bool> AbrirDirectorioAsync(string carpeta);

#if DEBUG
    /// <summary>
    /// Solo para pruebas: si tiene valor, se escribe en esta carpeta sin abrir el selector.
    /// </summary>
    string? CarpetaPrueba { get; set; }
#endif
}

public sealed class GuardadorArchivos(ITextos textos) : IGuardadorArchivos
{
#if DEBUG
    public string? CarpetaPrueba { get; set; }
#endif

    public async Task<ResultadoGuardado> GuardarAsync(string nombreSugerido, TipoArchivo tipo, Func<Task<byte[]>> generar)
    {
        ArgumentNullException.ThrowIfNull(generar);
#if DEBUG
        if (!string.IsNullOrWhiteSpace(CarpetaPrueba))
        {
            return await GuardarEnCarpetaAsync(Path.Combine(CarpetaPrueba.Trim().Trim('"'), nombreSugerido), generar);
        }
#endif

        StorageFile? archivo;
        try
        {
            var selector = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = Path.GetFileNameWithoutExtension(nombreSugerido),
            };
            var (descripcion, extension) = tipo == TipoArchivo.Pdf
                ? (textos["Reportes_TipoPdf"], ".pdf")
                : (textos["Reportes_TipoExcel"], ".xlsx");
            selector.FileTypeChoices.Add(descripcion, [extension]);
            if (App.VentanaPrincipal is { } ventana)
            {
                InitializeWithWindow.Initialize(selector, WindowNative.GetWindowHandle(ventana));
            }

            archivo = await selector.PickSaveFileAsync();
        }
        catch (Exception ex)
        {
            return ResultadoGuardado.ConError(Clasificar(ex));
        }

        if (archivo is null)
        {
            return ResultadoGuardado.PorCancelacion;
        }

        var contenido = await GenerarAsync(generar);
        if (contenido is null)
        {
            return ResultadoGuardado.ConError(ErrorGuardado.NoSeGenero);
        }

        try
        {
            await FileIO.WriteBytesAsync(archivo, contenido);
            return ResultadoGuardado.Guardado(archivo.Path);
        }
        catch (Exception ex)
        {
            return ResultadoGuardado.ConError(Clasificar(ex));
        }
    }

    public async Task<ResultadoGuardado> GuardarEnCarpetaAsync(string carpeta, IReadOnlyList<(string Nombre, Func<Task<byte[]>> Generar)> archivos)
    {
        ArgumentNullException.ThrowIfNull(archivos);
        string? ultima = null;
        foreach (var (nombre, generar) in archivos)
        {
            var contenido = await GenerarAsync(generar);
            if (contenido is null)
            {
                return ResultadoGuardado.ConError(ErrorGuardado.NoSeGenero);
            }

            try
            {
                if (!Directory.Exists(carpeta))
                {
                    return ResultadoGuardado.ConError(ErrorGuardado.DestinoNoExiste);
                }

                ultima = NombreArchivo.Disponible(carpeta, nombre);
                await File.WriteAllBytesAsync(ultima, contenido);
            }
            catch (Exception ex)
            {
                return ResultadoGuardado.ConError(Clasificar(ex));
            }
        }

        return ultima is null ? ResultadoGuardado.PorCancelacion : ResultadoGuardado.Guardado(ultima);
    }

    public async Task<bool> AbrirCarpetaAsync(string rutaDelArchivo)
        => Path.GetDirectoryName(rutaDelArchivo) is { Length: > 0 } carpeta && await AbrirDirectorioAsync(carpeta);

    public async Task<bool> AbrirDirectorioAsync(string carpeta)
    {
        try
        {
            return Directory.Exists(carpeta) && await Launcher.LaunchFolderPathAsync(carpeta);
        }
        catch (Exception)
        {
            return false;
        }
    }

#if DEBUG
    private static async Task<ResultadoGuardado> GuardarEnCarpetaAsync(string ruta, Func<Task<byte[]>> generar)
    {
        var contenido = await GenerarAsync(generar);
        if (contenido is null)
        {
            return ResultadoGuardado.ConError(ErrorGuardado.NoSeGenero);
        }

        try
        {
            await File.WriteAllBytesAsync(ruta, contenido);
            return ResultadoGuardado.Guardado(ruta);
        }
        catch (Exception ex)
        {
            return ResultadoGuardado.ConError(Clasificar(ex));
        }
    }
#endif

    private static async Task<byte[]?> GenerarAsync(Func<Task<byte[]>> generar)
    {
        try
        {
            return await generar();
        }
        catch (Exception ex)
        {
            RegistroDepuracion.Escribir($"[Reportes] no se pudo generar: {ex.GetType().Name}");
            return null;
        }
    }

    /// <summary>
    /// El error en palabras de la persona, por tipo de excepción o código de Windows.
    /// </summary>
    private static ErrorGuardado Clasificar(Exception ex) => ex switch
    {
        UnauthorizedAccessException => ErrorGuardado.SinPermiso,
        DirectoryNotFoundException or FileNotFoundException => ErrorGuardado.DestinoNoExiste,
        _ => (uint)ex.HResult switch
        {
            0x80070005 => ErrorGuardado.SinPermiso,
            0x80070020 or 0x80070021 => ErrorGuardado.EnUso,
            0x80070070 or 0x80070027 => ErrorGuardado.DiscoLleno,
            0x80070002 or 0x80070003 or 0x8007007B => ErrorGuardado.DestinoNoExiste,
            _ => ErrorGuardado.Desconocido,
        },
    };
}
