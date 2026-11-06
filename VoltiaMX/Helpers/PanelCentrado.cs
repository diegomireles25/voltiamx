using Windows.Foundation;

namespace VoltiaMX.Helpers;

/// <summary>
/// Contenedor del contenido de cada pantalla: le da al hijo todo el ancho disponible hasta
/// <see cref="AnchoMaximo"/> y lo centra con ese ancho real.
/// </summary>
/// <remarks>
/// Un elemento con HorizontalAlignment Stretch limitado por MaxWidth se centra en WinUI con su ancho
/// DESEADO (el de su contenido), no con el ancho al que quedó: por eso cada pantalla quedaba corrida a la
/// derecha una cantidad distinta. Este panel acomoda al hijo él mismo, así que no depende de eso.
/// </remarks>
public sealed partial class PanelCentrado : Panel
{
    public static readonly DependencyProperty AnchoMaximoProperty = DependencyProperty.Register(
        nameof(AnchoMaximo),
        typeof(double),
        typeof(PanelCentrado),
        new PropertyMetadata(double.PositiveInfinity, (d, _) => ((PanelCentrado)d).InvalidateMeasure()));

    /// <summary>
    /// Ancho máximo del contenido (en la app, ContentMaxWidth desde PageContentHostStyle).
    /// </summary>
    public double AnchoMaximo
    {
        get => (double)GetValue(AnchoMaximoProperty);
        set => SetValue(AnchoMaximoProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var ancho = Math.Min(availableSize.Width, AnchoMaximo);
        double anchoDeseado = 0, alto = 0;
        foreach (var hijo in Children)
        {
            hijo.Measure(new Size(ancho, availableSize.Height));
            anchoDeseado = Math.Max(anchoDeseado, hijo.DesiredSize.Width);
            alto = Math.Max(alto, hijo.DesiredSize.Height);
        }

        // Con ancho finito el contenido ocupa todo lo que puede (hasta el máximo), aunque pida menos.
        return new Size(double.IsInfinity(ancho) ? anchoDeseado : ancho, alto);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var ancho = Math.Min(finalSize.Width, AnchoMaximo);
        var izquierda = (finalSize.Width - ancho) / 2;
        foreach (var hijo in Children)
        {
            hijo.Arrange(new Rect(izquierda, 0, ancho, finalSize.Height));
        }

        return finalSize;
    }
}
