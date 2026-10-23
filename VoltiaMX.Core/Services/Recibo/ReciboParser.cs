using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Analiza las líneas que devuelve el OCR de un recibo de CFE. Función pura: no sabe de imágenes.
/// Solo toma valores que aparecen junto a su etiqueta; si un dato es ambiguo o aparece con dos
/// valores distintos, lo deja NoEncontrado en lugar de adivinar.
/// </summary>
public static partial class ReciboParser
{
    private enum TipoNumero
    {
        /// <summary>
        /// Lecturas, kWh y días: "48,623" o "48.623" es un millar.
        /// </summary>
        Entero,

        /// <summary>
        /// Pesos: "1,234.56" o "1.234,56"; "1.234" es ambiguo.
        /// </summary>
        Monto,

        /// <summary>
        /// Precio por kWh: el punto (o la coma, si es la única) es el decimal.
        /// </summary>
        Precio,
    }

    private static readonly Dictionary<string, int> Meses = new()
    {
        ["ene"] = 1, ["feb"] = 2, ["mar"] = 3, ["abr"] = 4, ["may"] = 5, ["jun"] = 6,
        ["jul"] = 7, ["ago"] = 8, ["sep"] = 9, ["sept"] = 9, ["set"] = 9, ["oct"] = 10, ["nov"] = 11, ["dic"] = 12,
    };

    // Etiquetas sobre la línea normalizada (minúsculas, sin acentos, espacios simples).
    [GeneratedRegex(@"\btarifa\b")] private static partial Regex EtiquetaTarifa();
    [GeneratedRegex(@"\bperiodo(?: facturado)?\b")] private static partial Regex EtiquetaPeriodo();
    [GeneratedRegex(@"\bdias(?: del periodo| facturados)?\b")] private static partial Regex EtiquetaDias();
    [GeneratedRegex(@"\blectura anterior\b")] private static partial Regex EtiquetaLecturaAnterior();
    [GeneratedRegex(@"\blectura actual\b")] private static partial Regex EtiquetaLecturaActual();
    [GeneratedRegex(@"^(?:consumo(?: total)?|total kwh|kwh consumidos)\b")] private static partial Regex EtiquetaConsumo();
    [GeneratedRegex(@"^(?:importe (?:de )?|subtotal (?:de )?)?energia\b")] private static partial Regex EtiquetaEnergia();
    [GeneratedRegex(@"\btotal a pagar\b")] private static partial Regex EtiquetaTotal();
    [GeneratedRegex(@"\b(?:fecha de corte|corte a partir(?: de)?)\b")] private static partial Regex EtiquetaCorte();
    [GeneratedRegex(@"^(basico|intermedio(?: (?:bajo|alto|1|2))?|excedente)\b")] private static partial Regex EtiquetaRango();

