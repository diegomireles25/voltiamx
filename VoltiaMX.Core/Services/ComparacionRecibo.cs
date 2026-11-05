namespace VoltiaMX.Core.Services;

public enum EstadoComparacion
{
    /// <summary>
    /// No hay recibo anterior con consumo para comparar.
    /// </summary>
    SinReciboAnterior,

    /// <summary>
    /// El recibo actual lleva menos de <see cref="ComparacionRecibo.DiasMinimos"/> días (o aún no hay lecturas).
    /// </summary>
    AunEsPronto,
    MasAlto,
    MasBajo,

    /// <summary>
    /// La diferencia está dentro de <see cref="ComparacionRecibo.MargenIgual"/> %.
    /// </summary>
    Igual,
}

/// <summary>
/// Resultado de comparar el ritmo de este recibo contra el anterior, en pesos. <see cref="Porcentaje"/> es la
/// diferencia redondeada a entero (sin signo); <see cref="Dias"/>, los días que lleva este recibo;
/// <see cref="Diferencia"/>, los pesos de diferencia (sin signo; 0 si no se compara).
/// </summary>
public sealed record ResultadoComparacion(
    EstadoComparacion Estado, decimal Porcentaje, int Dias, decimal MontoARitmo = 0m, decimal MontoAnterior = 0m, decimal Diferencia = 0m);

/// <summary>
/// Comparación segura de Inicio, en pesos (D6): lo que pagarías a este ritmo contra el recibo anterior
/// recalculado con la tarifa actual, para que la diferencia se deba al consumo. No compara con muy pocos días
/// ni sin recibo anterior, y una diferencia pequeña cuenta como "parecido".
/// </summary>
public static class ComparacionRecibo
{
    /// <summary>
    /// Con menos días el ritmo cambia mucho de un día a otro.
    /// </summary>
    public const int DiasMinimos = 7;

    /// <summary>
    /// Diferencia (en %) que todavía cuenta como "parecido".
    /// </summary>
    public const decimal MargenIgual = 2m;

    /// <param name="montoARitmo">Lo que pagarías a este ritmo (null sin lecturas).</param>
    /// <param name="montoReciboAnterior">El recibo anterior recalculado con la tarifa actual (null si no hay).</param>
    public static ResultadoComparacion Evaluar(decimal? montoARitmo, decimal? montoReciboAnterior, int diasTranscurridos)
    {
        // Sin recibo anterior, o en cero: no hay contra qué comparar (y no se divide entre cero).
        if (montoReciboAnterior is not > 0m || montoReciboAnterior is not decimal anterior)
        {
            return new ResultadoComparacion(EstadoComparacion.SinReciboAnterior, 0m, diasTranscurridos);
        }

        if (diasTranscurridos < DiasMinimos || montoARitmo is not decimal ritmo)
        {
            return new ResultadoComparacion(EstadoComparacion.AunEsPronto, 0m, diasTranscurridos, MontoAnterior: anterior);
        }

        var pesos = ritmo - anterior;
        var porciento = (ritmo / anterior - 1m) * 100m;
        if (Math.Abs(porciento) <= MargenIgual)
        {
            return new ResultadoComparacion(EstadoComparacion.Igual, 0m, diasTranscurridos, ritmo, anterior, Math.Abs(pesos));
        }

        var porcentaje = Math.Round(Math.Abs(porciento), 0, MidpointRounding.AwayFromZero);
        return new ResultadoComparacion(
            pesos > 0m ? EstadoComparacion.MasAlto : EstadoComparacion.MasBajo, porcentaje, diasTranscurridos, ritmo, anterior, Math.Abs(pesos));
    }
}
