using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public static class MetaValidador
{
    public const int PorcentajeMinimo = 1;
    public const int PorcentajeMaximo = 50;

    public static IReadOnlyList<CodigoErrorMeta> Validar(int porcentaje)
        => porcentaje is < PorcentajeMinimo or > PorcentajeMaximo ? [CodigoErrorMeta.PorcentajeFueraDeRango] : [];
}

/// <summary>
/// Progreso de una meta con lo ya calculado del periodo actual (consumo y proyección del Resumen) y
/// los periodos cerrados del lugar. Nada se guarda: se recalcula siempre.
/// </summary>
public static class CalculoMeta
{
    /// <param name="cerrados">Periodos cerrados del lugar, en cualquier orden.</param>
    /// <param name="consumidoKwh">kWh del periodo actual hasta hoy, o null sin lectura base.</param>
    /// <param name="proyectadoKwh">kWh proyectados al cierre del periodo actual.</param>
    /// <param name="diasParaCorte">Días que faltan para el corte (0 o negativo si ya pasó).</param>
    /// <param name="totalDe">Importe de un consumo con la tarifa del lugar; null para no calcular pesos.</param>
    public static ProgresoMeta Calcular(
        Meta meta,
        IEnumerable<Periodo> cerrados,
        decimal? consumidoKwh,
        decimal? proyectadoKwh,
        int diasParaCorte,
        Func<decimal, decimal>? totalDe = null)
    {
        ArgumentNullException.ThrowIfNull(meta);
        ArgumentNullException.ThrowIfNull(cerrados);

        if (!meta.Activa)
        {
            return new ProgresoMeta(EstadoMeta.Inactiva, meta.PorcentajeObjetivo, null, false, null, null, null, null, null, [], 0);
        }

        var ordenados = cerrados.OrderBy(p => p.Inicio).ToList();
        var fraccion = meta.PorcentajeObjetivo / 100m;
        var historial = Historial(ordenados, meta.FechaInicio, fraccion, totalDe);
        var racha = historial.TakeWhile(f => f.Cumplio).Count();

        if (ordenados.Count == 0)
        {
            return new ProgresoMeta(
                EstadoMeta.SinBase, meta.PorcentajeObjetivo, null, false, null, consumidoKwh, proyectadoKwh, null, null, historial, racha);
        }

        var periodoBase = ordenados[^1];
        var baseKwh = periodoBase.KwhTotales;
        var objetivo = baseKwh * (1m - fraccion);
        decimal? pesos = totalDe is null ? null : totalDe(baseKwh) - totalDe(objetivo);

        var estado = consumidoKwh is not decimal consumido ? EstadoMeta.SinConsumoMedido
            : consumido > objetivo ? EstadoMeta.Superada
            : proyectadoKwh > objetivo ? EstadoMeta.EnRiesgo
            : EstadoMeta.Cumpliendo;

        // Lo que queda del objetivo repartido en los días que faltan para el corte.
        decimal? porDia = consumidoKwh is decimal medido
            ? diasParaCorte > 0 ? Math.Max(0m, objetivo - medido) / diasParaCorte : 0m
            : null;

        return new ProgresoMeta(
            estado, meta.PorcentajeObjetivo, baseKwh, periodoBase.Estimado, objetivo, consumidoKwh, proyectadoKwh,
            porDia, pesos, historial, racha,
            BasePesos: totalDe?.Invoke(baseKwh),
            ObjetivoPesos: totalDe?.Invoke(objetivo),
            ConsumidoPesos: totalDe is not null && consumidoKwh is decimal c ? totalDe(c) : null,
            MontoARitmo: totalDe is not null && proyectadoKwh is decimal p ? totalDe(p) : null);
    }

    // Cada periodo que empieza desde la fecha de inicio contra el anterior, del más reciente al más antiguo.
    private static List<FilaHistorialMeta> Historial(
        List<Periodo> ordenados, DateOnly fechaInicio, decimal fraccion, Func<decimal, decimal>? totalDe)
    {
        var filas = new List<FilaHistorialMeta>();
        for (var i = ordenados.Count - 1; i >= 1; i--)
        {
            var (actual, anterior) = (ordenados[i], ordenados[i - 1]);
            if (actual.Inicio < fechaInicio || anterior.KwhTotales <= 0m)
            {
                continue;
            }

            var reduccion = (anterior.KwhTotales - actual.KwhTotales) / anterior.KwhTotales;
            filas.Add(new FilaHistorialMeta(
                actual.Inicio, actual.Fin, actual.KwhTotales, reduccion, reduccion >= fraccion, totalDe?.Invoke(actual.KwhTotales)));
        }

        return filas;
    }
}
