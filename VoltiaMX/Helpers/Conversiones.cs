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

    // Validar con tu recibo: Coincide en mint, Cercano en amarillo, No coincide en negro con amarillo.
    public static Style EstiloVeredicto(VeredictoRecibo veredicto)
        => (Style)Application.Current.Resources[veredicto switch
        {
            VeredictoRecibo.Coincide => "MintTagStyle",
            VeredictoRecibo.Cercano => "YellowTagStyle",
            _ => "RiskHighTagStyle",
        }];

    // Meta: Cumpliendo en mint, En riesgo en amarillo, Superada en negro con amarillo.
    public static Style EstiloEstadoMeta(EstadoMeta estado)
        => (Style)Application.Current.Resources[estado switch
        {
            EstadoMeta.Cumpliendo => "MintTagStyle",
            EstadoMeta.EnRiesgo => "YellowTagStyle",
            _ => "RiskHighTagStyle",
        }];

    // Historial de la meta: CUMPLIDA en mint, NO CUMPLIDA en amarillo.
    public static Style EstiloCumplida(bool cumplio)
        => (Style)Application.Current.Resources[cumplio ? "MintTagStyle" : "YellowTagStyle"];

    // Recibo escaneado: LEÍDO en mint, REVISAR en amarillo, NO ENCONTRADO en negro con amarillo.
    public static Style EstiloConfianza(Confianza confianza)
        => (Style)Application.Current.Resources[confianza switch
        {
            Confianza.Alta => "MintTagStyle",
            Confianza.Baja => "YellowTagStyle",
            _ => "RiskHighTagStyle",
        }];

    // Botón secundario sobre el lienzo o sobre una tarjeta negra.
    public static Style EstiloBotonSecundario(bool enTarjetaOscura)
        => (Style)Application.Current.Resources[enTarjetaOscura ? "GhostOnDarkButtonStyle" : "GhostButtonStyle"];

    // Recibo explicado del Predictor: la fila del total con más peso.
    public static Style EstiloFilaRecibo(bool esTotal)
        => (Style)Application.Current.Resources[esTotal ? "SubheadingLgTextStyle" : "BodyTextStyle"];

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
