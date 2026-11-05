using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Porcentaje del ranking de aparatos (la fila "sin identificar" de Aparatos).
/// </summary>
public static class FormatoRanking
{
    // Fracción a porcentaje entero: 0.458 → "46%".
    public static string Porcentaje(decimal fraccion, ITextos textos)
        => textos.Formato("Formato_Porcentaje", Math.Round(fraccion * 100m, MidpointRounding.AwayFromZero));
}
