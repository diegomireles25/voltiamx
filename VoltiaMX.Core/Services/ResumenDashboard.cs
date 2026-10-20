using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Barra del historial: un periodo cerrado o el actual proyectado.
/// </summary>
public sealed record PeriodoHistorial(DateOnly Inicio, DateOnly Fin, decimal Kwh, decimal Monto, bool EsActual);

/// <summary>
/// Lo que el Dashboard necesita del lugar principal. Los montos no incluyen IVA ni DAP.
/// Lo que no se puede calcular (sin lecturas, sin presupuesto, tarifa sin límite DAC) queda en null.
/// </summary>
public sealed record ResumenDashboard
{
    public required string NombreLugar { get; init; }

    public required TipoLugar TipoLugar { get; init; }

    public required string NombreTarifa { get; init; }

    public bool TarifaPorConfirmar { get; init; }

    public bool EsDemo { get; init; }

    public DateOnly InicioPeriodo { get; init; }

    public DateOnly FinPeriodo { get; init; }

    public int MesesDelPeriodo { get; init; }

    public int DiaDelPeriodo { get; init; }

    public int DiasDelPeriodo { get; init; }

    /// <summary>
    /// Días que faltan para la fecha de corte; 0 o negativo cuando <see cref="PeriodoTerminado"/>.
    /// </summary>
    public int DiasParaCorte { get; init; }

    /// <summary>
    /// Ya llegó la fecha de corte y falta la lectura en o después de ella que cierra el periodo.
    /// </summary>
    public bool PeriodoTerminado { get; init; }

    public decimal? KwhHastaHoy { get; init; }

    public decimal? KwhProyectados { get; init; }

    public decimal? MontoProyectado { get; init; }

    public decimal? PromedioDiarioKwh { get; init; }

    public decimal? PresupuestoMax { get; init; }

    /// <summary>
    /// Monto proyectado menos el presupuesto: positivo si te pasas.
    /// </summary>
    public decimal? DiferenciaContraPresupuesto { get; init; }

    public decimal? KwhPorDiaRestante { get; init; }

    /// <summary>
    /// Promedio diario contra el ritmo que cuadra con el presupuesto: 0.12 = 12% arriba.
    /// </summary>
    public decimal? VariacionRitmoPresupuesto { get; init; }

    public EvaluacionDac? Dac { get; init; }

    public decimal? LimiteDacKwhMensual { get; init; }

    /// <summary>
    /// Hasta cinco periodos cerrados y, si hay proyección, el actual al final.
    /// </summary>
    public IReadOnlyList<PeriodoHistorial> Historial { get; init; } = [];

    /// <summary>
    /// kWh proyectados contra el último periodo cerrado: -0.06 = 6% abajo.
    /// </summary>
    public decimal? VariacionVsPeriodoAnterior { get; init; }

    public bool TieneDatos => Historial.Count > 0 || KwhHastaHoy is not null;
}

/// <summary>
/// Fila del resumen por propiedad (uso Rentas).
/// </summary>
public sealed record ResumenPropiedad(string Nombre, DateOnly? UltimoFin, decimal? UltimoKwh, decimal? UltimoMonto);
