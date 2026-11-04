using VoltiaMX.Core.Services;

namespace VoltiaMX.Services;

/// <summary>
/// Las fuentes de la app para el PDF (Barlow Condensed en títulos, Inter en el cuerpo). Si no se pueden
/// leer, el PDF usa su fuente de respaldo.
/// </summary>
public static class FuentesDelReporte
{
    public static OpcionesPdf Cargar()
    {
        var carpeta = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");
        return new OpcionesPdf(Leer(Path.Combine(carpeta, "BarlowCondensed-Bold.ttf")), Leer(Path.Combine(carpeta, "Inter-Regular.ttf")));

        static byte[]? Leer(string ruta)
        {
            try
            {
                return File.ReadAllBytes(ruta);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
