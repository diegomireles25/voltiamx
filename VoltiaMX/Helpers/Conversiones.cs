using Microsoft.UI.Xaml.Media;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Helpers;

/// <summary>
/// Funciones para x:Bind (enlaces a funciones) que traducen estado del ViewModel a valores de UI.
/// </summary>
public static class Conversiones
{
    public static bool No(bool? valor) => valor != true;

    public static Visibility VisibleSi(bool? valor) => valor == true ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility VisibleSiNo(bool? valor) => valor == true ? Visibility.Collapsed : Visibility.Visible;

    public static Visibility VisibleSiHayTexto(string? texto)
        => string.IsNullOrWhiteSpace(texto) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Semáforo DAC: Bajo en mint, Medio en amarillo y Alto en negro con texto amarillo.
    /// </summary>
    public static Style EstiloDac(NivelRiesgoDac nivel) => (Style)Application.Current.Resources[nivel switch
    {
        NivelRiesgoDac.Alto => "RiskHighTagStyle",
        NivelRiesgoDac.Medio => "YellowTagStyle",
        _ => "MintTagStyle",
    }];

    public static Style EstiloBarra(bool esActual)
        => (Style)Application.Current.Resources[esActual ? "ChartBarActualStyle" : "ChartBarStyle"];

    public static Brush BrochaEtiquetaBarra(bool esActual)
        => (Brush)Application.Current.Resources[esActual ? "CarbonBlackBrush" : "SlateBrush"];
}
