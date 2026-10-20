using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Periodo en curso de un lugar y lo medido en él. Lo usan el resumen del Dashboard y el historial.
/// </summary>
public static class PeriodoActual
{
    /// <summary>
    /// El periodo no avanza solo: se cierra al registrar la lectura en o después del corte.
    /// </summary>
    public static Ventana Ventana(Lugar lugar, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(lugar);
        DateOnly? ultimoCorte = lugar.Periodos.Count > 0 ? lugar.Periodos.Max(p => p.Fin) : null;
        var referencia = lugar.Lecturas.Count > 0 ? lugar.Lecturas.Min(l => l.Fecha) : hoy;
        return VentanaPeriodo.Calcular(Math.Clamp(lugar.DiaCorte, 1, 31), lugar.MesesPorPeriodo, ultimoCorte, referencia);
    }

    /// <summary>
    /// Lecturas del periodo de hoy o antes, ordenadas: la primera es la base (la de corte del periodo
    /// anterior o, si no hay, la primera del periodo).
    /// </summary>
    public static IReadOnlyList<Lectura> LecturasMedidas(IEnumerable<Lectura> lecturas, Ventana ventana, DateOnly hoy)
    {
        var corteAnterior = ventana.Inicio.AddDays(-1);
        return lecturas
            .Where(l => l.Fecha >= corteAnterior && l.Fecha <= ventana.Fin && l.Fecha <= hoy)
            .OrderBy(l => l.Fecha)
            .ToList();
    }

    /// <summary>
    /// kWh y días desde la lectura base hasta la última medida. Null si no hay dos lecturas.
    /// </summary>
    public static (decimal? Kwh, int DiasMedidos) Consumo(IReadOnlyList<Lectura> medidas, Ventana ventana)
    {
        if (medidas.Count < 2 || medidas[^1].Fecha <= medidas[0].Fecha)
        {
            return (null, 0);
        }

        var dias = Math.Min(medidas[^1].Fecha.DayNumber - medidas[0].Fecha.DayNumber, ventana.Dias);
        return (medidas[^1].ValorKwh - medidas[0].ValorKwh, dias);
    }
}
