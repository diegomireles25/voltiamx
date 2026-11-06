using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// "Comparado con tu recibo anterior", en pesos (Fase 8, D6): lo que pagarías a este ritmo contra el recibo anterior
/// recalculado con la tarifa actual, con las reglas de <see cref="ComparacionRecibo"/>. Lo usan Inicio y, desde la
/// Fase 9c, Mis recibos.
/// </summary>
public static class TextosComparacion
{
    /// <summary>
    /// La cifra ("+$N", "−$N" o "Parecido") y la frase; <c>Tiene</c> es false sin recibo anterior.
    /// </summary>
    public static (bool Tiene, string Cifra, string Texto) Armar(ITextos textos, ResumenDashboard resumen)
    {
        var comparacion = ComparacionRecibo.Evaluar(resumen.MontoProyectado, resumen.MontoReciboAnterior, resumen.DiaDelPeriodo);
        var tiene = comparacion.Estado != EstadoComparacion.SinReciboAnterior;

        // "unos $N": se redondean los dos montos y la diferencia es la de los redondeados, para que la frase cuadre.
        var ritmo = Math.Round(comparacion.MontoARitmo, MidpointRounding.AwayFromZero);
        var anterior = Math.Round(comparacion.MontoAnterior, MidpointRounding.AwayFromZero);
        var diferencia = Math.Abs(ritmo - anterior);
        var (cifra, texto) = comparacion.Estado switch
        {
            EstadoComparacion.MasAlto => (
                textos.Formato("Dashboard_ComparacionCifraArriba", diferencia),
                textos.Formato("Dashboard_ComparacionArriba", ritmo, anterior, diferencia)),
            EstadoComparacion.MasBajo => (
                textos.Formato("Dashboard_ComparacionCifraAbajo", diferencia),
                textos.Formato("Dashboard_ComparacionAbajo", ritmo, anterior, diferencia)),
            EstadoComparacion.Igual => (
                textos["Dashboard_ComparacionCifraIgual"],
                textos.Formato("Dashboard_ComparacionIgual", ritmo, anterior)),
            EstadoComparacion.AunEsPronto => (string.Empty, textos.Formato("Dashboard_ComparacionPronto", comparacion.Dias)),
            _ => (string.Empty, string.Empty),
        };
        return (tiene, cifra, texto);
    }
}
