using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Una etiqueta y su valor tal como aparecen en el recibo.
/// </summary>
internal sealed record LineaPdf(string Etiqueta, string Valor);

/// <summary>
/// PDF del reparto: un recibo por inquilino y un resumen. Las cifras salen del reparto guardado, sin
/// recalcular nada. Sin dirección, teléfono ni correo: solo el nombre o unidad del inquilino.
/// </summary>
public static class GeneradorPdfReparto
{
    /// <summary>
    /// Recibo A4 del inquilino en la posición <paramref name="indice"/> de las líneas del reparto.
    /// </summary>
    public static byte[] Recibo(RepartoGuardado reparto, int indice, Func<string, string> texto, OpcionesPdf? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(reparto);
        ArgumentNullException.ThrowIfNull(texto);
        ArgumentOutOfRangeException.ThrowIfNegative(indice);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(indice, reparto.Lineas.Count);
        var (titulos, cuerpo) = GeneradorPdf.Registrar(opciones);
        var (datos, calculo) = LineasRecibo(reparto, indice, texto);
        var linea = reparto.Lineas[indice];

        return Documento(reparto, texto, titulos, cuerpo, texto("RepartoPdf_Titulo"), columna =>
        {
            columna.Item().Text(texto("RepartoPdf_Titulo")).FontFamily(titulos).Bold().FontSize(28);
            foreach (var d in datos)
            {
                Dato(columna, d);
            }

            columna.Item().PaddingTop(8).Element(c => Titulo(c, texto("RepartoPdf_FormaCalculo"), titulos));
            columna.Item().Text(texto($"RepartoPdf_Modo_{reparto.Modo}"));
            foreach (var d in calculo)
            {
                Dato(columna, d);
            }

            columna.Item().Text(texto("RepartoPdf_SinRangos")).FontColor(Colors.Grey.Darken2).FontSize(9);

            columna.Item().PaddingTop(12).Background(Colors.Black).Padding(16).Row(fila =>
            {
                fila.RelativeItem().AlignMiddle().Text(texto("RepartoPdf_Cuota")).FontFamily(titulos).Bold().FontSize(18).FontColor(Colors.White);
                fila.AutoItem().AlignMiddle().Text(FormatoReporte.Pesos(linea.Cuota)).FontFamily(titulos).Bold().FontSize(28).FontColor(Colors.White);
            });

            columna.Item().PaddingTop(12).Text(texto("RepartoPdf_Nota")).Italic().FontSize(9);
        });
    }

