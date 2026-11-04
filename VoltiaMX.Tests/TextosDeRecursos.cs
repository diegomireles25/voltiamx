using System.Xml.Linq;

namespace VoltiaMX.Tests;

/// <summary>
/// Los textos reales de la app (Resources.resw del repositorio), para generar los documentos en las
/// pruebas igual que la app. Una clave que no existe falla la prueba.
/// </summary>
internal static class TextosDeRecursos
{
    private static readonly Lazy<Dictionary<string, string>> Textos = new(Cargar);

    public static string Texto(string clave)
        => Textos.Value.TryGetValue(clave, out var valor) ? valor : throw new KeyNotFoundException($"Falta la clave '{clave}' en Resources.resw");

    private static Dictionary<string, string> Cargar()
    {
        var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
        while (carpeta is not null && !File.Exists(Path.Combine(carpeta.FullName, "VoltiaMX.sln")) && !Directory.Exists(Path.Combine(carpeta.FullName, "VoltiaMX.Core")))
        {
            carpeta = carpeta.Parent;
        }

        var ruta = Path.Combine(carpeta?.FullName ?? throw new DirectoryNotFoundException("No encontré la raíz del repositorio"), "VoltiaMX", "Strings", "es-MX", "Resources.resw");
        return XDocument.Load(ruta).Root!
            .Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? string.Empty);
    }
}
