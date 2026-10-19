using Microsoft.UI.Xaml.Data;
using Windows.UI;

namespace VoltiaMX.Helpers;

/// <summary>
/// Muestra un <see cref="Color"/> como #RRGGBB. La galería de diseño lo usa para enseñar el valor
/// real de cada token en lugar de copiar el hex a mano.
/// </summary>
public sealed class ColorToHexConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string? language)
        => value is Color color ? $"#{color.R:X2}{color.G:X2}{color.B:X2}" : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        => throw new NotSupportedException();
}
