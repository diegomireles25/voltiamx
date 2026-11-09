namespace VoltiaMX.Core.Services;

/// <summary>
/// Una palabra del recibo con su posición: <see cref="X"/> es su borde izquierdo y <see cref="Y"/> su borde de arriba,
/// medidos desde la esquina superior izquierda de la página; <see cref="Alto"/> es la altura de la letra.
/// </summary>
public sealed record PalabraUbicada(string Texto, double X, double Y, double Alto);

/// <summary>
/// Arma líneas de texto con las palabras sueltas de un PDF o del OCR (Fase 9k). Las palabras a la misma altura forman
/// una línea, de izquierda a derecha: así las columnas de una tabla quedan en su fila ("Básico 300 1.000 300.00").
/// </summary>
public static class LineasPorPosicion
{
    public static IReadOnlyList<string> Agrupar(IEnumerable<PalabraUbicada> palabras)
    {
        ArgumentNullException.ThrowIfNull(palabras);
        var lista = palabras.Where(p => !string.IsNullOrWhiteSpace(p.Texto)).ToList();
        if (lista.Count == 0)
        {
            return [];
        }

        // Dos palabras van en la misma línea si sus centros están a menos de media altura de letra.
        var alturas = lista.Select(p => p.Alto).Where(a => a > 0).Order().ToList();
        var tolerancia = (alturas.Count > 0 ? alturas[alturas.Count / 2] : 1d) / 2d;

        var lineas = new List<(double Centro, List<PalabraUbicada> Palabras)>();
        foreach (var palabra in lista.OrderBy(p => p.Y + p.Alto / 2d))
        {
            var centro = palabra.Y + palabra.Alto / 2d;
            if (lineas.Count > 0 && Math.Abs(lineas[^1].Centro - centro) <= tolerancia)
            {
                lineas[^1].Palabras.Add(palabra);
            }
            else
            {
                lineas.Add((centro, [palabra]));
            }
        }

        return lineas.Select(l => string.Join(' ', l.Palabras.OrderBy(p => p.X).Select(p => p.Texto.Trim()))).ToList();
    }
}
