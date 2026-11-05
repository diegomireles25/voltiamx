using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Fuentes para el PDF (los bytes de los .ttf); la app las lee de sus Assets. Sin fuentes se usa Lato, que viene con QuestPDF.
/// </summary>
public sealed record OpcionesPdf(byte[]? FuenteTitulos = null, byte[]? FuenteCuerpo = null);

/// <summary>
/// "Reporte del periodo" en PDF (A4 vertical): portada, periodo actual con su desglose, historial,
/// riesgo DAC, meta y aparatos. Las cifras salen tal cual de <see cref="ReporteDatos"/>; las secciones
/// sin datos lo dicen. Los textos del documento llegan en <c>texto(clave)</c>.
/// </summary>
public static class GeneradorPdf
{
    // Familias tal como vienen en los archivos de fuente de la app.
    private const string FamiliaTitulos = "Barlow Condensed";
    private const string FamiliaCuerpo = "Inter";
    private const string FamiliaRespaldo = "Lato";

    private static readonly object Candado = new();
    private static bool _titulosRegistrados;
    private static bool _cuerpoRegistrado;

    static GeneradorPdf()
    {
        // VoltiaMX usa la Community License de QuestPDF (ver los créditos del README).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generar(ReporteDatos datos, Func<string, string> texto, OpcionesPdf? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(texto);
        var (titulos, cuerpo) = Registrar(opciones);

        return Document
            .Create(documento => documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(40);
                pagina.PageColor(Colors.White);
                pagina.DefaultTextStyle(t => t.FontFamily(cuerpo).FontSize(10).FontColor(Colors.Black));

                pagina.Header().Element(c => Encabezado(c, datos, texto, titulos));
                pagina.Content().PaddingVertical(12).Column(columna =>
                {
                    columna.Spacing(18);
                    columna.Item().Element(c => Portada(c, datos, texto, titulos));
                    columna.Item().Element(c => Periodo(c, datos, texto, titulos));
                    columna.Item().Element(c => Historial(c, datos, texto, titulos));
                    columna.Item().Element(c => Dac(c, datos, texto, titulos));
                    columna.Item().Element(c => Meta(c, datos, texto, titulos));
                    columna.Item().Element(c => Aparatos(c, datos, texto, titulos));
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
                Title = texto("Reporte_Titulo"),
                Author = "VoltiaMX",
                Creator = "VoltiaMX",
                CreationDate = datos.GeneradoEl,
            })
            .GeneratePdf();
    }

    /// <summary>
    /// Registra las fuentes una sola vez; si no llegan o no se pueden leer, se usa la de respaldo.
    /// </summary>
    internal static (string Titulos, string Cuerpo) Registrar(OpcionesPdf? opciones)
    {
        lock (Candado)
        {
            _titulosRegistrados |= Intentar(opciones?.FuenteTitulos, _titulosRegistrados);
            _cuerpoRegistrado |= Intentar(opciones?.FuenteCuerpo, _cuerpoRegistrado);
            return (_titulosRegistrados ? FamiliaTitulos : FamiliaRespaldo, _cuerpoRegistrado ? FamiliaCuerpo : FamiliaRespaldo);
        }

        static bool Intentar(byte[]? fuente, bool yaRegistrada)
        {
            if (yaRegistrada || fuente is not { Length: > 0 })
            {
                return yaRegistrada;
            }

            try
            {
                FontManager.RegisterFontFromBinaryData(fuente);
                return true;
            }
            catch (Exception)
            {
                // Una fuente dañada no impide el reporte.
                return false;
            }
        }
    }

    private static void Encabezado(IContainer c, ReporteDatos datos, Func<string, string> texto, string titulos)
        => c.BorderBottom(1).PaddingBottom(4).Row(fila =>
        {
            fila.RelativeItem().Text("VOLTIAMX").FontFamily(titulos).Bold().FontSize(12);
            if (datos.EsDemo)
            {
                fila.AutoItem().Background(Colors.Black).PaddingHorizontal(6).PaddingVertical(2)
                    .Text(texto("Reporte_DatosDeEjemplo")).FontColor(Colors.White).Bold().FontSize(9);
            }
        });

    private static void Titulo(IContainer c, string texto, string titulos)
        => c.BorderBottom(1).PaddingBottom(3).Text(texto).FontFamily(titulos).Bold().FontSize(14);

    private static void SinDatos(ColumnDescriptor columna, Func<string, string> texto)
        => columna.Item().Text(texto("Reporte_SinDatos")).Italic().FontColor(Colors.Grey.Darken2);

    private static void Dato(ColumnDescriptor columna, string etiqueta, string valor)
        => columna.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).Row(fila =>
        {
            fila.RelativeItem(3).Text(etiqueta);
            Valor(fila.RelativeItem(2).AlignRight(), valor).SemiBold();
        });

