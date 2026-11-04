using System.Text;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Nombres de archivo válidos en Windows a partir de textos de la persona (por ejemplo, el nombre de un inquilino).
/// </summary>
public static class NombreArchivo
{
    public const int LargoMaximo = 60;
    private const string Respaldo = "inquilino";

    private static readonly HashSet<char> Invalidos = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> Reservados = new(
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).SelectMany(n => new[] { $"COM{n}", $"LPT{n}" })],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Cambia los caracteres inválidos por "-", quita puntos y espacios al final, evita los nombres reservados
    /// (CON, PRN, AUX, NUL, COM1-9, LPT1-9) y corta a <paramref name="maximo"/> caracteres.
    /// </summary>
    public static string Limpiar(string? texto, int maximo = LargoMaximo)
    {
        var limpio = new StringBuilder();
        foreach (var c in (texto ?? string.Empty).Trim())
        {
            limpio.Append(c < 32 || Invalidos.Contains(c) ? '-' : c);
        }

        var nombre = limpio.ToString().Trim().TrimEnd('.', ' ');
        if (nombre.Length > maximo)
        {
            nombre = nombre[..maximo].TrimEnd('.', ' ');
        }

        if (nombre.Length == 0)
        {
            return Respaldo;
        }

        // "CON" o "con.txt" están reservados: se agrega "_" a la parte antes del primer punto.
        var punto = nombre.IndexOf('.', StringComparison.Ordinal);
        var raiz = punto < 0 ? nombre : nombre[..punto];
        if (Reservados.Contains(raiz.TrimEnd()))
        {
            nombre = nombre.Insert(raiz.Length, "_");
        }

        return nombre;
    }

    public static string Recibo(string inquilino, DateOnly finDelPeriodo) => $"recibo-{Limpiar(inquilino)}-{finDelPeriodo:yyyy-MM-dd}.pdf";

    public static string Resumen(DateOnly finDelPeriodo) => $"reparto-{finDelPeriodo:yyyy-MM-dd}.pdf";

    /// <summary>
    /// Ruta en <paramref name="carpeta"/> que no pisa un archivo existente: agrega "-2", "-3"… antes de la extensión.
    /// </summary>
    public static string Disponible(string carpeta, string nombre)
    {
        var ruta = Path.Combine(carpeta, nombre);
        var raiz = Path.GetFileNameWithoutExtension(nombre);
        var extension = Path.GetExtension(nombre);
        for (var n = 2; File.Exists(ruta); n++)
        {
            ruta = Path.Combine(carpeta, $"{raiz}-{n}{extension}");
        }

        return ruta;
    }
}
