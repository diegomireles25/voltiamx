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
/// Resultado de comparar el ritmo de este recibo contra el anterior. <see cref="Porcentaje"/> es la diferencia
/// redondeada a entero (sin signo); <see cref="Dias"/>, los días que lleva este recibo.
/// </summary>
public sealed record ResultadoComparacion(EstadoComparacion Estado, decimal Porcentaje, int Dias);

/// <summary>
/// Comparación segura de Inicio: no compara con muy pocos días ni sin recibo anterior, y una diferencia pequeña
/// cuenta como "parecido". Recibe lo que ya calcula el Resumen; no recalcula consumos.
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

    public static ResultadoComparacion Evaluar(decimal? consumoProyectado, decimal? consumoReciboAnterior, int diasTranscurridos)
    {
        // Sin recibo anterior, o con consumo 0: no hay contra qué comparar (y no se divide entre cero).
        if (consumoReciboAnterior is not > 0m)
        {
            return new ResultadoComparacion(EstadoComparacion.SinReciboAnterior, 0m, diasTranscurridos);
        }

        if (diasTranscurridos < DiasMinimos || consumoProyectado is not decimal proyectado)
        {
            return new ResultadoComparacion(EstadoComparacion.AunEsPronto, 0m, diasTranscurridos);
        }

        var diferencia = (proyectado / consumoReciboAnterior.Value - 1m) * 100m;
        if (Math.Abs(diferencia) <= MargenIgual)
        {
            return new ResultadoComparacion(EstadoComparacion.Igual, 0m, diasTranscurridos);
        }

        var porcentaje = Math.Round(Math.Abs(diferencia), 0, MidpointRounding.AwayFromZero);
        return new ResultadoComparacion(
            diferencia > 0m ? EstadoComparacion.MasAlto : EstadoComparacion.MasBajo, porcentaje, diasTranscurridos);
    }
}