    private static void Portada(IContainer c, ReporteDatos datos, Func<string, string> texto, string titulos)
        => c.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Text(texto("Reporte_Titulo")).FontFamily(titulos).Bold().FontSize(28);
            Dato(columna, texto("Reporte_Lugar"), datos.Lugar?.Nombre ?? texto("Reporte_SinDatos"));
            Dato(columna, texto("Reporte_Tarifa"), datos.Tarifa?.Nombre ?? datos.Resumen?.NombreTarifa ?? texto("Reporte_SinDatos"));
            Dato(columna, texto("Reporte_Generado"), FormatoReporte.Fecha(datos.GeneradoEl));
            columna.Item().PaddingTop(4).Text(texto("Reporte_SinIvaNiDap")).FontColor(Colors.Grey.Darken2);
            // Un PDF no abre la ayuda "¿Qué es esto?": la explicación va escrita.
            columna.Item().Text(texto("Ayuda_AntesDeImpuestos")).FontColor(Colors.Grey.Darken2).FontSize(9);
            if (datos.PreciosDeEjemplo)
            {
                columna.Item().PaddingTop(4).Text(texto("Reporte_PreciosDeEjemplo")).Bold();
            }
        });

    private static void Periodo(IContainer c, ReporteDatos datos, Func<string, string> texto, string titulos)
        => c.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Element(t => Titulo(t, texto("Reporte_SeccionPeriodo"), titulos));
            if (datos.Resumen is not { } r || (r.KwhHastaHoy is null && r.MontoProyectado is null))
            {
                SinDatos(columna, texto);
                return;
            }

            Dato(columna, texto("Reporte_Periodo"), $"{FormatoReporte.Fecha(r.InicioPeriodo)} – {FormatoReporte.Fecha(r.FinPeriodo)}");
            if (r.KwhHastaHoy is decimal hastaHoy)
            {
                Dato(columna, texto("Reporte_KwhHastaHoy"), FormatoReporte.Kwh(hastaHoy));
            }

            if (r.KwhProyectados is decimal proyectados)
            {
                Dato(columna, texto("Reporte_KwhProyectados"), FormatoReporte.Kwh(proyectados));
            }

            if (r.MontoProyectado is decimal monto)
            {
                Dato(columna, texto("Reporte_PagoProyectado"), FormatoReporte.Pesos(monto));
            }

            // El presupuesto no se imprime desde la Fase 7a.

            if (r.DesgloseProyectado is not { } desglose)
            {
                return;
            }

            // El mismo desglose del Predictor y del Dashboard, a 2 decimales.
            columna.Item().PaddingTop(8).Text(texto("Reporte_Desglose")).SemiBold();
            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(3);
                    d.RelativeColumn(2);
                    d.RelativeColumn(2);
                    d.RelativeColumn(2);
                });
                Cabecera(tabla, texto("Reporte_Rango"), texto("Reporte_Kwh"), texto("Reporte_Precio"), texto("Reporte_Subtotal"));
                foreach (var rango in desglose.Desglose)
                {
                    Celdas(tabla, rango.Nombre, FormatoReporte.Kwh(rango.Kwh), FormatoReporte.Precio(rango.PrecioPorKwh), FormatoReporte.Pesos(rango.Subtotal));
                }

                Celdas(tabla, texto("Reporte_CargoFijo"), string.Empty, string.Empty, FormatoReporte.Pesos(desglose.CargoFijo));
                Celdas(tabla, texto("Reporte_Total"), string.Empty, string.Empty, FormatoReporte.Pesos(desglose.Total), negritas: true);
            });
        });

    private static void Historial(IContainer c, ReporteDatos datos, Func<string, string> texto, string titulos)
        => c.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Element(t => Titulo(t, texto("Reporte_SeccionHistorial"), titulos));
            if (datos.Periodos.Count == 0)
            {
                SinDatos(columna, texto);
                return;
            }

            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(2);
                    d.RelativeColumn(2);
                    d.RelativeColumn(2);
                    d.RelativeColumn(2);
                    d.RelativeColumn(2);
                });
                tabla.Header(h => Cabecera(h, texto("Reporte_Inicio"), texto("Reporte_Fin"), texto("Reporte_Kwh"), texto("Reporte_Monto"), string.Empty));
                foreach (var periodo in datos.Periodos)
                {
                    var marca = periodo.EsActual ? texto("Reporte_Actual") : periodo.Estimado ? texto("Reporte_Estimado") : string.Empty;
                    Celdas(tabla, FormatoReporte.Fecha(periodo.Inicio), FormatoReporte.Fecha(periodo.Fin), FormatoReporte.Kwh(periodo.Kwh), FormatoReporte.Pesos(periodo.Monto), marca);
                }
            });

            if (datos.Periodos.Any(p => p.Estimado))
            {
                columna.Item().Text(texto("Reporte_NotaEstimado")).FontSize(8).FontColor(Colors.Grey.Darken2);
            }
        });

    private static void Dac(IContainer c, ReporteDatos datos, Func<string, string> texto, string titulos)
        => c.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Element(t => Titulo(t, texto("Reporte_SeccionDac"), titulos));
            if (datos.Resumen?.Dac is not { } dac || datos.Resumen.LimiteDacKwhMensual is not decimal limite)
            {
                SinDatos(columna, texto);
                return;
            }

            Dato(columna, texto("Reporte_Nivel"), texto($"Dac_{dac.Nivel}"));
            Dato(columna, texto("Reporte_Promedio"), FormatoReporte.Kwh(dac.PromedioMensualKwh));
            Dato(columna, texto("Reporte_Limite"), FormatoReporte.Kwh(limite));
            Dato(columna, texto("Reporte_Margen"), FormatoReporte.Kwh(dac.MargenKwh));
            columna.Item().Text(texto("Reporte_NotaDac")).FontSize(8).FontColor(Colors.Grey.Darken2);
        });

    private static void Meta(IContainer c, ReporteDatos datos, Func<string, string> texto, string titulos)
        => c.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Element(t => Titulo(t, texto("Reporte_SeccionMeta"), titulos));
            if (datos.Meta is not { } meta)
            {
                SinDatos(columna, texto);
                return;
            }

            Dato(columna, texto("Reporte_MetaPorcentaje"), $"{meta.PorcentajeObjetivo}%");
            Dato(columna, texto("Reporte_Estado"), texto($"EstadoMeta_{meta.Estado}"));
            if (meta.ObjetivoKwh is decimal objetivo)
            {
                Dato(columna, texto("Reporte_Objetivo"), FormatoReporte.Kwh(objetivo));
            }

            if (meta.ConsumidoKwh is decimal consumido)
            {
                Dato(columna, texto("Reporte_Consumido"), FormatoReporte.Kwh(consumido));
            }

            Dato(columna, texto("Reporte_Racha"), meta.Racha.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });

    private static void Aparatos(IContainer c, ReporteDatos datos, Func<string, string> texto, string titulos)
        => c.Column(columna =>
        {
            columna.Spacing(4);
            columna.Item().Element(t => Titulo(t, texto("Reporte_SeccionAparatos"), titulos));
            if (datos.Ranking is not { Filas.Count: > 0 } ranking)
            {
                SinDatos(columna, texto);
                return;
            }

            columna.Item().Table(tabla =>
            {
                tabla.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(4);
                    d.RelativeColumn(2);
                    d.RelativeColumn(2);
                });
                tabla.Header(h => Cabecera(h, texto("Reporte_Aparato"), texto("Reporte_KwhMes"), texto("Reporte_Porcentaje")));
                foreach (var fila in ranking.Filas)
                {
                    Celdas(tabla, fila.Nombre, FormatoReporte.Kwh(fila.KwhPorMes), fila.Porcentaje is decimal p ? FormatoReporte.Porcentaje(p) : "—");
                }

                if (ranking.SinIdentificar is decimal sinIdentificar)
                {
                    Celdas(tabla, texto("Reporte_SinIdentificar"), string.Empty, FormatoReporte.Porcentaje(sinIdentificar));
                }
            });
            columna.Item().Text(texto("Reporte_NotaAparatos")).FontSize(8).FontColor(Colors.Grey.Darken2);
        });

    // Palabras más largas que esto (p. ej. un nombre escrito sin espacios) se cortan donde haga falta;
    // el resto del texto se acomoda por palabras.
    private const int PalabraMasLarga = 28;

    internal static TextBlockDescriptor Valor(IContainer contenedor, string valor)
    {
        var texto = contenedor.Text(valor);
        return valor.Split(' ').Any(palabra => palabra.Length > PalabraMasLarga) ? texto.BreakAnywhere() : texto;
    }

    private static void Cabecera(TableCellDescriptor tabla, params string[] titulos)
    {
        foreach (var titulo in titulos)
        {
            tabla.Cell().BorderBottom(1).PaddingVertical(3).Text(titulo).SemiBold().FontSize(9);
        }
    }

    private static void Cabecera(TableDescriptor tabla, params string[] titulos)
    {
        foreach (var titulo in titulos)
        {
            tabla.Cell().BorderBottom(1).PaddingVertical(3).Text(titulo).SemiBold().FontSize(9);
        }
    }

    private static void Celdas(TableDescriptor tabla, string primera, params string[] resto) => Celdas(tabla, [primera, .. resto], negritas: false);

    private static void Celdas(TableDescriptor tabla, string a, string b, string c, string d, bool negritas)
        => Celdas(tabla, [a, b, c, d], negritas);

    private static void Celdas(TableDescriptor tabla, string[] valores, bool negritas)
    {
        foreach (var valor in valores)
        {
            var texto = Valor(tabla.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3), valor);
            if (negritas)
            {
                texto.SemiBold();
            }
        }
    }
}
