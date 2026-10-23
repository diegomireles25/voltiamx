using Windows.Data.Pdf;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;
using Windows.Storage.Streams;

namespace VoltiaMX.Services;

public enum ErrorOcr
{
    /// <summary>
    /// Windows no tiene ningún idioma de reconocimiento de texto instalado.
    /// </summary>
    SinIdioma,

    /// <summary>
    /// No es .png, .jpg, .jpeg ni .pdf.
    /// </summary>
    FormatoNoSoportado,

    /// <summary>
    /// El archivo no existe, está vacío o dañado, o no se pudo abrir.
    /// </summary>
    ArchivoIlegible,
}

/// <summary>
/// Líneas reconocidas, en el orden en que el OCR las devuelve. <see cref="Idioma"/> es la etiqueta
/// del idioma usado (p. ej. "es-MX").
/// </summary>
public sealed record ResultadoOcr(IReadOnlyList<string> Lineas, string? Idioma, ErrorOcr? Error);

/// <summary>
/// Reconocimiento de texto del recibo, sin internet. No guarda la imagen ni el texto.
/// </summary>
public interface IReciboOcr
{
    Task<ResultadoOcr> LeerAsync(string ruta, CancellationToken cancellationToken = default);
}

/// <summary>
/// OCR con Windows.Media.Ocr. Las imágenes se decodifican en memoria; de un PDF se leen las primeras
/// páginas, renderizadas en memoria. Nada se escribe en disco ni en registros.
/// </summary>
public sealed class ReciboOcr : IReciboOcr
{
    private const int PaginasDePdf = 2;

    // Ancho al que se renderiza cada página del PDF: ~200 ppp en tamaño carta, suficiente para el OCR.
    private const uint AnchoDePaginaPdf = 1700;

    private static readonly string[] Imagenes = [".png", ".jpg", ".jpeg"];

    public async Task<ResultadoOcr> LeerAsync(string ruta, CancellationToken cancellationToken = default)
    {
        var motor = CrearMotor();
        if (motor is null)
        {
            return new ResultadoOcr([], null, ErrorOcr.SinIdioma);
        }

        var idioma = motor.RecognizerLanguage.LanguageTag;
        var extension = Path.GetExtension(ruta ?? string.Empty).ToLowerInvariant();
        var esPdf = extension == ".pdf";
        if (!esPdf && !Imagenes.Contains(extension))
        {
            return new ResultadoOcr([], idioma, ErrorOcr.FormatoNoSoportado);
        }

        try
        {
            var archivo = await StorageFile.GetFileFromPathAsync(ruta);
            var lineas = new List<string>();
            if (esPdf)
            {
                var documento = await PdfDocument.LoadFromFileAsync(archivo);
                for (uint i = 0; i < Math.Min(documento.PageCount, PaginasDePdf); i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var pagina = documento.GetPage(i);
                    using var imagen = new InMemoryRandomAccessStream();
                    await pagina.RenderToStreamAsync(imagen, new PdfPageRenderOptions { DestinationWidth = AnchoDePaginaPdf });
                    lineas.AddRange(await ReconocerAsync(motor, imagen, cancellationToken));
                }
            }
            else
            {
                using var imagen = await archivo.OpenReadAsync();
                lineas.AddRange(await ReconocerAsync(motor, imagen, cancellationToken));
            }

            return new ResultadoOcr(lineas, idioma, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Archivo inexistente, vacío, dañado o con formato distinto a su extensión.
            return new ResultadoOcr([], idioma, ErrorOcr.ArchivoIlegible);
        }
    }

    /// <summary>
    /// Español de México; si no está, cualquier español instalado; si tampoco, los idiomas del perfil.
    /// </summary>
    private static OcrEngine? CrearMotor()
        => OcrEngine.TryCreateFromLanguage(new Language("es-MX"))
            ?? OcrEngine.AvailableRecognizerLanguages
                .Where(l => l.LanguageTag.StartsWith("es", StringComparison.OrdinalIgnoreCase))
                .Select(OcrEngine.TryCreateFromLanguage)
                .FirstOrDefault(m => m is not null)
            ?? OcrEngine.TryCreateFromUserProfileLanguages();

    private static async Task<IEnumerable<string>> ReconocerAsync(OcrEngine motor, IRandomAccessStream imagen, CancellationToken cancellationToken)
    {
        imagen.Seek(0);
        var decodificador = await BitmapDecoder.CreateAsync(imagen);
        cancellationToken.ThrowIfCancellationRequested();

        // El OCR tiene un tamaño máximo por lado: se reduce conservando la proporción.
        var transformacion = new BitmapTransform();
        var mayor = Math.Max(decodificador.PixelWidth, decodificador.PixelHeight);
        if (mayor > OcrEngine.MaxImageDimension)
        {
            var escala = (double)OcrEngine.MaxImageDimension / mayor;
            transformacion.ScaledWidth = (uint)(decodificador.PixelWidth * escala);
            transformacion.ScaledHeight = (uint)(decodificador.PixelHeight * escala);
        }

        using var mapa = await decodificador.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, transformacion,
            ExifOrientationMode.RespectExifOrientation, ColorManagementMode.DoNotColorManage);
        cancellationToken.ThrowIfCancellationRequested();
        var resultado = await motor.RecognizeAsync(mapa);
        return resultado.Lines.Select(l => l.Text).ToList();
    }
}
