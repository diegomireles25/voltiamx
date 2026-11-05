using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed record LugarDelReporte(int Id, string Nombre, TipoLugar Tipo, bool EsDemo);

/// <summary>
/// Rango de la tarifa con su "desde" calculado (límites mensuales); <see cref="Hasta"/> null = sin límite.
/// </summary>
public sealed record RangoDelReporte(string Nombre, decimal Desde, decimal? Hasta, decimal PrecioPorKwh);

public sealed record TarifaDelReporte(
    string Nombre, TipoTarifa Tipo, decimal CargoFijoMensual, decimal? LimiteDacKwhMensual, IReadOnlyList<RangoDelReporte> Rangos);

/// <summary>
/// Aparato con su consumo estimado al mes (<see cref="ConsumoAparato"/>).
/// </summary>
public sealed record AparatoDelReporte(
    string Nombre, CategoriaAparato Categoria, int Cantidad, decimal Watts, decimal HorasPorDia, int DiasPorSemana,
    decimal CicloDeTrabajo, decimal KwhPorMes);

/// <summary>
/// Todo lo que llevan el PDF y el Excel, tal como lo calculan los servicios de la app (las mismas cifras
/// del Dashboard). Solo datos: los textos los pone quien genera el documento. Lo que no existe queda en
/// null o vacío, y el documento lo dice ("Sin datos suficientes").
/// </summary>
public sealed record ReporteDatos
{
    public required DateTime GeneradoEl { get; init; }

    public LugarDelReporte? Lugar { get; init; }

    public TarifaDelReporte? Tarifa { get; init; }

    /// <summary>
    /// El mismo resumen del Dashboard: periodo actual, proyección, desglose, presupuesto y DAC.
    /// </summary>
    public ResumenDashboard? Resumen { get; init; }

    /// <summary>
    /// Todos los periodos del historial (cerrados y, si se puede proyectar, el actual al final).
    /// </summary>
    public IReadOnlyList<PeriodoDeHistorial> Periodos { get; init; } = [];

    /// <summary>
    /// Lecturas de la más antigua a la más reciente.
    /// </summary>
    public IReadOnlyList<LecturaListada> Lecturas { get; init; } = [];

    public RankingAparatos? Ranking { get; init; }

    public IReadOnlyList<AparatoDelReporte> Aparatos { get; init; } = [];

    public ProgresoMeta? Meta { get; init; }

    public bool EsDemo => Lugar?.EsDemo == true;

    /// <summary>
    /// La tarifa del lugar todavía trae los precios de ejemplo: los montos no son los del recibo.
    /// </summary>
    public bool PreciosDeEjemplo => Resumen?.PreciosDeEjemplo == true;
}
