namespace VoltiaMX.Tests;

/// <summary>
/// Busca en los textos de la app las palabras que la Fase 7b sustituyó por lenguaje sencillo.
/// </summary>
internal static class TerminosProhibidos
{
    /// <summary>
    /// Se buscan sin distinguir mayúsculas ("Proyección" y "PROYECCIÓN" cuentan igual).
    /// </summary>
    public static readonly string[] Palabras =
    [
        "presupuesto",
        "periodo cerrado",
        "periodos cerrados",
        "lectura acumulada",
        "proyección",
        "proyectado",
        "submedidor",
        "ciclo de trabajo",
        "riesgo DAC",
        "sin IVA ni DAP",
        "ranking",
    ];

    /// <summary>
    /// La abreviatura de "estimado" solo se busca tal cual, en mayúsculas y con punto ("EST.").
    /// </summary>
    public const string AbreviaturaEstimado = "EST.";

    public static IReadOnlyList<(string Clave, string Termino)> Buscar(IReadOnlyDictionary<string, string> textos)
    {
        var hallazgos = new List<(string, string)>();
        foreach (var (clave, valor) in textos)
        {
            foreach (var palabra in Palabras)
            {
                if (valor.Contains(palabra, StringComparison.CurrentCultureIgnoreCase))
                {
                    hallazgos.Add((clave, palabra));
                }
            }

            if (valor.Contains(AbreviaturaEstimado, StringComparison.Ordinal))
            {
                hallazgos.Add((clave, AbreviaturaEstimado));
            }
        }

        return hallazgos;
    }
}

public sealed class LenguajeSencilloTests
{
    /// <summary>
    /// Textos que pueden conservar un término, cada uno con su motivo. Hoy no hace falta ninguno: si algún día se
    /// agrega uno, debe llevar el motivo y la prueba avisa cuando deje de hacer falta.
    /// </summary>
    private static readonly Dictionary<string, string> Excepciones = new()
    {
    };

    [Fact]
    public void LosTextosDeLaApp_NoUsanTerminosProhibidos()
    {
        var hallazgos = TerminosProhibidos.Buscar(TextosDeRecursos.Todos())
            .Where(h => !Excepciones.ContainsKey(h.Clave))
            .Select(h => $"{h.Clave}: \"{h.Termino}\"")
            .ToList();

        Assert.True(hallazgos.Count == 0, "Textos con términos prohibidos:\n" + string.Join("\n", hallazgos));
    }

    [Fact]
    public void CadaExcepcion_TieneMotivoYTodaviaHaceFalta()
    {
        var conTermino = TerminosProhibidos.Buscar(TextosDeRecursos.Todos()).Select(h => h.Clave).ToHashSet();
        foreach (var (clave, motivo) in Excepciones)
        {
            Assert.False(string.IsNullOrWhiteSpace(motivo), $"La excepción {clave} no dice por qué.");
            Assert.True(conTermino.Contains(clave), $"La excepción {clave} ya no hace falta: quítala.");
        }
    }

    [Fact]
    public void LaGuardiana_DetectaUnTextoMalo()
    {
        // Un resw de prueba, en memoria, con un texto bueno y varios malos.
        var textos = new Dictionary<string, string>
        {
            ["Bueno"] = "A este ritmo vas 46 % más alto que tu recibo anterior.",
            ["Malo1"] = "Tu PROYECCIÓN al cierre del periodo cerrado.",
            ["Malo2"] = "Monto sin IVA ni DAP.",
            ["Malo3"] = "820 kWh EST.",
            ["Estado"] = "Estado de la meta",
        };

        var hallazgos = TerminosProhibidos.Buscar(textos);

        Assert.Equal(
            [("Malo1", "periodo cerrado"), ("Malo1", "proyección"), ("Malo2", "sin IVA ni DAP"), ("Malo3", "EST.")],
            hallazgos.OrderBy(h => h.Clave).ThenBy(h => h.Termino, StringComparer.Ordinal));
        Assert.DoesNotContain(hallazgos, h => h.Clave is "Bueno" or "Estado");
    }
    [Fact]
    public void CadaAyuda_TieneSuTerminoYDeUnaATresFrasesCortas()
    {
        // "¿Qué es esto?": Ayuda_{clave} es la explicación y Ayuda_Termino_{clave} su nombre (D5).
        var todos = TextosDeRecursos.Todos();
        var explicaciones = todos.Keys
            .Where(k => k.StartsWith("Ayuda_", StringComparison.Ordinal) && !k.Contains('.'))
            .Where(k => !k.StartsWith("Ayuda_Termino_", StringComparison.Ordinal)
                && !k.StartsWith("Ayuda_Calculo_", StringComparison.Ordinal)
                && !k.StartsWith("Ayuda_NoHace_", StringComparison.Ordinal)
                && k != "Ayuda_QueEsEsto")
            .ToList();

        Assert.NotEmpty(explicaciones);
        foreach (var clave in explicaciones)
        {
            var termino = "Ayuda_Termino_" + clave["Ayuda_".Length..];
            Assert.True(todos.ContainsKey(termino), $"Falta {termino}");

            var frases = todos[clave].Split(['.', '?', '!'], StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim()).Where(f => f.Length > 0).ToList();
            // Se cuentan trozos entre puntos: "APROX." o "0.5" cuentan como un corte más, y aun así caben en 3.
            Assert.InRange(frases.Count, 1, 3);
            Assert.All(frases, f => Assert.True(
                f.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 20, $"{clave}: frase de más de 20 palabras"));
        }
    }
}
