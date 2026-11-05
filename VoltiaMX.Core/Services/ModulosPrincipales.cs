using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Módulos del menú: hasta dos principales (pestañas) y el resto, que va en la página Más.
/// </summary>
public sealed record MenuModulos(IReadOnlyList<ModuloApp> Principales, IReadOnlyList<ModuloApp> Secundarios);

/// <summary>
/// Regla del menú corto: Casa y Negocio → Lecturas y Aparatos; Rentas → Lecturas e Inquilinos; Solar → Solar.
/// Se aplica sobre los módulos activos (un módulo no activo nunca aparece), así que un perfil viejo sigue la
/// regla sin repetir el onboarding.
/// </summary>
public static class ModulosPrincipales
{
    public const int MaximoPrincipales = 2;

    private static readonly Dictionary<UsoApp, ModuloApp[]> CandidatosPorUso = new()
    {
        [UsoApp.Casa] = [ModuloApp.Lecturas, ModuloApp.Aparatos],
        [UsoApp.Negocio] = [ModuloApp.Lecturas, ModuloApp.Aparatos],
        [UsoApp.Rentas] = [ModuloApp.Lecturas, ModuloApp.Inquilinos],
        [UsoApp.Solar] = [ModuloApp.Solar],
    };

    /// <summary>
    /// Prioridad cuando hay más candidatos que lugares.
    /// </summary>
    private static readonly ModuloApp[] Prioridad = [ModuloApp.Lecturas, ModuloApp.Aparatos, ModuloApp.Inquilinos, ModuloApp.Solar];

    /// <summary>
    /// Orden fijo de la página Más.
    /// </summary>
    private static readonly ModuloApp[] OrdenSecundarios =
    [
        ModuloApp.Predictor, ModuloApp.Metas, ModuloApp.Reportes, ModuloApp.Solar,
        ModuloApp.Inquilinos, ModuloApp.Aparatos, ModuloApp.Lecturas,
    ];

    public static MenuModulos Calcular(IEnumerable<ModuloApp> activos, IEnumerable<UsoApp> usos)
    {
        ArgumentNullException.ThrowIfNull(activos);
        ArgumentNullException.ThrowIfNull(usos);

        // Presupuesto ya no tiene pantalla (Fase 7a): nunca aparece aunque una lista vieja lo traiga.
        var disponibles = activos.Where(m => m != ModuloApp.Presupuesto).ToHashSet();
        var elegidos = usos.Distinct().ToList();
        if (elegidos.Count == 0)
        {
            elegidos.Add(UsoApp.Casa);
        }

        var candidatos = elegidos.SelectMany(u => CandidatosPorUso[u]).ToHashSet();
        var principales = Prioridad
            .Where(m => candidatos.Contains(m) && disponibles.Contains(m))
            .Take(MaximoPrincipales)
            .ToList();
        var secundarios = OrdenSecundarios
            .Where(m => disponibles.Contains(m) && !principales.Contains(m))
            .ToList();

        return new MenuModulos(principales, secundarios);
    }
}
