using Microsoft.UI.Xaml.Media;
using VoltiaMX.Core.Services;
using VoltiaMX.ViewModels;

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

    // Vista previa de Lecturas: tarjeta blanca, o negra cuando la lectura cierra periodos.
    public static Style EstiloTarjetaPrevia(bool conCierres)
        => (Style)Application.Current.Resources[conCierres ? "InvertedCardStyle" : "CardStyle"];

    public static Style EstiloFilaPrevia(bool enTarjetaOscura)
        => (Style)Application.Current.Resources[enTarjetaOscura ? "HairlineRowOnDarkStyle" : "HairlineRowStyle"];

    public static Brush BrochaSecundaria(bool enTarjetaOscura)
        => (Brush)Application.Current.Resources[enTarjetaOscura ? "AshBrush" : "SlateBrush"];

    // Etiquetas de Lecturas: CORTE en mint; ESTIMADA o ESTIMADO en amarillo; REVISA en negro con amarillo.
    public static Style EstiloEtiquetaLectura(TipoEtiquetaLectura tipo)
        => (Style)Application.Current.Resources[tipo switch
        {
            TipoEtiquetaLectura.Corte => "MintTagStyle",
            TipoEtiquetaLectura.Alerta => "RiskHighTagStyle",
            _ => "YellowTagStyle",
        }];
}
