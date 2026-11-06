using System.Text.RegularExpressions;
using System.Xml.Linq;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Guardiana de claves (Fase 9c, D6). En Windows App SDK, <c>ResourceLoader.GetString</c> lanza una excepción si la
/// clave no existe, y la app se cierra (pasó dos veces en la Fase 9b). Esta prueba revisa que cada <c>x:Uid</c> de
/// las vistas y cada clave escrita en el código (<c>textos["…"]</c>, <c>textos.Formato("…")</c>, <c>GetString("…")</c>)
/// exista en Resources.resw. Las claves armadas en tiempo de ejecución (<c>$"Prefijo_{valor}"</c>) se validan con
/// la lista de valores posibles; solo se excluyen las familias que no tienen esa lista, con su motivo.
/// </summary>
public sealed partial class ClavesDeRecursosTests
{
    // ===================================================================================================
    // Familias de claves armadas en tiempo de ejecución. El "*" es la parte que cambia.
    // ===================================================================================================

    /// <summary>
    /// Familias que se validan con la lista de sus valores posibles: cada valor debe tener su clave.
    /// </summary>
    private static readonly Dictionary<string, Func<IEnumerable<string>>> FamiliasValidadas = new()
    {
        // --- Armadas con el valor de un enum de Core: se revisan todos sus valores ---
        ["Aparato_Error_*"] = Nombres<CodigoErrorAparato>,
        ["AvisoRecibo_*"] = Nombres<CodigoAvisoRecibo>,
        ["CategoriaAparato_*"] = Nombres<CategoriaAparato>,
        ["Confianza_*"] = Nombres<Confianza>,
        ["Dac_*"] = Nombres<NivelRiesgoDac>,
        ["Escanear_Error_*"] = Nombres<ErrorConfirmacionRecibo>,
        ["EstadoMeta_*"] = Nombres<EstadoMeta>,
        ["Inquilino_Error_*"] = Nombres<CodigoErrorInquilino>,
        ["Lectura_Rechazo_*"] = Nombres<MotivoRechazoLectura>,
        ["Meta_Error_*"] = Nombres<CodigoErrorMeta>,
        ["ReciboMonto_Error_*"] = Nombres<ErrorReciboPorMonto>,
        ["Reparto_Aviso_*"] = Nombres<AvisoReparto>,
        ["Reparto_Origen_*"] = Nombres<OrigenTotal>,
        ["Respaldo_Error_*"] = Nombres<ErrorRespaldo>,
        ["Respaldo_Rechazo_*"] = Nombres<MotivoRechazo>,
        ["Simulador_Error_*"] = Nombres<CodigoErrorSimulacion>,
        ["Solar_Error_*"] = Nombres<CodigoErrorSolar>,
        ["Tarifa_Error_*"] = Nombres<CodigoErrorTarifa>,
        ["Tarifas_NoEliminar_*"] = Nombres<MotivoNoEliminarTarifa>,
        ["TipoLugar_*"] = Nombres<TipoLugar>,
        ["TipoTarifa_*"] = Nombres<TipoTarifa>,
        ["Validar_Veredicto_*"] = Nombres<VeredictoRecibo>,

        // --- Armadas con un enum de Core del que solo se muestran algunos valores (el resto tiene su propio caso) ---
        // El simulador solo muestra un aviso cuando el estado no es Normal (SimuladorViewModel).
        ["EstadoSimulacion_*"] = () => Nombres<EstadoSimulacion>().Where(n => n != nameof(EstadoSimulacion.Normal)),
        // Un objetivo viejo de presupuesto se trata como "sin objetivo" desde la Fase 7a (DashboardViewModel.CargarAsync).
        ["MetaObjetivo_*"] = () => Nombres<TipoObjetivo>().Where(n => n != nameof(TipoObjetivo.NoPasarmeDePresupuesto)),
        // Presupuesto ya no tiene pantalla desde la Fase 7a: ModulosPorUso no lo activa y ModulosPrincipales lo quita.
        ["Modulo_*"] = () => Nombres<ModuloApp>().Where(n => n != nameof(ModuloApp.Presupuesto)),

        // Solo se notifica el riesgo DAC Medio o Alto (EvaluadorAvisos): Bajo nunca llega al Notificador.
        ["Aviso_Dac_Cuerpo_*"] = () => [nameof(NivelRiesgoDac.Medio), nameof(NivelRiesgoDac.Alto)],
        // Solo se notifica la meta en riesgo o superada (EvaluadorAvisos).
        ["Aviso_Meta_Cuerpo_*"] = () => [nameof(EstadoMeta.EnRiesgo), nameof(EstadoMeta.Superada)],
        // Caso por defecto del switch de DashboardViewModel.CargarMetaAsync: Cumpliendo, EnRiesgo y Superada tienen su
        // propio texto e Inactiva sale antes.
        ["Dashboard_Meta*"] = () => SinEstadosDeTarjeta().Where(n => n != nameof(EstadoMeta.Inactiva)),
        // MetasViewModel solo arma el título y el texto "sin datos" fuera de la tarjeta; Inactiva no llega a Mostrar.
        ["Metas_*Titulo"] = () => SinEstadosDeTarjeta().Where(n => n != nameof(EstadoMeta.Inactiva)),
        ["Metas_*Texto"] = () => SinEstadosDeTarjeta().Where(n => n != nameof(EstadoMeta.Inactiva)),
        ["Solar_SinPesos_*"] = Nombres<EstadoPesosSolar>,
        // Las filas de HERRAMIENTAS AVANZADAS de Configuración: cualquier módulo menos Presupuesto (sin pantalla).
        ["Mas_Modulo_*_Titulo"] = () => Nombres<ModuloApp>().Where(n => n != nameof(ModuloApp.Presupuesto)),
        ["Mas_Modulo_*_Texto"] = () => Nombres<ModuloApp>().Where(n => n != nameof(ModuloApp.Presupuesto)),

        // --- Armadas con un enum de la app (VoltiaMX no se referencia desde las pruebas): valores leídos del código ---
        // SinIdioma tiene su propio texto (Escanear_SinIdiomaTexto) en el escáner y en el onboarding.
        ["ErrorOcr_*"] = () => ValoresDeEnum("Services/ReciboOcr.cs", "ErrorOcr").Where(n => n != "SinIdioma"),
        ["Reportes_Error_*"] = () => ValoresDeEnum("Services/GuardadorArchivos.cs", "ErrorGuardado"),

        // --- Armadas con datos de Core ---
        // Cada plantilla de aparato de referencia tiene su nombre.
        ["Plantilla_*"] = () => new AparatoService(null!, null!).PlantillasDeReferencia().Select(p => p.Clave),

        // --- Armadas con listas o conteos de la app: valores leídos del código ---
        // Ayuda: los términos de AyudaViewModel.Terminos y los de cada TerminoAyuda de las vistas (Clave="…").
        ["Ayuda_*"] = TerminosDeAyuda,
        ["Ayuda_Termino_*"] = TerminosDeAyuda,
        // Ayuda: las frases del cálculo (1..FrasesDelCalculo) y lo que la app no hace (1..CosasQueNoHace).
        ["Ayuda_Calculo_*"] = () => Conteo("ViewModels/AyudaViewModel.cs", "FrasesDelCalculo"),
        ["Ayuda_NoHace_*"] = () => Conteo("ViewModels/AyudaViewModel.cs", "CosasQueNoHace"),
    };

