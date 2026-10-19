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
}
