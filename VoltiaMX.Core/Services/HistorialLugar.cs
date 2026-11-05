namespace VoltiaMX.Core.Services;

/// <summary>
/// Un periodo del historial. En el actual (<paramref name="EsActual"/>) los kWh y el monto son la
/// proyección al cierre y <paramref name="KwhPorDia"/> es el promedio medido hasta hoy.
/// </summary>
public sealed record PeriodoDeHistorial(
    DateOnly Inicio,
    DateOnly Fin,
    decimal Kwh,
    decimal Monto,
    bool Estimado,
    decimal? MontoRecibo,
    decimal KwhPorDia,
    bool EsActual);

/// <summary>
/// kWh consumidos desde la lectura base del periodo actual hasta <paramref name="Fecha"/>.
/// </summary>
public sealed record PuntoAcumulado(DateOnly Fecha, decimal Kwh);

/// <summary>
/// Historial de un lugar: periodos cerrados (del más antiguo al más reciente) y el actual si ya se
/// puede proyectar; la serie acumulada del periodo actual y, si hay presupuesto, los kWh que caben en él.
/// </summary>
public sealed record HistorialLugar(
    IReadOnlyList<PeriodoDeHistorial> Periodos,
    Ventana PeriodoActual,
    IReadOnlyList<PuntoAcumulado> AcumuladoActual,
    // SIN USO EN LA INTERFAZ desde Fase 7a
    decimal? KwhPresupuestoPeriodo)
{
    public int PeriodosCerrados => Periodos.Count(p => !p.EsActual);
}