    /// <summary>
    /// Resumen del reparto con la tabla de todos los inquilinos, lo que absorbe el propietario y los avisos.
    /// </summary>
    public static byte[] Resumen(RepartoGuardado reparto, Func<string, string> texto, OpcionesPdf? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(reparto);
        ArgumentNullException.ThrowIfNull(texto);
        var (titulos, cuerpo) = GeneradorPdf.Registrar(opciones);

        return Documento(reparto, texto, titulos, cuerpo, texto("RepartoPdf_ResumenTitulo"), columna =>
        {
            columna.Item().Text(texto("RepartoPdf_ResumenTitulo")).FontFamily(titulos).Bold().FontSize(24);
            Dato(columna, new(texto("RepartoPdf_Lugar"), reparto.NombreLugar));
            Dato(columna, new(texto("RepartoPdf_Periodo"), Periodo(reparto)));
            foreach (var d in Calculo(reparto, texto))
            {
                Dato(columna, d);
            }

            columna.Item().PaddingTop(8).Table(tabla =>
            {
                tabla.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(1);
                    c.RelativeColumn(1);
                    c.RelativeColumn(1);
                });
                foreach (var titulo in new[] { texto("RepartoPdf_Unidad"), texto("RepartoPdf_ColLecturas"), texto("RepartoPdf_ColKwh"), texto("RepartoPdf_ColCuota") })
                {
                    tabla.Cell().BorderBottom(1).PaddingVertical(3).Text(titulo).SemiBold().FontSize(9);
                }

                foreach (var l in reparto.Lineas)
                {
                    Celda(tabla, l.Nombre);
                    Celda(tabla, $"{FormatoReporte.Kwh(l.LecturaAnterior)} – {FormatoReporte.Kwh(l.LecturaActual)}");
                    Celda(tabla, FormatoReporte.Kwh(l.Kwh));
                    Celda(tabla, FormatoReporte.Pesos(l.Cuota));
                }
            });

            Dato(columna, new(texto("RepartoPdf_TotalCobrado"), FormatoReporte.Pesos(reparto.TotalCobrado)));
            Dato(columna, new(texto("RepartoPdf_Absorbe"), FormatoReporte.Pesos(reparto.AbsorbePropietario)));

            columna.Item().PaddingTop(8).Element(c => Titulo(c, texto("RepartoPdf_Avisos"), titulos));
            if (reparto.Avisos.Count == 0)
            {
                columna.Item().Text(texto("RepartoPdf_SinAvisos"));
            }

            foreach (var aviso in reparto.Avisos)
            {
                columna.Item().Text(texto($"Reparto_Aviso_{aviso}"));
            }

            columna.Item().Text(texto("RepartoPdf_SinRangos")).FontColor(Colors.Grey.Darken2).FontSize(9);
            columna.Item().PaddingTop(12).Text(texto("RepartoPdf_Nota")).Italic().FontSize(9);
        });
    }

    /// <summary>
    /// Datos del recibo (lugar, periodo, inquilino, lecturas) y forma de cálculo, en el orden en que se muestran.
    /// </summary>
    internal static (IReadOnlyList<LineaPdf> Datos, IReadOnlyList<LineaPdf> Calculo) LineasRecibo(
        RepartoGuardado reparto, int indice, Func<string, string> texto)
    {
        var linea = reparto.Lineas[indice];
        IReadOnlyList<LineaPdf> datos =
        [
            new(texto("RepartoPdf_Lugar"), reparto.NombreLugar),
            new(texto("RepartoPdf_Periodo"), Periodo(reparto)),
            new(texto("RepartoPdf_Inquilino"), linea.Nombre),
            new(texto("RepartoPdf_LecturaAnterior"), FormatoReporte.Kwh(linea.LecturaAnterior)),
            new(texto("RepartoPdf_LecturaActual"), FormatoReporte.Kwh(linea.LecturaActual)),
            new(texto("RepartoPdf_Kwh"), $"{FormatoReporte.Kwh(linea.Kwh)} kWh"),
        ];
        return (datos, Calculo(reparto, texto));
    }

    private static IReadOnlyList<LineaPdf> Calculo(RepartoGuardado reparto, Func<string, string> texto) =>
    [
        new(texto("RepartoPdf_Total"), $"{FormatoReporte.Pesos(reparto.TotalRepartido)} ({texto($"RepartoPdf_Origen_{reparto.Origen}")})"),
        new(texto("RepartoPdf_KwhPrincipal"), $"{FormatoReporte.Kwh(reparto.KwhPrincipal)} kWh"),
        new(texto("RepartoPdf_KwhRepartidos"), $"{FormatoReporte.Kwh(reparto.KwhRepartidos)} kWh"),
        new(texto("RepartoPdf_PrecioPromedio"), reparto.PrecioPromedioKwh is { } precio ? FormatoReporte.Precio(precio) : "—"),
    ];

    private static string Periodo(RepartoGuardado reparto) => $"{FormatoReporte.Fecha(reparto.Inicio)} – {FormatoReporte.Fecha(reparto.Fin)}";

    private static byte[] Documento(
        RepartoGuardado reparto, Func<string, string> texto, string titulos, string cuerpo, string tituloDocumento, Action<ColumnDescriptor> contenido)
        => Document
            .Create(documento => documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(40);
                pagina.PageColor(Colors.White);
                pagina.DefaultTextStyle(t => t.FontFamily(cuerpo).FontSize(10).FontColor(Colors.Black));

                pagina.Header().BorderBottom(1).PaddingBottom(4).Row(fila =>
                {
                    fila.RelativeItem().Text("VOLTIAMX").FontFamily(titulos).Bold().FontSize(12);
                    if (reparto.EsDemo)
                    {
                        fila.AutoItem().Background(Colors.Black).PaddingHorizontal(6).PaddingVertical(2)
                            .Text(texto("Reporte_DatosDeEjemplo")).FontColor(Colors.White).Bold().FontSize(9);
                    }
                });
                pagina.Content().PaddingVertical(12).Column(columna =>
                {
                    columna.Spacing(6);
                    contenido(columna);
                });
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(8));
                    t.Span($"{texto("Reporte_Pagina")} ");
                    t.CurrentPageNumber();
                    t.Span($" {texto("Reporte_De")} ");
                    t.TotalPages();
                });
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = tituloDocumento,
                Author = "VoltiaMX",
                Creator = "VoltiaMX",
                CreationDate = reparto.FechaCreacion,
            })
            .GeneratePdf();

    private static void Titulo(IContainer c, string texto, string titulos)
        => c.BorderBottom(1).PaddingBottom(3).Text(texto).FontFamily(titulos).Bold().FontSize(14);

    private static void Dato(ColumnDescriptor columna, LineaPdf dato)
        => columna.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).Row(fila =>
        {
            fila.RelativeItem(3).Text(dato.Etiqueta);
            GeneradorPdf.Valor(fila.RelativeItem(2).AlignRight(), dato.Valor).SemiBold();
        });

    private static void Celda(TableDescriptor tabla, string valor)
        => GeneradorPdf.Valor(tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3), valor);
}
