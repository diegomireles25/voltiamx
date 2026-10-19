using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Regla de qué módulos ve cada perfil según los usos elegidos en el onboarding.
/// </summary>
public static class ModulosPorUso
{
    private static readonly Dictionary<UsoApp, ModuloApp[]> PorUso = new()
    {
        [UsoApp.Casa] = [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Presupuesto, ModuloApp.Aparatos],
        [UsoApp.Negocio] = [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos, ModuloApp.Reportes],
        [UsoApp.Rentas] = [ModuloApp.Lecturas, ModuloApp.Inquilinos, ModuloApp.Reportes],
        [UsoApp.Solar] = [ModuloApp.Solar, ModuloApp.Reportes],
    };

    private static readonly ModuloApp[] DelModoAvanzado = [ModuloApp.Aparatos, ModuloApp.Solar, ModuloApp.Inquilinos];

    /// <summary>
    /// Une los módulos de cada uso; el modo avanzado agrega Aparatos, Solar e Inquilinos.
    /// Sin usos (el paso se omitió) se toma Casa, el caso más común.
    /// </summary>
    public static IReadOnlySet<ModuloApp> Calcular(IEnumerable<UsoApp> usos, bool modoAvanzado)
    {
        var elegidos = usos.ToList();
        if (elegidos.Count == 0)
        {
            elegidos.Add(UsoApp.Casa);
        }

        var modulos = elegidos.SelectMany(uso => PorUso[uso]).ToHashSet();
        if (modoAvanzado)
        {
            modulos.UnionWith(DelModoAvanzado);
        }

        return modulos;
    }
}
