using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Lo que lleva el PDF: los datos del lugar, los supuestos y el resultado tal como se mostraron en pantalla.
/// La referencia (hasta 40 caracteres) solo va en el PDF y no se guarda.
/// </summary>
public sealed record DatosPdfSolar(
    DatosLugarSolar Lugar,
    ParametrosSolar Parametros,
    ResultadoSolar Resultado,
    string? Referencia,
    DateTime GeneradoEl);

/// <summary>
/// Secciones del PDF en el orden en que se muestran.
/// </summary>
internal sealed record SeccionesPdfSolar(
    IReadOnlyList<LineaPdf> Encabezado,
    IReadOnlyList<LineaPdf> Parametros,
    IReadOnlyList<LineaPdf> Resultado,
    IReadOnlyList<LineaPdf> Dac,
    IReadOnlyList<string> Avisos);

/// <summary>
/// "ESTIMACIÓN SOLAR" en PDF (A4). No recalcula: imprime el <see cref="ResultadoSolar"/> que recibe.
/// Sin dirección ni datos de contacto.
/// </summary>
public static class GeneradorPdfSolar
{
    public const int LargoMaximoReferencia = 40;

    public static byte[] Generar(DatosPdfSolar datos, Func<string, string> texto, OpcionesPdf? opciones = null)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ArgumentNullException.ThrowIfNull(texto);
        var (titulos, cuerpo) = GeneradorPdf.Registrar(opciones);
        var s = Secciones(datos, texto);

