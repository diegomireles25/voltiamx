using System.Globalization;
using Microsoft.UI.Xaml.Data;

namespace VoltiaMX.Helpers;

/// <summary>
/// Pasa a MAYÚSCULAS el texto dinámico que se muestra con los estilos display
/// (WinUI no tiene text-transform). El texto fijo ya viene en mayúsculas desde Resources.resw.
/// </summary>
public sealed class UppercaseConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, string? language)
    {
        var cultura = string.IsNullOrEmpty(language) ? CultureInfo.CurrentUICulture : new CultureInfo(language);
        return value?.ToString()?.ToUpper(cultura);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, string? language)
        => throw new NotSupportedException();
}