    /// <summary>
    /// Familias que no se pueden validar con una lista de valores. Cada una dice por qué.
    /// </summary>
    private static readonly Dictionary<string, string> FamiliasExcluidas = new()
    {
        // --- Opcionales a propósito ---
        ["AyudaEnlace_*"] =
            "Texto opcional del enlace de TerminoAyuda: se lee con TextoOpcional (try/catch) y sin clave usa Ayuda_QueEsEsto.",
    };

    // ===================================================================================================

    private static IEnumerable<string> Nombres<T>() where T : struct, Enum => Enum.GetNames<T>();

    // Los estados de la meta que no se muestran en la tarjeta (MetasViewModel.MostrarTarjeta).
    private static IEnumerable<string> SinEstadosDeTarjeta()
        => Nombres<EstadoMeta>().Except([nameof(EstadoMeta.Cumpliendo), nameof(EstadoMeta.EnRiesgo), nameof(EstadoMeta.Superada)]);

    private static string App(string ruta) => Path.Combine(TextosDeRecursos.Raiz(), "VoltiaMX", ruta);

    private static IEnumerable<string> ValoresDeEnum(string archivo, string nombre)
    {
        var codigo = File.ReadAllText(App(archivo));
        var cuerpo = Regex.Match(codigo, @"enum\s+" + nombre + @"\s*\{(?<cuerpo>[^}]*)\}").Groups["cuerpo"].Value;
        var sinComentarios = Regex.Replace(cuerpo, @"//[^\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        var valores = Regex.Matches(sinComentarios, @"\b([A-Z]\w*)\b(?:\s*=\s*\d+)?\s*(?:,|$)").Select(m => m.Groups[1].Value).ToList();
        Assert.True(valores.Count > 0, $"No encontré los valores de {nombre} en {archivo}");
        return valores;
    }

    private static IEnumerable<string> Conteo(string archivo, string constante)
    {
        var m = Regex.Match(File.ReadAllText(App(archivo)), constante + @"\s*=\s*(\d+)");
        Assert.True(m.Success, $"No encontré {constante} en {archivo}");
        return Enumerable.Range(1, int.Parse(m.Groups[1].Value)).Select(n => n.ToString());
    }

    private static IEnumerable<string> TerminosDeAyuda()
    {
        var codigo = File.ReadAllText(App("ViewModels/AyudaViewModel.cs"));
        var lista = Regex.Match(codigo, @"Terminos\s*=\s*\[(?<lista>[^\]]*)\]").Groups["lista"].Value;
        var terminos = Regex.Matches(lista, "\"(\\w+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.True(terminos.Count > 0, "No encontré AyudaViewModel.Terminos");
        foreach (var vista in Directory.EnumerateFiles(App("Views"), "*.xaml", SearchOption.AllDirectories))
        {
            terminos.AddRange(Regex.Matches(File.ReadAllText(vista), @"<local:TerminoAyuda\b[^>]*\bClave=""(\w+)""").Select(m => m.Groups[1].Value));
        }

        return terminos.Distinct();
    }

    // ===================================================================================================

    private static Dictionary<string, string> Fuentes(string carpeta, string patron)
        => Directory.EnumerateFiles(App(carpeta), patron, SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToDictionary(f => Path.GetRelativePath(TextosDeRecursos.Raiz(), f), File.ReadAllText);

    private static Dictionary<string, string> Vistas() => Fuentes("Views", "*.xaml");

    private static Dictionary<string, string> Codigo()
        => new[] { "ViewModels", "Services", "Views", "Helpers" }
            .Where(c => Directory.Exists(App(c)))
            .SelectMany(c => Fuentes(c, "*.cs"))
            .ToDictionary(p => p.Key, p => p.Value);

    private static HashSet<string> ClavesReales() => [.. TextosDeRecursos.Todos().Keys];

    [Fact]
    public void CadaClaveEscritaEnLasVistasYElCodigo_ExisteEnElResw()
    {
        var faltan = BuscadorDeClaves.Faltantes(ClavesReales(), Vistas(), Codigo());

        Assert.True(faltan.Count == 0, "Faltan en Resources.resw:\n" + string.Join("\n", faltan));
    }

    [Fact]
    public void CadaFamiliaDinamica_EstaValidadaOExcluidaConSuMotivo()
    {
        var familias = BuscadorDeClaves.FamiliasDinamicas(Codigo());

        var sinRevisar = familias.Keys.Where(f => !FamiliasValidadas.ContainsKey(f) && !FamiliasExcluidas.ContainsKey(f))
            .Select(f => $"{f} (en {string.Join(", ", familias[f])})")
            .ToList();
        Assert.True(sinRevisar.Count == 0, "Familias sin validar ni excluir:\n" + string.Join("\n", sinRevisar));

        // Una excepción o una validación que ya no corresponde a ningún uso sobra.
        var sobran = FamiliasValidadas.Keys.Concat(FamiliasExcluidas.Keys).Where(f => !familias.ContainsKey(f)).ToList();
        Assert.True(sobran.Count == 0, "Familias que ya no se usan:\n" + string.Join("\n", sobran));
        Assert.Empty(FamiliasValidadas.Keys.Intersect(FamiliasExcluidas.Keys));
    }

    [Fact]
    public void CadaValorDeUnaFamiliaValidada_TieneSuClave()
    {
        var claves = ClavesReales();
        var faltan = FamiliasValidadas
            .SelectMany(f => f.Value().Select(valor => f.Key.Replace("*", valor)))
            .Where(c => !claves.Contains(c))
            .ToList();

        Assert.True(faltan.Count == 0, "Faltan en Resources.resw:\n" + string.Join("\n", faltan));
    }

    // ---- La guardiana detecta lo que debe detectar (resw y XAML en memoria) ----

    private const string ReswDePrueba = """
        <root>
          <data name="Hola_Titulo.Text"><value>HOLA</value></data>
          <data name="Hola_Boton.[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name"><value>Hola</value></data>
          <data name="Saludo"><value>Hola, {0}</value></data>
          <data name="Despedida"><value>Adiós</value></data>
        </root>
        """;

    private static HashSet<string> ClavesDe(string resw)
        => [.. XDocument.Parse(resw).Root!.Elements("data").Select(d => (string)d.Attribute("name")!)];

    [Fact]
    public void Guardiana_ConTodoEnElResw_NoReportaNada()
    {
        var vistas = new Dictionary<string, string> { ["Hola.xaml"] = """<TextBlock x:Uid="Hola_Titulo" /><Button x:Uid="Hola_Boton" />""" };
        var codigo = new Dictionary<string, string>
        {
            ["Hola.cs"] = """var a = textos["Saludo"]; var b = textos.Formato("Saludo", 1); var c = _textos["Hola_Titulo/Text"]; var d = new ResourceLoader().GetString("Despedida");""",
        };

        Assert.Empty(BuscadorDeClaves.Faltantes(ClavesDe(ReswDePrueba), vistas, codigo));
    }

    [Fact]
    public void Guardiana_DetectaUnXUidSinClave()
    {
        var vistas = new Dictionary<string, string> { ["Hola.xaml"] = "<TextBlock x:Uid=\"Hola_Titulo\" />\n<TextBlock x:Uid=\"Hola_Falta\" />" };

        var faltan = BuscadorDeClaves.Faltantes(ClavesDe(ReswDePrueba), vistas, new Dictionary<string, string>());

        Assert.Equal(["Hola.xaml:2 x:Uid Hola_Falta"], faltan);
    }

    [Fact]
    public void Guardiana_DetectaUnaClaveDelCodigoSinClaveEnElResw()
    {
        var codigo = new Dictionary<string, string>
        {
            ["Hola.cs"] = "var a = textos[\"Saludo\"];\nvar b = textos.Formato(\"NoExiste\", 1);\nvar c = textos[\"Hola_Titulo/Content\"];\nvar d = Recursos.GetString(\"TampocoExiste\");",
        };

        var faltan = BuscadorDeClaves.Faltantes(ClavesDe(ReswDePrueba), new Dictionary<string, string>(), codigo);

        Assert.Equal(["Hola.cs:2 NoExiste", "Hola.cs:3 Hola_Titulo/Content", "Hola.cs:4 TampocoExiste"], faltan);
    }

    [Fact]
    public void Guardiana_EncuentraLasFamiliasDinamicas()
    {
        var codigo = new Dictionary<string, string>
        {
            ["Hola.cs"] = "var a = textos[$\"Dac_{nivel}\"];\nvar b = textos.Formato($\"Metas_{estado}Titulo\", 1);\nvar c = TextoOpcional($\"AyudaEnlace_{Clave}\");",
        };

        var familias = BuscadorDeClaves.FamiliasDinamicas(codigo);

        Assert.Equal(["AyudaEnlace_*", "Dac_*", "Metas_*Titulo"], familias.Keys.Order());
    }
}

/// <summary>
/// Busca las claves de Resources.resw que usan las vistas (x:Uid) y el código (textos, GetString).
/// </summary>
internal static partial class BuscadorDeClaves
{
    [GeneratedRegex(@"x:Uid=""([^""]+)""")]
    private static partial Regex XUid();

    // textos["Clave"], _textos["Clave"], textos.Formato("Clave", …), GetString("Clave"), TextoOpcional("Clave").
    [GeneratedRegex(@"(?:textos(?:\.Formato\(|\[)|GetString\(|TextoOpcional\()\s*""([^""]+)""")]
    private static partial Regex ClaveFija();

    // Lo mismo con una cadena interpolada: $"Prefijo_{valor}".
    [GeneratedRegex(@"(?:textos(?:\.Formato\(|\[)|GetString\(|TextoOpcional\()\s*\$""([^""]+)""")]
    private static partial Regex ClaveArmada();

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex Interpolacion();

    /// <summary>
    /// Las claves que faltan, como "archivo:línea clave". Un x:Uid necesita al menos una clave "Uid.Propiedad"; en
    /// el código, "Clave/Propiedad" es la clave "Clave.Propiedad" del resw.
    /// </summary>
    public static List<string> Faltantes(
        IReadOnlySet<string> claves, IReadOnlyDictionary<string, string> vistas, IReadOnlyDictionary<string, string> codigo)
    {
        var uids = claves.Select(c => c.Split('.')[0]).ToHashSet();
        var faltan = new List<string>();
        foreach (var (archivo, texto) in vistas.OrderBy(v => v.Key))
        {
            foreach (var (linea, numero) in Lineas(texto))
            {
                faltan.AddRange(XUid().Matches(linea).Select(m => m.Groups[1].Value).Where(u => !uids.Contains(u))
                    .Select(u => $"{archivo}:{numero} x:Uid {u}"));
            }
        }

        foreach (var (archivo, texto) in codigo.OrderBy(c => c.Key))
        {
            foreach (var (linea, numero) in Lineas(texto))
            {
                faltan.AddRange(ClaveFija().Matches(linea).Select(m => m.Groups[1].Value).Where(c => !claves.Contains(c.Replace('/', '.')))
                    .Select(c => $"{archivo}:{numero} {c}"));
            }
        }

        return faltan;
    }

    /// <summary>
    /// Las familias de claves armadas en el código ("Prefijo_*"), con los archivos y líneas donde se arman.
    /// </summary>
    public static Dictionary<string, List<string>> FamiliasDinamicas(IReadOnlyDictionary<string, string> codigo)
    {
        var familias = new Dictionary<string, List<string>>();
        foreach (var (archivo, texto) in codigo.OrderBy(c => c.Key))
        {
            foreach (var (linea, numero) in Lineas(texto))
            {
                foreach (Match m in ClaveArmada().Matches(linea))
                {
                    var familia = Interpolacion().Replace(m.Groups[1].Value, "*");
                    if (!familias.TryGetValue(familia, out var usos))
                    {
                        familias[familia] = usos = [];
                    }

                    usos.Add($"{archivo}:{numero}");
                }
            }
        }

        return familias;
    }

    private static IEnumerable<(string Linea, int Numero)> Lineas(string texto)
        => texto.Split('\n').Select((l, i) => (l, i + 1));
}
