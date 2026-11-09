using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9k-0: el texto de un PDF digital (los recibos de CFE suelen tenerlo) se lee directo, palabra por palabra con su
/// posición, y se arma en líneas por altura. Todo con datos SINTÉTICOS.
/// </summary>
public sealed class TextoDePdfTests
{
    static TextoDePdfTests() => QuestPDF.Settings.License = LicenseType.Community;

    // ---- Agrupar palabras por altura ----

    [Fact]
    public void Agrupar_PalabrasDeLaMismaAltura_UnaLinea_DeIzquierdaADerecha()
    {
        // Las columnas de una tabla llegan sueltas y en desorden; quedan en su fila y en orden.
        PalabraUbicada[] palabras =
        [
            new("300.00", 300, 101, 10),
            new("Básico", 10, 100, 10),
            new("1.000", 200, 100.5, 10),
            new("300", 100, 99.5, 10),
            new("Intermedio1", 10, 120, 10),
            new("360.00", 300, 120, 10),
        ];

        Assert.Equal(["Básico 300 1.000 300.00", "Intermedio1 360.00"], LineasPorPosicion.Agrupar(palabras));
    }

    [Fact]
    public void Agrupar_RenglonesCercanosPeroDistintos_NoSeMezclan()
    {
        // Dos renglones separados por más de la mitad de la altura de la letra.
        PalabraUbicada[] palabras = [new("TARIFA:1C", 10, 50, 10), new("MULTIPLICADOR:1", 10, 62, 10)];

        Assert.Equal(["TARIFA:1C", "MULTIPLICADOR:1"], LineasPorPosicion.Agrupar(palabras));
        Assert.Empty(LineasPorPosicion.Agrupar([]));
    }

    // ---- Leer un PDF con texto ----

    private static string PdfDePrueba(Action<ColumnDescriptor> contenido)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"voltiamx-prueba-{Guid.NewGuid():N}.pdf");
        Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.Letter);
            p.Margin(40);
            p.Content().Column(contenido);
        })).GeneratePdf(ruta);
        return ruta;
    }

    [Fact]
    public void Lineas_DeUnPdfConTexto_IncluyeLasFilasDeLaTabla()
    {
        // Recibo sintético: etiquetas pegadas y una fila de la tabla hecha con columnas separadas.
        var ruta = PdfDePrueba(col =>
        {
            col.Item().Text("TARIFA:1CNO. MEDIDOR:123ABC");
            col.Item().Text("PERIODO FACTURADO:12 JUN 26-10 AGO 26");
            col.Item().Row(fila =>
            {
                fila.RelativeItem().Text("Básico");
                fila.RelativeItem().AlignRight().Text("300");
                fila.RelativeItem().AlignRight().Text("1.000");
                fila.RelativeItem().AlignRight().Text("300.00");
            });
        });
        try
        {
            var lineas = TextoDePdf.Lineas(ruta);

            Assert.Contains("TARIFA:1CNO. MEDIDOR:123ABC", lineas);
            Assert.Contains("PERIODO FACTURADO:12 JUN 26-10 AGO 26", lineas);
            Assert.Contains("Básico 300 1.000 300.00", lineas);
        }
        finally
        {
            File.Delete(ruta);
        }
    }

    [Fact]
    public void Lineas_PdfSinTexto_ListaVacia_YArchivoQueNoEsPdf_ListaVacia()
    {
        var ruta = PdfDePrueba(col => col.Item().Height(50).Background(Colors.Grey.Lighten2));
        var noPdf = Path.Combine(Path.GetTempPath(), $"voltiamx-prueba-{Guid.NewGuid():N}.pdf");
        File.WriteAllText(noPdf, "esto no es un PDF");
        try
        {
            Assert.Empty(TextoDePdf.Lineas(ruta));
            Assert.Empty(TextoDePdf.Lineas(noPdf));
        }
        finally
        {
            File.Delete(ruta);
            File.Delete(noPdf);
        }
    }

    [Fact]
    public void TieneTextoUtil_SoloConEtiquetasDelRecibo()
    {
        Assert.True(TextoDePdf.TieneTextoUtil(["algo", "PERIODO FACTURADO:12 JUN 26-10 AGO 26"]));
        Assert.True(TextoDePdf.TieneTextoUtil(["TOTAL A PAGAR $1,752"]));
        Assert.False(TextoDePdf.TieneTextoUtil(["Página 1"]));
        Assert.False(TextoDePdf.TieneTextoUtil([]));
    }
}
