namespace VoltiaMX.Services;

/// <summary>
/// Textos de Resources.resw para los ViewModels, sin que dependan de tipos de WinUI.
/// </summary>
public interface ITextos
{
    string this[string clave] { get; }

    /// <summary>
    /// Aplica <c>string.Format</c> con la cultura es-MX al patrón guardado en <paramref name="clave"/>.
    /// </summary>
    string Formato(string clave, params object?[] valores);
}
