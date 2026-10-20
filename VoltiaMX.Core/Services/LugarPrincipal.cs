using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Lugar que muestran el Dashboard y la pantalla de lecturas.
/// </summary>
public static class LugarPrincipal
{
    /// <summary>
    /// El primer lugar real con lecturas o periodos; si no hay, la demo; si tampoco, el primer lugar real.
    /// Espera los lugares ordenados por Id y con sus lecturas y periodos cargados.
    /// </summary>
    public static Lugar? Elegir(IReadOnlyList<Lugar> lugares)
    {
        ArgumentNullException.ThrowIfNull(lugares);

        static bool TieneDatos(Lugar l) => l.Periodos.Count > 0 || l.Lecturas.Count > 0;
        return lugares.FirstOrDefault(l => !l.EsDemo && TieneDatos(l))
            ?? lugares.FirstOrDefault(l => l.EsDemo)
            ?? lugares.FirstOrDefault(l => !l.EsDemo);
    }
}
