using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface IAparatoService
{
    /// <summary>
    /// Aparatos del lugar, ordenados por nombre.
    /// </summary>
    Task<IReadOnlyList<Aparato>> ListarAsync(int lugarId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea o edita. Si hay errores de validación los devuelve sin guardar.
    /// </summary>
    Task<ResultadoGuardarAparato> GuardarAsync(AparatoEditable aparato, CancellationToken cancellationToken = default);

    /// <summary>
    /// False si el aparato ya no existía.
    /// </summary>
    Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aparatos del lugar de mayor a menor consumo, con su porcentaje del consumo mensual medido.
    /// </summary>
    Task<RankingAparatos> ObtenerRankingAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ranking, costo mensual de cada aparato (D4) y sugerencias de ahorro (D5) del lugar, con su tarifa y su
    /// consumo medido. Sin consumo medido o sin tarifa no hay pesos ni sugerencias.
    /// </summary>
    Task<CostosDelLugar> ObtenerCostosAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valores típicos para empezar a capturar (de ejemplo, no de la etiqueta del aparato).
    /// </summary>
    IReadOnlyList<PlantillaAparato> PlantillasDeReferencia();

    /// <summary>
    /// Fase 9g (D3): los costos agrupados por aparato típico ("Aire acondicionado · 3"), con el costo del grupo
    /// sobre su consumo total (<see cref="GruposDeAparatos"/>).
    /// </summary>
    IReadOnlyList<GrupoAparatos> Agrupar(CostosDelLugar costos);

    /// <summary>
    /// "Elegir mis aparatos" (Fase 9d, D1): crea los aparatos típicos nuevos con los valores de su plantilla y, de
    /// los que ya existen, solo cambia las horas y la cantidad (la potencia ajustada en el editor completo se
    /// conserva). Elimina los de <paramref name="quitar"/> (los que la persona desmarcó). Con un error no guarda nada.
    /// </summary>
    Task<ResultadoGuardarElegidos> GuardarElegidosAsync(
        int lugarId, IReadOnlyList<AparatoElegido> elegidos, IReadOnlyList<int> quitar, CancellationToken cancellationToken = default);
}
