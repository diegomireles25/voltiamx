namespace VoltiaMX.Core.Services;

/// <summary>
/// Días que cubre un periodo de facturación, ambos incluidos.
/// </summary>
public readonly record struct Ventana(DateOnly Inicio, DateOnly Fin)
{
    public int Dias => Fin.DayNumber - Inicio.DayNumber + 1;
}

/// <summary>
/// Fechas del periodo actual a partir del día de corte de CFE.
/// </summary>
public static class VentanaPeriodo
{
    /// <summary>
    /// Con un periodo cerrado, el actual empieza al día siguiente de <paramref name="ultimoCorte"/> y
    /// termina en el corte <paramref name="mesesPorPeriodo"/> meses después. Sin periodos cerrados se
    /// parte del corte más reciente en o antes de <paramref name="referencia"/> (la primera lectura, o
    /// hoy si no hay lecturas). El día de corte se limita al último día de los meses más cortos.
    /// </summary>
    public static Ventana Calcular(int diaCorte, int mesesPorPeriodo, DateOnly? ultimoCorte, DateOnly referencia)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(diaCorte, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(diaCorte, 31);
        ArgumentOutOfRangeException.ThrowIfLessThan(mesesPorPeriodo, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mesesPorPeriodo, 2);

        var ancla = ultimoCorte ?? CorteEnOAntesDe(diaCorte, referencia);
        return new Ventana(ancla.AddDays(1), CorteDelMes(diaCorte, ancla.AddMonths(mesesPorPeriodo)));
    }

    /// <summary>
    /// Fecha de corte del mes de <paramref name="mes"/>: el día de corte o el último día si el mes es más corto.
    /// </summary>
    public static DateOnly CorteDelMes(int diaCorte, DateOnly mes)
        => new(mes.Year, mes.Month, Math.Min(diaCorte, DateTime.DaysInMonth(mes.Year, mes.Month)));

    private static DateOnly CorteEnOAntesDe(int diaCorte, DateOnly fecha)
    {
        var corte = CorteDelMes(diaCorte, fecha);
        return corte <= fecha ? corte : CorteDelMes(diaCorte, fecha.AddMonths(-1));
    }
}
