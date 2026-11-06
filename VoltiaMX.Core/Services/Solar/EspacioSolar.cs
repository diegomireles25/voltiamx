namespace VoltiaMX.Core.Services;

/// <summary>
/// Espacio que ocupan los paneles (Fase 9e, D4). Es el espacio de los paneles con su paso entre filas, no el área de
/// techo libre que se necesita: no se sabe la forma del techo, su orientación ni sus sombras.
/// </summary>
public static class EspacioSolar
{
    /// <summary>
    /// Se redondea hacia arriba a medios metros cuadrados.
    /// </summary>
    public const decimal Redondeo = 0.5m;

    /// <summary>
    /// paneles × superficie de cada panel × (1 + espacio de paso / 100), redondeado hacia arriba a 0.5 m².
    /// </summary>
    public static decimal Calcular(int paneles, decimal superficiePanelM2, decimal espacioPasoPorciento)
    {
        var espacio = paneles * superficiePanelM2 * (1m + espacioPasoPorciento / 100m);
        return Math.Ceiling(espacio / Redondeo) * Redondeo;
    }
}
