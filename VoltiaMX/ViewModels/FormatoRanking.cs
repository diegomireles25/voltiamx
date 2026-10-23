using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Textos del ranking de aparatos que comparten el Dashboard y la pantalla de Aparatos.
/// </summary>
public static class FormatoRanking
{
    /// <summary>
    /// "46%" con consumo medido; "240.2 kWh/mes" sin él.
    /// </summary>
    public static string Valor(FilaRanking fila, ITextos textos)
    {
        ArgumentNullException.ThrowIfNull(fila);
        ArgumentNullException.ThrowIfNull(textos);
        return fila.Porcentaje is decimal porcentaje
            ? Porcentaje(porcentaje, textos)
            : textos.Formato("Aparatos_KwhMes", fila.KwhPorMes);
    }

    // Fracción a porcentaje entero: 0.458 → "46%".
    public static string Porcentaje(decimal fraccion, ITextos textos)
        => textos.Formato("Formato_Porcentaje", Math.Round(fraccion * 100m, MidpointRounding.AwayFromZero));
}