        return Document
            .Create(documento => documento.Page(pagina =>
            {
                pagina.Size(PageSizes.A4);
                pagina.Margin(40);
                pagina.PageColor(Colors.White);
                pagina.DefaultTextStyle(t => t.FontFamily(cuerpo).FontSize(10).FontColor(Colors.Black));

                pagina.Header().BorderBottom(1).PaddingBottom(4).Row(fila =>
                {
                    fila.RelativeItem().Text("VOLTIAMX").FontFamily(titulos).Bold().FontSize(12);
                    if (datos.Lugar.EsDemo)
                    {
                        fila.AutoItem().Background(Colors.Black).PaddingHorizontal(6).PaddingVertical(2)
                            .Text(texto("Reporte_DatosDeEjemplo")).FontColor(Colors.White).Bold().FontSize(9);
                    }
                });
                pagina.Content().PaddingVertical(12).Column(columna =>
                {
                    columna.Spacing(6);
                    columna.Item().Text(texto("SolarPdf_Titulo")).FontFamily(titulos).Bold().FontSize(28);
                    Datos(columna, s.Encabezado);

                    Titulo(columna, texto("SolarPdf_ParametrosTitulo"), titulos);
                    columna.Item().Text(texto("SolarPdf_ParametrosNota")).FontColor(Colors.Grey.Darken2).FontSize(9);
                    Datos(columna, s.Parametros);

                    Titulo(columna, texto("SolarPdf_ResultadoTitulo"), titulos);
                    columna.Item().Background(Colors.Black).Padding(16).Row(fila =>
                    {
                        fila.RelativeItem().AlignMiddle().Text(texto("SolarPdf_Paneles")).FontFamily(titulos).Bold().FontSize(18).FontColor(Colors.White);
                        fila.AutoItem().AlignMiddle().Text(datos.Resultado.Paneles.ToString(CultureInfo.InvariantCulture))
                            .FontFamily(titulos).Bold().FontSize(28).FontColor(Colors.White);
                    });
                    Datos(columna, s.Resultado);

                    if (s.Dac.Count > 0)
                    {
                        Titulo(columna, texto("SolarPdf_DacTitulo"), titulos);
                        Datos(columna, s.Dac);
                    }

                    columna.Item().PaddingTop(10).Column(avisos =>
                    {
                        avisos.Spacing(4);
                        foreach (var aviso in s.Avisos)
                        {
                            avisos.Item().Text(aviso).Italic().FontSize(9);
                        }
                    });
                });
                pagina.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(st => st.FontSize(8));
                    t.Span($"{texto("Reporte_Pagina")} ");
                    t.CurrentPageNumber();
                    t.Span($" {texto("Reporte_De")} ");
                    t.TotalPages();
                });
            }))
            .WithMetadata(new DocumentMetadata
            {
                Title = texto("SolarPdf_Titulo"),
                Author = "VoltiaMX",
                Creator = "VoltiaMX",
                CreationDate = datos.GeneradoEl,
            })
            .GeneratePdf();
    }

    internal static SeccionesPdfSolar Secciones(DatosPdfSolar d, Func<string, string> texto)
    {
        var p = d.Parametros;
        var r = d.Resultado;

        var encabezado = new List<LineaPdf>
        {
            new(texto("SolarPdf_Lugar"), d.Lugar.NombreLugar.Length > 0 ? d.Lugar.NombreLugar : "—"),
            new(texto("SolarPdf_Fecha"), FormatoReporte.Fecha(d.GeneradoEl)),
            new(texto("SolarPdf_Tarifa"), d.Lugar.Tarifa?.Nombre ?? texto("SolarPdf_SinTarifa")),
        };
        if (Referencia(d.Referencia) is { } referencia)
        {
            encabezado.Add(new(texto("SolarPdf_Referencia"), referencia));
        }

        IReadOnlyList<LineaPdf> parametros =
        [
            new(texto("SolarPdf_Consumo"), $"{FormatoReporte.Kwh(p.ConsumoMensualKwh ?? 0m)} kWh"),
            new(texto("SolarPdf_Porcentaje"), $"{Numero(p.PorcentajeCubrir)} %"),
            new(texto("SolarPdf_Horas"), $"{Numero(p.HorasSol)} h"),
            new(texto("SolarPdf_Eficiencia"), p.Eficiencia.ToString("0.00", CultureInfo.InvariantCulture)),
            new(texto("SolarPdf_WattsPanel"), $"{Numero(p.WattsPanel)} W"),
            new(texto("SolarPdf_Costo"), p.CostoPorKwp is > 0m and decimal costo ? FormatoReporte.Pesos(costo) : texto("SolarPdf_SinCosto")),
        ];

        var resultado = new List<LineaPdf>
        {
            new(texto("SolarPdf_Kwp"), $"{FormatoReporte.Kwh(r.KwpInstalados)} kWp"),
            new(texto("SolarPdf_Generacion"), $"{FormatoReporte.Kwh(r.GeneracionMensualKwh)} kWh"),
            new(texto("SolarPdf_Aprovechados"), $"{FormatoReporte.Kwh(r.KwhAprovechadosMes)} kWh"),
            new(texto("SolarPdf_Excedente"), $"{FormatoReporte.Kwh(r.ExcedenteKwhMes)} kWh"),
        };
        if (r.AhorroMes is decimal mes && r.AhorroAnio is decimal anio)
        {
            resultado.Add(new(texto("SolarPdf_AhorroMes"), FormatoReporte.Pesos(mes)));
            resultado.Add(new(texto("SolarPdf_AhorroAnio"), FormatoReporte.Pesos(anio)));
        }
        else
        {
            resultado.Add(new(texto("SolarPdf_Ahorro"), texto($"Solar_SinPesos_{r.EstadoPesos}")));
        }

        if (r.Inversion is decimal inversion)
        {
            resultado.Add(new(texto("SolarPdf_Inversion"), FormatoReporte.Pesos(inversion)));
            resultado.Add(new(texto("SolarPdf_Recuperacion"), Recuperacion(r, texto)));
        }
        else
        {
            resultado.Add(new(texto("SolarPdf_Inversion"), texto("SolarPdf_SinCosto")));
        }

        IReadOnlyList<LineaPdf> dac = r is { DacAntes: { } antes, DacDespues: { } despues }
            ?
            [
                new(texto("SolarPdf_DacAntes"), $"{texto($"Dac_{antes.Nivel}")} · {FormatoReporte.Kwh(antes.PromedioMensualKwh)} kWh"),
                new(texto("SolarPdf_DacDespues"), $"{texto($"Dac_{despues.Nivel}")} · {FormatoReporte.Kwh(despues.PromedioMensualKwh)} kWh"),
            ]
            : [];

        IReadOnlyList<string> avisos =
        [
            texto("Solar_AvisoEstimacion"),
            texto("Solar_AvisoNoIncluye"),
            texto("SolarPdf_SinSupuestos"),
            texto("SolarPdf_SinIvaNiDap"),
        ];

        return new SeccionesPdfSolar(encabezado, parametros, resultado, dac, avisos);
    }

    /// <summary>
    /// Texto de la recuperación: meses y años, o "más de 20 años"; vacío si no hay ahorro.
    /// </summary>
    public static string Recuperacion(ResultadoSolar r, Func<string, string> texto)
    {
        ArgumentNullException.ThrowIfNull(r);
        ArgumentNullException.ThrowIfNull(texto);
        if (r.RecuperacionMeses is not decimal meses)
        {
            return texto("SolarPdf_SinRecuperacion");
        }

        return r.RecuperacionExcedeMaximo
            ? texto("Solar_RecuperacionMasDe20")
            : string.Format(CultureInfo.InvariantCulture, texto("Solar_RecuperacionMeses"), Math.Round(meses, 1), Math.Round(meses / 12m, 1));
    }

    private static string? Referencia(string? referencia)
    {
        var limpia = referencia?.Trim();
        return string.IsNullOrEmpty(limpia) ? null : limpia.Length > LargoMaximoReferencia ? limpia[..LargoMaximoReferencia] : limpia;
    }

    private static string Numero(decimal valor) => valor.ToString("#,##0.##", CultureInfo.InvariantCulture);

    private static void Titulo(ColumnDescriptor columna, string texto, string titulos)
        => columna.Item().PaddingTop(8).BorderBottom(1).PaddingBottom(3).Text(texto).FontFamily(titulos).Bold().FontSize(14);

    private static void Datos(ColumnDescriptor columna, IEnumerable<LineaPdf> lineas)
    {
        foreach (var dato in lineas)
        {
            columna.Item().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).PaddingVertical(3).Row(fila =>
            {
                fila.RelativeItem(3).Text(dato.Etiqueta);
                GeneradorPdf.Valor(fila.RelativeItem(2).AlignRight(), dato.Valor).SemiBold();
            });
        }
    }
}
