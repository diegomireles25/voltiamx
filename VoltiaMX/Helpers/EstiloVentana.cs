using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.UI;

namespace VoltiaMX.Helpers;

/// <summary>
/// Aplica el estilo de ventana de VoltiaMX: barra de título integrada sobre el lienzo,
/// fondo sólido (sin Mica ni Acrylic) y tamaño mínimo. Los colores y medidas se leen de
/// Styles/*.xaml para no repetirlos en código.
/// </summary>
internal static class EstiloVentana
{
    public static void Aplicar(Window ventana, UIElement barraDeTitulo)
    {
        ventana.SystemBackdrop = null;
        ventana.ExtendsContentIntoTitleBar = true;
        ventana.SetTitleBar(barraDeTitulo);

        var lienzo = ColorDeRecurso("WarmCanvasColor");
        var carbon = ColorDeRecurso("CarbonBlackColor");
        var barra = ventana.AppWindow.TitleBar;
        barra.BackgroundColor = lienzo;
        barra.InactiveBackgroundColor = lienzo;
        barra.ButtonBackgroundColor = Colors.Transparent;
        barra.ButtonInactiveBackgroundColor = Colors.Transparent;
        barra.ButtonForegroundColor = carbon;
        barra.ButtonInactiveForegroundColor = ColorDeRecurso("SlateColor");
        barra.ButtonHoverBackgroundColor = ColorDeRecurso("AshColor");
        barra.ButtonHoverForegroundColor = carbon;
        barra.ButtonPressedBackgroundColor = ColorDeRecurso("SmokeColor");
        barra.ButtonPressedForegroundColor = carbon;

        if (ventana.Content is FrameworkElement raiz && ventana.AppWindow.Presenter is OverlappedPresenter presentador)
        {
            // El tamaño mínimo del presentador va en píxeles físicos: se escala con el DPI del
            // monitor y se recalcula si la ventana pasa a otro con distinta escala.
            void ActualizarTamanoMinimo()
            {
                if (raiz.XamlRoot is null)
                {
                    return;
                }

                var escala = raiz.XamlRoot.RasterizationScale;
                presentador.PreferredMinimumWidth = (int)Math.Ceiling(MedidaDeRecurso("WindowMinWidth") * escala);
                presentador.PreferredMinimumHeight = (int)Math.Ceiling(MedidaDeRecurso("WindowMinHeight") * escala);
            }

            raiz.Loaded += (_, _) =>
            {
                ActualizarTamanoMinimo();
                raiz.XamlRoot.Changed += (_, _) => ActualizarTamanoMinimo();
            };
        }
    }

    private static Color ColorDeRecurso(string clave) => (Color)Application.Current.Resources[clave];

    private static double MedidaDeRecurso(string clave) => (double)Application.Current.Resources[clave];
}
