namespace VoltiaMX.Services;

/// <summary>
/// ContentDialog con los estilos del sistema de diseño (DialogoStyle y los botones de Controls.xaml),
/// mostrado sobre el contenido del Shell.
/// </summary>
public sealed class Dialogos(NavegacionShell navegacion) : IDialogos
{
    // WinUI no permite dos ContentDialog abiertos a la vez (lanza y cierra la app): se muestran uno tras otro.
    private readonly SemaphoreSlim _uno = new(1, 1);

    public async Task<bool> ConfirmarAsync(string titulo, string mensaje, string confirmar, string cancelar)
    {
        var dialogo = Crear(titulo, mensaje, cancelar);
        dialogo.PrimaryButtonText = confirmar;
        return await MostrarAsync(dialogo) == ContentDialogResult.Primary;
    }

    public async Task AvisarAsync(string titulo, string mensaje, string cerrar)
        => await MostrarAsync(Crear(titulo, mensaje, cerrar));

    private async Task<ContentDialogResult> MostrarAsync(ContentDialog dialogo)
    {
        await _uno.WaitAsync();
        try
        {
            return await dialogo.ShowAsync();
        }
        finally
        {
            _uno.Release();
        }
    }

    private ContentDialog Crear(string titulo, string mensaje, string cerrar)
    {
        var recursos = Application.Current.Resources;
        return new ContentDialog
        {
            XamlRoot = navegacion.Marco?.XamlRoot
                ?? throw new InvalidOperationException("El Shell todavía no está listo para mostrar diálogos."),
            Style = (Style)recursos["DialogoStyle"],
            Title = new TextBlock { Text = titulo, Style = (Style)recursos["HeadingSmTextStyle"], TextWrapping = TextWrapping.Wrap },
            Content = new TextBlock { Text = mensaje, Style = (Style)recursos["BodyTextStyle"], TextWrapping = TextWrapping.Wrap },
            CloseButtonText = cerrar,
            DefaultButton = ContentDialogButton.Close,
            PrimaryButtonStyle = (Style)recursos["PrimaryButtonStyle"],
            CloseButtonStyle = (Style)recursos["GhostButtonStyle"],
        };
    }
}
