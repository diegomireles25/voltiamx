using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Dónde queda un recibo nuevo (Fase 9i): su periodo, el recibo siguiente cuyo inicio se mueve, o el rechazo con el
/// recibo con el que choca (<see cref="ChoqueInicio"/> y <see cref="ChoqueFin"/>).
/// </summary>
public sealed record UbicacionRecibo(
    ErrorReciboPorMonto? Error,
    DateOnly Inicio,
    DateOnly Fin,
    Periodo? Siguiente = null,
    DateOnly? NuevoInicioSiguiente = null,
    DateOnly? ChoqueInicio = null,
    DateOnly? ChoqueFin = null);

/// <summary>
/// Fase 9i: los recibos consecutivos dividen el tiempo en sus fechas de corte, aunque no estén a meses exactos (las
/// fechas reales se mueven unos días). El recibo nuevo termina en su corte y empieza al día siguiente del corte del
/// recibo anterior; sin anterior, sus meses antes. Si hay un recibo más reciente por monto, empieza al día siguiente
/// del corte nuevo. Un recibo cerrado con lecturas (con lectura el día de su corte) nunca se modifica.
/// </summary>
public static class CortesDeRecibos
{
    /// <summary>
    /// Dos cortes a menos de estos días seguramente son el mismo recibo escrito con otra fecha.
    /// </summary>
    public const int DiasMinimosEntreCortes = 20;

    /// <param name="periodos">Los recibos guardados del lugar.</param>
    /// <param name="fechasConLectura">Las fechas de las lecturas del lugar: un recibo con lectura en su corte está
    /// cerrado con lecturas.</param>
    /// <param name="corte">La fecha de corte del recibo nuevo.</param>
    /// <param name="meses">Los meses de un recibo del lugar.</param>
    public static UbicacionRecibo Ubicar(IReadOnlyList<Periodo> periodos, IReadOnlySet<DateOnly> fechasConLectura, DateOnly corte, int meses)
    {
        ArgumentNullException.ThrowIfNull(periodos);
        ArgumentNullException.ThrowIfNull(fechasConLectura);
        var predeterminado = corte.AddMonths(-Math.Max(meses, 1)).AddDays(1);

        if (periodos.FirstOrDefault(p => p.Fin == corte) is { } igual)
        {
            return new UbicacionRecibo(ErrorReciboPorMonto.CorteRepetido, predeterminado, corte, ChoqueInicio: igual.Inicio, ChoqueFin: igual.Fin);
        }

        if (periodos.Where(p => Math.Abs(p.Fin.DayNumber - corte.DayNumber) < DiasMinimosEntreCortes)
                .OrderBy(p => Math.Abs(p.Fin.DayNumber - corte.DayNumber))
                .FirstOrDefault() is { } cerca)
        {
            return new UbicacionRecibo(ErrorReciboPorMonto.CorteMuyCerca, predeterminado, corte, ChoqueInicio: cerca.Inicio, ChoqueFin: cerca.Fin);
        }

        if (periodos.FirstOrDefault(p => fechasConLectura.Contains(p.Fin) && p.Inicio <= corte && corte < p.Fin) is { } conLecturas)
        {
            return new UbicacionRecibo(
                ErrorReciboPorMonto.DentroDeLecturas, predeterminado, corte, ChoqueInicio: conLecturas.Inicio, ChoqueFin: conLecturas.Fin);
        }

        var anterior = periodos.Where(p => p.Fin < corte).MaxBy(p => p.Fin);
        var inicio = anterior is null ? predeterminado : anterior.Fin.AddDays(1);

        // El siguiente se mueve solo si es por monto (sin lectura en su corte).
        var siguiente = periodos.Where(p => p.Fin > corte).MinBy(p => p.Fin);
        return siguiente is not null && !fechasConLectura.Contains(siguiente.Fin)
            ? new UbicacionRecibo(null, inicio, corte, siguiente, corte.AddDays(1))
            : new UbicacionRecibo(null, inicio, corte);
    }
}
