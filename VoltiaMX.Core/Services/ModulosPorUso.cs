using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Regla de qué módulos ve cada perfil (Fase 9a: la app es para hogares). Sin modo avanzado, todos ven lo mismo:
/// Mis recibos (Lecturas), Aparatos y "Cómo se calcula tu recibo" (Predictor). Con modo avanzado se suman los
/// módulos que da cada uso más Solar e Inquilinos.
/// </summary>
public static class ModulosPorUso
{
    /// <summary>
    /// Lo que ve cualquier perfil, con o sin modo avanzado.
    /// </summary>
    private static readonly ModuloApp[] DeHogar = [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos];

    /// <summary>
    /// Módulos de cada uso; solo se activan con el modo avanzado.
    /// </summary>
    private static readonly Dictionary<UsoApp, ModuloApp[]> PorUso = new()
    {
        // ModuloApp.Presupuesto ya no se activa para ningún uso. // SIN USO EN LA INTERFAZ desde Fase 7a
        [UsoApp.Casa] = [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos, ModuloApp.Metas],
        [UsoApp.Negocio] = [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Aparatos, ModuloApp.Metas, ModuloApp.Reportes],
        [UsoApp.Rentas] = [ModuloApp.Lecturas, ModuloApp.Inquilinos, ModuloApp.Reportes],
        [UsoApp.Solar] = [ModuloApp.Solar, ModuloApp.Reportes],
    };

    private static readonly ModuloApp[] DelModoAvanzado = [ModuloApp.Aparatos, ModuloApp.Solar, ModuloApp.Inquilinos];

    /// <summary>
    /// Sin modo avanzado, los de hogar (los usos guardados no cuentan). Con modo avanzado, además, los de cada uso
    /// y Aparatos, Solar e Inquilinos; sin usos (el paso se omitió) se toma Casa.
    /// </summary>
    public static IReadOnlySet<ModuloApp> Calcular(IEnumerable<UsoApp> usos, bool modoAvanzado)
    {
        ArgumentNullException.ThrowIfNull(usos);
        var modulos = DeHogar.ToHashSet();
        if (!modoAvanzado)
        {
            return modulos;
        }

        var elegidos = usos.ToList();
        if (elegidos.Count == 0)
        {
            elegidos.Add(UsoApp.Casa);
        }

        modulos.UnionWith(elegidos.SelectMany(uso => PorUso[uso]));
        modulos.UnionWith(DelModoAvanzado);
        return modulos;
    }
}