    // El primer número justo después de la etiqueta (se permiten ":", "$", "=" o "-" antes). Las letras
    // o, i, l y s cuentan como posibles errores de OCR dentro del número.
    [GeneratedRegex(@"^[\s:$=\-]*(?<n>[0-9oils][0-9oils.,]*?)(?=$|\s|\$|kwh|%)")] private static partial Regex PrimerNumero();
    [GeneratedRegex(@"(?<![a-z0-9.,])(?<n>[0-9oils][0-9oils.,]*)(?=$|[^a-z0-9.,]|kwh)")] private static partial Regex Numeros();
    [GeneratedRegex(@"^[\s:]*(?<t>1[a-f]?|dac|pdbt|gdbt|gdmto)\b")] private static partial Regex ValorTarifa();
    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})[\s/\-.]+(?<m>[a-z]{3,4}|\d{1,2})[\s/\-.]+(?<a>\d{4}|\d{2})(?!\d)")] private static partial Regex Fecha();
    [GeneratedRegex(@"\s+")] private static partial Regex Espacios();

    public static ReciboLeido Analizar(IReadOnlyList<string> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        var normalizadas = lineas.Select(Normalizar).ToList();

        var tarifa = Resolver(Candidatos(normalizadas, EtiquetaTarifa(), resto =>
            ValorTarifa().Match(resto) is { Success: true } m ? m.Groups["t"].Value.ToUpperInvariant() : null));
        var (inicio, fin) = Periodo(normalizadas);
        var dias = Numero(normalizadas, EtiquetaDias(), TipoNumero.Entero);
        var anterior = Numero(normalizadas, EtiquetaLecturaAnterior(), TipoNumero.Entero);
        var actual = Numero(normalizadas, EtiquetaLecturaActual(), TipoNumero.Entero);
        var consumo = Numero(normalizadas, EtiquetaConsumo(), TipoNumero.Entero);
        var energia = Numero(normalizadas, EtiquetaEnergia(), TipoNumero.Monto);
        var total = Numero(normalizadas, EtiquetaTotal(), TipoNumero.Monto);
        var corte = FechaUnica(normalizadas, EtiquetaCorte());
        var rangos = Rangos(normalizadas);

        // Deducciones: siempre en Baja, para que se revisen.
        if (dias.Confianza == Confianza.NoEncontrado && inicio.Valor is DateOnly i && fin.Valor is DateOnly f)
        {
            dias = new CampoNumero(f.DayNumber - i.DayNumber, Confianza.Baja);
        }

        if (consumo.Confianza == Confianza.NoEncontrado && actual.Valor is decimal a && anterior.Valor is decimal b && a >= b)
        {
            consumo = new CampoNumero(a - b, Confianza.Baja);
        }

        if (energia.Confianza == Confianza.NoEncontrado && rangos.Count > 0 && rangos.All(r => r.Importe.Valor is not null))
        {
            energia = new CampoNumero(rangos.Sum(r => r.Importe.Valor!.Value), Confianza.Baja);
        }

        return new ReciboLeido(
            tarifa is { } t ? new CampoTexto(t.Valor, t.Confianza) : CampoTexto.NoEncontrado,
            inicio, fin, dias, anterior, actual, consumo, rangos, energia, total, corte);
    }

    private static string Normalizar(string linea)
    {
        var descompuesta = (linea ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sinAcentos = new StringBuilder(descompuesta.Length);
        foreach (var c in descompuesta)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sinAcentos.Append(c);
            }
        }

        return Espacios().Replace(sinAcentos.ToString().ToLowerInvariant(), " ").Trim();
    }

    /// <summary>
    /// Valores junto a cada aparición de la etiqueta: en la misma línea (Alta) o, si ahí no hay
    /// ninguno, al inicio de la siguiente (Baja).
    /// </summary>
    private static List<(T Valor, Confianza Confianza)> Candidatos<T>(List<string> lineas, Regex etiqueta, Func<string, T?> leer)
    {
        var candidatos = new List<(T, Confianza)>();
        for (var i = 0; i < lineas.Count; i++)
        {
            var m = etiqueta.Match(lineas[i]);
            if (!m.Success)
            {
                continue;
            }

            var resto = lineas[i][(m.Index + m.Length)..];
            if (leer(resto) is { } valor)
            {
                candidatos.Add((valor, Confianza.Alta));
            }
            else if (!resto.Any(char.IsDigit) && i + 1 < lineas.Count && leer(lineas[i + 1]) is { } siguiente)
            {
                candidatos.Add((siguiente, Confianza.Baja));
            }
        }

        return candidatos;
    }

    /// <summary>
    /// Un solo valor (repetido o no); dos valores distintos para el mismo campo no se resuelven.
    /// </summary>
    private static (T Valor, Confianza Confianza)? Resolver<T>(List<(T Valor, Confianza Confianza)> candidatos)
    {
        if (candidatos.Count == 0 || candidatos.Select(c => c.Valor).Distinct().Count() > 1)
        {
            return null;
        }

        return (candidatos[0].Valor, candidatos.Any(c => c.Confianza == Confianza.Alta) ? Confianza.Alta : Confianza.Baja);
    }

    private static CampoNumero Numero(List<string> lineas, Regex etiqueta, TipoNumero tipo)
    {
        var resuelto = Resolver(Candidatos<decimal?>(lineas, etiqueta, resto =>
            PrimerNumero().Match(resto) is { Success: true } m ? Convertir(m.Groups["n"].Value, tipo) : null));
        return resuelto is { } r ? new CampoNumero(r.Valor, r.Confianza) : CampoNumero.NoEncontrado;
    }

    private static CampoFecha FechaUnica(List<string> lineas, Regex etiqueta)
    {
        var resuelto = Resolver(Candidatos<DateOnly?>(lineas, etiqueta, resto => Fechas(resto) is [var unica, ..] ? unica : null));
        return resuelto is { } r ? new CampoFecha(r.Valor, r.Confianza) : CampoFecha.NoEncontrado;
    }

    private static (CampoFecha Inicio, CampoFecha Fin) Periodo(List<string> lineas)
    {
        var resuelto = Resolver(Candidatos<(DateOnly, DateOnly)?>(lineas, EtiquetaPeriodo(), resto =>
            Fechas(resto) is [var inicio, var fin, ..] && inicio < fin ? (inicio, fin) : null));
        return resuelto is { Valor: var (inicio, fin) } r
            ? (new CampoFecha(inicio, r.Confianza), new CampoFecha(fin, r.Confianza))
            : (CampoFecha.NoEncontrado, CampoFecha.NoEncontrado);
    }

    private static List<DateOnly> Fechas(string texto)
    {
        var fechas = new List<DateOnly>();
        foreach (Match m in Fecha().Matches(texto))
        {
            var dia = int.Parse(m.Groups["d"].Value, CultureInfo.InvariantCulture);
            var mesTexto = m.Groups["m"].Value;
            int mes;
            if (char.IsDigit(mesTexto[0]))
            {
                mes = int.Parse(mesTexto, CultureInfo.InvariantCulture);
            }
            else if (!Meses.TryGetValue(mesTexto, out mes))
            {
                continue;
            }

            var anio = int.Parse(m.Groups["a"].Value, CultureInfo.InvariantCulture);
            anio = anio < 100 ? 2000 + anio : anio;
            if (mes is >= 1 and <= 12 && dia >= 1 && dia <= DateTime.DaysInMonth(anio, mes))
            {
                fechas.Add(new DateOnly(anio, mes, dia));
            }
        }

        return fechas;
    }

    private static List<RangoLeido> Rangos(List<string> lineas)
    {
        var rangos = new List<RangoLeido>();
        foreach (var linea in lineas)
        {
            var etiqueta = EtiquetaRango().Match(linea);
            if (!etiqueta.Success)
            {
                continue;
            }

            var numeros = Numeros().Matches(linea[(etiqueta.Index + etiqueta.Length)..])
                .Select(m => m.Groups["n"].Value)
                .Where(n => n.Any(char.IsDigit))
                .Take(3)
                .ToList();
            if (numeros.Count == 0)
            {
                continue;
            }

            // kWh, precio e importe, en ese orden; con solo dos números no se sabe cuál falta.
            var completo = numeros.Count == 3;
            var rango = new RangoLeido(
                NombreDeRango(etiqueta.Value),
                Campo(Convertir(numeros[0], TipoNumero.Entero)),
                completo ? Campo(Convertir(numeros[1], TipoNumero.Precio)) : CampoNumero.NoEncontrado,
                completo ? Campo(Convertir(numeros[2], TipoNumero.Monto)) : CampoNumero.NoEncontrado);

            var previo = rangos.FindIndex(r => r.Nombre == rango.Nombre);
            if (previo < 0)
            {
                rangos.Add(rango);
            }
            else if (rangos[previo] != rango)
            {
                // El mismo renglón con valores distintos: no se adivina cuál es.
                rangos[previo] = new RangoLeido(rango.Nombre, CampoNumero.NoEncontrado, CampoNumero.NoEncontrado, CampoNumero.NoEncontrado);
            }
        }

        return rangos;

        static CampoNumero Campo(decimal? valor) => valor is decimal v ? new CampoNumero(v, Confianza.Alta) : CampoNumero.NoEncontrado;
    }

    private static string NombreDeRango(string etiqueta) => etiqueta switch
    {
        "basico" => "Básico",
        "excedente" => "Excedente",
        _ => char.ToUpperInvariant(etiqueta[0]) + etiqueta[1..],
    };

    /// <summary>
    /// Convierte el texto de un número ya etiquetado; null si no es un número o si es ambiguo.
    /// </summary>
    private static decimal? Convertir(string token, TipoNumero tipo)
    {
        if (!token.Any(char.IsDigit))
        {
            return null;
        }

        // Errores típicos de OCR, solo dentro del número: o → 0; i, l → 1; s → 5.
        var texto = new string(token.TrimEnd('.', ',').Select(c => c switch
        {
            'o' => '0',
            'i' or 'l' => '1',
            's' => '5',
            _ => c,
        }).ToArray());
        if (texto.Length == 0 || !char.IsDigit(texto[0]))
        {
            return null;
        }

        var comas = texto.Count(c => c == ',');
        var puntos = texto.Count(c => c == '.');
        string entero;
        var fraccion = string.Empty;

        if (comas > 0 && puntos > 0)
        {
            // El último separador es el decimal; el otro, el de miles.
            var decimalSep = texto.LastIndexOf(',') > texto.LastIndexOf('.') ? ',' : '.';
            var milesSep = decimalSep == ',' ? '.' : ',';
            if (texto.Count(c => c == decimalSep) > 1)
            {
                return null;
            }

            var corte = texto.LastIndexOf(decimalSep);
            fraccion = texto[(corte + 1)..];
            entero = SinMiles(texto[..corte], milesSep);
        }
        else if (comas + puntos == 0)
        {
            entero = texto;
        }
        else
        {
            var sep = comas > 0 ? ',' : '.';
            var partes = texto.Split(sep);
            if (partes.Length > 2)
            {
                entero = SinMiles(texto, sep);
            }
            else
            {
                var despues = partes[1];
                var esMiles = tipo switch
                {
                    TipoNumero.Precio => false,
                    TipoNumero.Monto when despues.Length == 3 && sep == '.' => (bool?)null, // "1.234": ambiguo
                    _ => despues.Length == 3 && partes[0].Length <= 3,
                };
                if (esMiles is null)
                {
                    return null;
                }

                if (esMiles == true)
                {
                    entero = partes[0] + despues;
                }
                else
                {
                    entero = partes[0];
                    fraccion = despues;
                }
            }
        }

        if (entero.Length == 0 || !entero.All(char.IsDigit) || !fraccion.All(char.IsDigit))
        {
            return null;
        }

        return decimal.Parse(fraccion.Length > 0 ? $"{entero}.{fraccion}" : entero, CultureInfo.InvariantCulture);
    }

    // Quita separadores de miles solo si los grupos son de 3 cifras ("2,345,678"); si no, es ambiguo.
    private static string SinMiles(string texto, char sep)
    {
        var grupos = texto.Split(sep);
        return grupos[0].Length is >= 1 and <= 3 && grupos.Skip(1).All(g => g.Length == 3) ? string.Concat(grupos) : string.Empty;
    }
}
