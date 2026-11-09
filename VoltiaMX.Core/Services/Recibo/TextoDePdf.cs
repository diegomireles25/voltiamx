using UglyToad.PdfPig;

namespace VoltiaMX.Core.Services;

/// <summary>
/// El texto de un PDF digital (Fase 9k-0), leído directo de su capa de texto con PdfPig: sin internet y sin OCR. Si el
/// PDF no tiene texto (por ejemplo, una foto guardada como PDF) o no se puede abrir, devuelve una lista vacía y la app
/// usa el OCR. El texto solo vive en memoria: no se guarda ni se registra.
/// </summary>
public static class TextoDePdf
{
    /// <summary>
    /// Igual que el OCR: solo las primeras páginas (el recibo de CFE trae lo necesario en la primera).
    /// </summary>
    public const int PaginasMaximas = 2;

    public static IReadOnlyList<string> Lineas(string ruta)
    {
        try
        {
            using var documento = PdfDocument.Open(ruta);
            var lineas = new List<string>();
            foreach (var pagina in documento.GetPages().Take(PaginasMaximas))
            {
                // PdfPig mide desde abajo; se convierte a "desde arriba" para agrupar igual que el OCR.
                lineas.AddRange(LineasPorPosicion.Agrupar(pagina.GetWords().Select(p => new PalabraUbicada(
                    p.Text, p.BoundingBox.Left, pagina.Height - p.BoundingBox.Top, p.BoundingBox.Height))));
            }

            return lineas;
        }
        catch (Exception)
        {
            // Un archivo dañado, cifrado o que no es PDF: se intenta con el OCR.
            return [];
        }
    }

    /// <summary>
    /// El texto sirve si trae alguna etiqueta del recibo; si no, se usa el OCR.
    /// </summary>
    public static bool TieneTextoUtil(IReadOnlyList<string> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        return lineas.Any(l =>
            l.Contains("periodo", StringComparison.OrdinalIgnoreCase)
            || l.Contains("período", StringComparison.OrdinalIgnoreCase)
            || l.Contains("total a pagar", StringComparison.OrdinalIgnoreCase));
    }
}
