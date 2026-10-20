using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.Kernel.Sketches;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.Painting.Effects;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using VoltiaMX.ViewModels;
using Color = Windows.UI.Color;

namespace VoltiaMX.Helpers;

/// <summary>
/// Series y ejes de LiveCharts2 para x:Bind. Los ViewModels solo entregan datos; aquí se convierten
/// con los colores, medidas y la fuente mono del sistema de diseño (Colors.xaml, Shapes.xaml,
/// Typography.xaml), sin valores escritos a mano.
/// </summary>
public static class Graficas
{
    private static SKTypeface? _mono;

    private static object Recurso(string clave) => Application.Current.Resources[clave];

    private static SKColor Color(string clave)
    {
        var color = (Color)Recurso(clave);
        return new SKColor(color.R, color.G, color.B, color.A);
    }

    private static float Medida(string clave) => (float)(double)Recurso(clave);

    // Skia no lee ms-appx: se toma el archivo de la fuente que usa MonoFontFamily.
    private static SKTypeface Mono => _mono ??= CargarFuente("MonoFontFamily");

    private static SKTypeface CargarFuente(string clave)
    {
        var origen = ((FontFamily)Recurso(clave)).Source.Split('#')[0].Replace("ms-appx:///", string.Empty);
        var ruta = Path.Combine(AppContext.BaseDirectory, origen.Replace('/', Path.DirectorySeparatorChar));
        return SKTypeface.FromFile(ruta) ?? SKTypeface.Default;
    }

    private static SolidColorPaint Texto(string color) => new(Color(color)) { SKTypeface = Mono };

    private static SolidColorPaint Trazo(string color, PathEffect? efecto = null)
        => new(Color(color)) { StrokeThickness = Medida("GraficaGrosorLinea"), PathEffect = efecto };

    /// <summary>
    /// Tres series superpuestas en las mismas posiciones: cerrados en Ash, estimados en blanco con
    /// borde Ash y el actual en negro. El valor va arriba de cada barra.
    /// </summary>
    public static ISeries[] Barras(IReadOnlyList<BarraPeriodo>? barras)
    {
        if (barras is null || barras.Count == 0)
        {
            return [];
        }

        ColumnSeries<double?> Serie(Func<BarraPeriodo, bool> pertenece, string relleno, string? borde) => new()
        {
            Values = barras.Select(b => pertenece(b) ? b.Valor : (double?)null).ToArray(),
            Fill = new SolidColorPaint(Color(relleno)),
            Stroke = borde is null ? null : new SolidColorPaint(Color(borde)) { StrokeThickness = 1 },
            IgnoresBarPosition = true,
            MaxBarWidth = Medida("GraficaAnchoBarra"),
            Rx = 0,
            Ry = 0,
            IsHoverable = false,
            DataLabelsPaint = Texto("CarbonBlackColor"),
            DataLabelsSize = Medida("FontSizeCaption"),
            DataLabelsPosition = DataLabelsPosition.Top,
            DataLabelsFormatter = punto => barras[punto.Index].ValorTexto,
        };

        return
        [
            Serie(b => !b.EsActual && !b.Estimado, "AshColor", null),
            Serie(b => !b.EsActual && b.Estimado, "PaperWhiteColor", "AshColor"),
            Serie(b => b.EsActual, "CarbonBlackColor", null),
        ];
    }

    public static IEnumerable<ICartesianAxis> EjePeriodos(IReadOnlyList<BarraPeriodo>? barras) =>
    [
        new Axis
        {
            Labels = barras?.Select(b => b.Etiqueta).ToArray() ?? [],
            LabelsPaint = Texto("SlateColor"),
            TextSize = Medida("FontSizeCaption"),
            SeparatorsPaint = null,
            TicksPaint = null,
        },
    ];

    /// <summary>
    /// Eje de valores sin etiquetas ni líneas: los valores van sobre las barras.
    /// </summary>
    public static IEnumerable<ICartesianAxis> EjeValoresOculto() =>
    [
        new Axis { MinLimit = 0, LabelsPaint = null, SeparatorsPaint = null, TicksPaint = null },
    ];

    /// <summary>
    /// Consumo acumulado (línea negra) y, si hay presupuesto, la recta punteada gris que cabe en él.
    /// </summary>
    public static ISeries[] Acumulado(GraficaAcumulado? grafica)
    {
        if (grafica is null)
        {
            return [];
        }

        var series = new List<ISeries>();
        if (grafica.Presupuesto.Count > 0)
        {
            var guion = Medida("GraficaGuion");
            series.Add(new LineSeries<ObservablePoint>
            {
                Values = grafica.Presupuesto.Select(p => new ObservablePoint(p.Dia, p.Valor)).ToArray(),
                Stroke = Trazo("SmokeColor", new DashEffect([guion, guion])),
                Fill = null,
                GeometrySize = 0,
                GeometryFill = null,
                GeometryStroke = null,
                LineSmoothness = 0,
                IsHoverable = false,
            });
        }

        series.Add(new LineSeries<ObservablePoint>
        {
            Values = grafica.Consumo.Select(p => new ObservablePoint(p.Dia, p.Valor)).ToArray(),
            Stroke = Trazo("CarbonBlackColor"),
            Fill = null,
            GeometrySize = Medida("GraficaPunto"),
            GeometryFill = new SolidColorPaint(Color("CarbonBlackColor")),
            GeometryStroke = null,
            LineSmoothness = 0,
            IsHoverable = false,
        });

        return [.. series];
    }

    public static IEnumerable<ICartesianAxis> EjeDias(GraficaAcumulado? grafica) =>
    [
        new Axis
        {
            MinLimit = 0,
            MaxLimit = grafica?.DiasDelPeriodo,
            Labeler = valor => grafica is null ? string.Empty : string.Format(grafica.Cultura, grafica.FormatoDia, valor),
            LabelsPaint = Texto("SlateColor"),
            TextSize = Medida("FontSizeCaption"),
            SeparatorsPaint = null,
            TicksPaint = null,
        },
    ];

    public static IEnumerable<ICartesianAxis> EjeKwh(GraficaAcumulado? grafica) =>
    [
        new Axis
        {
            MinLimit = 0,
            Labeler = valor => grafica is null ? string.Empty : string.Format(grafica.Cultura, grafica.FormatoKwh, valor),
            LabelsPaint = Texto("SlateColor"),
            TextSize = Medida("FontSizeCaption"),
            SeparatorsPaint = new SolidColorPaint(Color("AshColor")) { StrokeThickness = 1 },
            TicksPaint = null,
        },
    ];
}
