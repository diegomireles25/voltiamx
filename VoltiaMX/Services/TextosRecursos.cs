using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;

namespace VoltiaMX.Services;

public sealed class TextosRecursos : ITextos
{
    private static readonly CultureInfo Cultura = new("es-MX");

    private readonly ResourceLoader _recursos = new();

    public string this[string clave] => _recursos.GetString(clave);

    public string Formato(string clave, params object?[] valores)
        => string.Format(Cultura, _recursos.GetString(clave), valores);
}
