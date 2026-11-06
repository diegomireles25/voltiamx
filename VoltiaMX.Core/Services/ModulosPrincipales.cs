using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Módulos del menú: las dos pestañas de módulos y el resto, que se abre sin pestaña.
/// </summary>
public sealed record MenuModulos(IReadOnlyList<ModuloApp> Principales, IReadOnlyList<ModuloApp> Secundarios);

/// <summary>
/// Menú para hogares (Fase 9a): Inicio, Mis recibos (Lecturas), Aparatos y Configuración para todos los perfiles.
/// Aquí van las dos pestañas de módulos, en ese orden fijo; Inicio y Configuración los pone el Shell. Se aplica
/// sobre los módulos activos (uno no activo nunca aparece).
/// </summary>
public static class ModulosPrincipales
{
    public const int MaximoPrincipales = 2;

    /// <summary>
    /// Las pestañas de módulos, en este orden.
    /// </summary>
    private static readonly ModuloApp[] Pestanas = [ModuloApp.Lecturas, ModuloApp.Aparatos];

    /// <summary>
    /// Orden fijo de los módulos sin pestaña.
    /// </summary>
    private static readonly ModuloApp[] OrdenSecundarios =
    [
        ModuloApp.Predictor, ModuloApp.Metas, ModuloApp.Reportes, ModuloApp.Solar,
        ModuloApp.Inquilinos, ModuloApp.Aparatos, ModuloApp.Lecturas,
    ];

    public static MenuModulos Calcular(IEnumerable<ModuloApp> activos)
    {
        ArgumentNullException.ThrowIfNull(activos);

        // Presupuesto ya no tiene pantalla (Fase 7a): nunca aparece aunque una lista vieja lo traiga.
        var disponibles = activos.Where(m => m != ModuloApp.Presupuesto).ToHashSet();
        var principales = Pestanas.Where(disponibles.Contains).ToList();
        var secundarios = OrdenSecundarios
            .Where(m => disponibles.Contains(m) && !principales.Contains(m))
            .ToList();

        return new MenuModulos(principales, secundarios);
    }
}
