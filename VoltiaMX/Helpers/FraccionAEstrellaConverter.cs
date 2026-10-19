using Microsoft.UI.Xaml.Data;

namespace VoltiaMX.Helpers;

/// <summary>
/// Convierte una fracción (0 a 1) en un alto proporcional de Grid (<c>n*</c>). Las barras del
/// historial usan dos filas: lo que falta arriba y la barra abajo.
/// </summary>
public sealed class FraccionAEstrellaConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => new GridLength(value is double fraccion ? Math.Max(0, fraccion) : 0, GridUnitType.Star);

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}
