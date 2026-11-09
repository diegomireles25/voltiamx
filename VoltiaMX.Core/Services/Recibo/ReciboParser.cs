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
    // "Fac. del Periodo" es un importe, no el periodo facturado.
    [GeneratedRegex(@"(?<!del )\bperiodo(?: facturado)?\b(?! \|)")] private static partial Regex EtiquetaPeriodo();
    [GeneratedRegex(@"\bdias(?: del periodo| facturados)?\b")] private static partial Regex EtiquetaDias();
    [GeneratedRegex(@"\blectura anterior\b")] private static partial Regex EtiquetaLecturaAnterior();
    [GeneratedRegex(@"\blectura actual\b")] private static partial Regex EtiquetaLecturaActual();
    [GeneratedRegex(@"^(?:consumo(?: total)?|total kwh|kwh consumidos)\b")] private static partial Regex EtiquetaConsumo();
    // "Energía (kWh)" es la fila de lecturas y consumo, no el importe de energía.
    [GeneratedRegex(@"^(?:importe (?:de )?|subtotal (?:de )?)?energia\b(?! ?\(kwh\))")] private static partial Regex EtiquetaEnergia();
    [GeneratedRegex(@"\btotal a pagar\b")] private static partial Regex EtiquetaTotal();
    [GeneratedRegex(@"^(basico|intermedio(?: ?(?:bajo|alto|1|2))?|excedente)(?![a-z0-9])")] private static partial Regex EtiquetaRango();
    [GeneratedRegex(@"^energia ?\(kwh\)")] private static partial Regex EtiquetaFilaEnergia();
    [GeneratedRegex(@"\bmultiplicador\b")] private static partial Regex EtiquetaMultiplicador();
    [GeneratedRegex(@"^i\.?v\.?a\.?\b")] private static partial Regex EtiquetaIva();
    [GeneratedRegex(@"^fac(?:turacion)?\.? del periodo\b")] private static partial Regex EtiquetaFacturacion();
    [GeneratedRegex(@"^adeudo anterior\b")] private static partial Regex EtiquetaAdeudo();
    [GeneratedRegex(@"^su pago\b")] private static partial Regex EtiquetaSuPago();
    [GeneratedRegex(@"^total(?! a pagar| periodo| kwh)\b")] private static partial Regex EtiquetaTotalDesglose();

    // El primer número justo después de la etiqueta (se permiten ":", "$", "=" o "-" antes). Las letras
    // o, i, l y s cuentan como posibles errores de OCR dentro del número.
    [GeneratedRegex(@"^[\s:$=\-]*(?<n>[0-9oils][0-9oils.,]*?)(?=$|\s|\$|kwh|%)")] private static partial Regex PrimerNumero();
    [GeneratedRegex(@"(?<![a-z0-9.,])(?<n>[0-9oils][0-9oils.,]*)(?=$|[^a-z0-9.,]|kwh)")] private static partial Regex Numeros();
    // Fase 9k (D2): la tarifa termina donde termina su nombre, aunque venga texto pegado ("1CNO. MEDIDOR" da "1C").
    [GeneratedRegex(@"^[\s:]*(?<t>dac|pdbt|gdbt|gdmto|1[a-f]|1)(?![0-9])")] private static partial Regex ValorTarifa();
    [GeneratedRegex(@"(?<!\d)(?<d>\d{1,2})[\s/\-.]+(?<m>[a-z]{3,4}|\d{1,2})[\s/\-.]+(?<a>\d{4}|\d{2})(?!\d)")] private static partial Regex Fecha();
    [GeneratedRegex(@"\s+")] private static partial Regex Espacios();

    /// <summary>
    /// Fase 9k: el periodo facturado es coherente si dura de 25 a 70 días; si no, sus fechas quedan para revisar.
    /// </summary>
    public const int DiasMinimosPeriodo = 25;

    public const int DiasMaximosPeriodo = 70;

    public static ReciboLeido Analizar(IReadOnlyList<string> lineas)
    {
        ArgumentNullException.ThrowIfNull(lineas);

        // El encabezado de la tabla ("Concepto | Lectura actual | Lectura anterior | …") no trae valores.
        var normalizadas = lineas.Select(Normalizar)
            .Where(l => !(l.Contains("lectura actual", StringComparison.Ordinal) && l.Contains("lectura anterior", StringComparison.Ordinal)))
            .ToList();

        var tarifa = Resolver(Candidatos(normalizadas, EtiquetaTarifa(), resto =>
            ValorTarifa().Match(resto) is { Success: true } m ? m.Groups["t"].Value.ToUpperInvariant() : null));
        var (inicio, fin) = Periodo(normalizadas);
        var dias = Numero(normalizadas, EtiquetaDias(), TipoNumero.Entero);
        var anterior = Numero(normalizadas, EtiquetaLecturaAnterior(), TipoNumero.Entero);
        var actual = Numero(normalizadas, EtiquetaLecturaActual(), TipoNumero.Entero);
        var consumo = Numero(normalizadas, EtiquetaConsumo(), TipoNumero.Entero);
        var energia = Numero(normalizadas, EtiquetaEnergia(), TipoNumero.Monto);
        var total = Numero(normalizadas, EtiquetaTotal(), TipoNumero.Monto);
        var rangos = Rangos(normalizadas);

        // La fila "Energía (kWh)": lectura actual, lectura anterior y total del periodo (el consumo).
        if (FilaEnergia(normalizadas) is { } fila)
        {
            actual = Preferir(actual, fila.Actual);
            anterior = Preferir(anterior, fila.Anterior);
            consumo = Preferir(consumo, fila.Consumo);
        }

        var multiplicador = Numero(normalizadas, EtiquetaMultiplicador(), TipoNumero.Entero);
        var iva = Iva(normalizadas);
        var facturacion = Numero(normalizadas, EtiquetaFacturacion(), TipoNumero.Monto);
        var adeudo = Numero(normalizadas, EtiquetaAdeudo(), TipoNumero.Monto);
        var suPago = Numero(normalizadas, EtiquetaSuPago(), TipoNumero.Monto);
        var totalDesglose = Numero(normalizadas, EtiquetaTotalDesglose(), TipoNumero.Monto);

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

        // D4: el importe del recibo es la facturación del periodo; sin ella, energía + IVA; sin ninguna, el total a
        // pagar (que puede incluir saldos anteriores). Lo deducido queda para revisar.
        var importe = facturacion;
        var desdeTotal = false;
        if (importe.Confianza == Confianza.NoEncontrado && energia.Valor is decimal e && iva.Valor is decimal v)
        {
            importe = new CampoNumero(e + v, Confianza.Baja);
        }
        else if (importe.Confianza == Confianza.NoEncontrado && total.Valor is decimal t0)
        {
            importe = new CampoNumero(t0, Confianza.Baja);
            desdeTotal = true;
        }

        // D1: la fecha del recibo es el FIN del periodo facturado.
        return new ReciboLeido(
            tarifa is { } t ? new CampoTexto(t.Valor, t.Confianza) : CampoTexto.NoEncontrado,
            inicio, fin, dias, anterior, actual, consumo, rangos, energia, total, fin)
        {
            Multiplicador = multiplicador,
            Iva = iva,
            FacturacionPeriodo = facturacion,
            AdeudoAnterior = adeudo,
            SuPago = suPago,
            TotalDesglose = totalDesglose,
            ImporteRecibo = importe,
            ImporteDesdeTotal = desdeTotal,
        };
    }

    // Lo leído junto a su etiqueta gana; la fila de energía llena lo que falte.
    private static CampoNumero Preferir(CampoNumero etiquetado, CampoNumero deLaFila)
        => etiquetado.Confianza == Confianza.Alta || deLaFila.Confianza == Confianza.NoEncontrado ? etiquetado : deLaFila;

    // "Energía (kWh) 11,000 10,000 1,000": los números pueden venir en las líneas siguientes (para revisar).
    private static (CampoNumero Actual, CampoNumero Anterior, CampoNumero Consumo)? FilaEnergia(List<string> lineas)
    {
        for (var i = 0; i < lineas.Count; i++)
        {
            var m = EtiquetaFilaEnergia().Match(lineas[i]);
            if (!m.Success)
            {
                continue;
            }

            var (numeros, confianza) = NumerosDeFila(lineas, i, m.Index + m.Length, 3);
            if (numeros.Count < 3)
            {
                return null;
            }

            CampoNumero Campo(string n) => Convertir(n, TipoNumero.Entero) is decimal v ? new CampoNumero(v, confianza) : CampoNumero.NoEncontrado;
            return (Campo(numeros[0]), Campo(numeros[1]), Campo(numeros[2]));
        }

        return null;
    }

    // "IVA 16% 241.60": el monto, no el porcentaje.
    private static CampoNumero Iva(List<string> lineas)
    {
        var candidatos = new List<(decimal? Valor, Confianza Confianza)>();
        foreach (var linea in lineas)
        {
            var m = EtiquetaIva().Match(linea);
            if (!m.Success)
            {
                continue;
            }

            var resto = linea[(m.Index + m.Length)..];
            var montos = Numeros().Matches(resto)
                .Where(n => n.Index + n.Length >= resto.Length || resto[n.Index + n.Length] != '%')
                .Select(n => Convertir(n.Groups["n"].Value, TipoNumero.Monto))
                .Where(v => v is not null)
                .ToList();
            if (montos.Count > 0)
            {
                candidatos.Add((montos[^1], Confianza.Alta));
            }
        }

        return Resolver(candidatos) is { } r ? new CampoNumero(r.Valor, r.Confianza) : CampoNumero.NoEncontrado;
    }

    /// <summary>
    /// Los números de una fila de la tabla: los de su línea y, si faltan, los de las líneas siguientes que solo traen
    /// números (el OCR a veces deja cada celda en su línea). Tomados de otras líneas, quedan para revisar.
    /// </summary>
    private static (List<string> Numeros, Confianza Confianza) NumerosDeFila(List<string> lineas, int indice, int desde, int cuantos)
    {
        var numeros = NumerosDe(lineas[indice][desde..]);
        var confianza = Confianza.Alta;
        for (var j = indice + 1; numeros.Count < cuantos && j < lineas.Count && SoloNumeros(lineas[j]); j++)
        {
            numeros.AddRange(NumerosDe(lineas[j]));
            confianza = Confianza.Baja;
        }

        return (numeros.Take(cuantos).ToList(), confianza);
    }

    private static List<string> NumerosDe(string texto)
        => Numeros().Matches(texto).Select(m => m.Groups["n"].Value).Where(n => n.Any(char.IsDigit)).ToList();

    private static bool SoloNumeros(string linea) => linea.Length > 0 && linea.All(c => char.IsDigit(c) || c is '.' or ',' or ' ' or '$' or '-');

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
    /// ninguno, al inicio de la siguiente (Baja). Con <paramref name="unirConSiguiente"/> (fechas), un
    /// valor incompleto se intenta completar con la línea siguiente: el OCR a veces deja ahí el año.
    /// </summary>
    private static List<(T Valor, Confianza Confianza)> Candidatos<T>(
        List<string> lineas, Regex etiqueta, Func<string, T?> leer, bool unirConSiguiente = false)
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
            else if (unirConSiguiente && i + 1 < lineas.Count && leer($"{resto} {lineas[i + 1]}") is { } unido)
            {
                candidatos.Add((unido, Confianza.Baja));
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

    // D1: dos periodos distintos no se resuelven; un periodo incoherente (el inicio después del fin, o menos de 25 o más
    // de 70 días) se lee pero queda para revisar.
    private static (CampoFecha Inicio, CampoFecha Fin) Periodo(List<string> lineas)
    {
        var resuelto = Resolver(Candidatos<(DateOnly, DateOnly)?>(lineas, EtiquetaPeriodo(), resto =>
            Fechas(resto) is [var inicio, var fin, ..] ? (inicio, fin) : null, unirConSiguiente: true));
        if (resuelto is not { Valor: var (inicio, fin) } r)
        {
            return (CampoFecha.NoEncontrado, CampoFecha.NoEncontrado);
        }

        var dias = fin.DayNumber - inicio.DayNumber;
        var confianza = dias is >= DiasMinimosPeriodo and <= DiasMaximosPeriodo ? r.Confianza : Confianza.Baja;
        return (new CampoFecha(inicio, confianza), new CampoFecha(fin, confianza));
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

    /// <summary>
    /// De 1 a 4 tramos (Fase 9k, D3): Básico, Intermedio (1, 2, bajo o alto) y Excedente.
    /// </summary>
    public const int TramosMaximos = 4;

    private static List<RangoLeido> Rangos(List<string> lineas)
    {
        var rangos = new List<RangoLeido>();
        for (var i = 0; i < lineas.Count && rangos.Count <= TramosMaximos; i++)
        {
            var etiqueta = EtiquetaRango().Match(lineas[i]);
            if (!etiqueta.Success)
            {
                continue;
            }

            var (numeros, confianza) = NumerosDeFila(lineas, i, etiqueta.Index + etiqueta.Length, 3);
            if (numeros.Count == 0)
            {
                continue;
            }

            // kWh, precio e importe, en ese orden; con solo dos números no se sabe cuál falta.
            var completo = numeros.Count == 3;
            var rango = new RangoLeido(
                NombreDeRango(etiqueta.Value),
                Campo(Convertir(numeros[0], TipoNumero.Entero), confianza),
                completo ? Campo(Convertir(numeros[1], TipoNumero.Precio), confianza) : CampoNumero.NoEncontrado,
                completo ? Campo(Convertir(numeros[2], TipoNumero.Monto), confianza) : CampoNumero.NoEncontrado);

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

        return rangos.Take(TramosMaximos).ToList();

        static CampoNumero Campo(decimal? valor, Confianza confianza) => valor is decimal v ? new CampoNumero(v, confianza) : CampoNumero.NoEncontrado;
    }

    // "intermedio1" e "intermedio 1" son el mismo tramo: "Intermedio 1".
    private static string NombreDeRango(string etiqueta) => etiqueta.Replace(" ", string.Empty, StringComparison.Ordinal) switch
    {
        "basico" => "Básico",
        "excedente" => "Excedente",
        "intermedio" => "Intermedio",
        "intermedio1" => "Intermedio 1",
        "intermedio2" => "Intermedio 2",
        "intermediobajo" => "Intermedio bajo",
        "intermedioalto" => "Intermedio alto",
        var otro => char.ToUpperInvariant(otro[0]) + otro[1..],
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
