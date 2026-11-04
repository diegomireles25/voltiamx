using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Core.Services;

public interface IReporteService
{
    /// <summary>
    /// Datos del lugar para el reporte y la exportación. Con <paramref name="lugarId"/> null (aún no hay
    /// lugar) devuelve un reporte vacío que se puede generar igual.
    /// </summary>
    Task<ReporteDatos> ObtenerAsync(int? lugarId, DateOnly hoy, CancellationToken cancellationToken = default);
}

/// <summary>
/// Junta lo que ya calculan los demás servicios; no recalcula nada, para que las cifras sean idénticas
/// a las del Dashboard.
/// </summary>
public sealed class ReporteService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    IResumenDashboardService resumenes,
    IHistorialService historiales,
    ILecturaService lecturas,
    IAparatoService aparatos,
    IMetaService metas,
    ITarifaService tarifas) : IReporteService
{
    public async Task<ReporteDatos> ObtenerAsync(int? lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var ahora = DateTime.Now;
        if (lugarId is not int id)
        {
            return new ReporteDatos { GeneradoEl = ahora };
        }

        LugarDelReporte? lugar;
        int? tarifaId;
        await using (var db = await fabrica.CreateDbContextAsync(cancellationToken))
        {
            var fila = await db.Lugares
                .AsNoTracking()
                .Where(l => l.Id == id)
                .Select(l => new { l.Id, l.Nombre, l.Tipo, l.EsDemo, l.TarifaId })
                .FirstOrDefaultAsync(cancellationToken);
            if (fila is null)
            {
                return new ReporteDatos { GeneradoEl = ahora };
            }

            lugar = new LugarDelReporte(fila.Id, fila.Nombre, fila.Tipo, fila.EsDemo);
            tarifaId = fila.TarifaId;
        }

        var resumen = await resumenes.ObtenerDeLugarAsync(id, hoy, cancellationToken);
        var historial = await historiales.ObtenerAsync(id, hoy, cancellationToken);
        var listadas = await lecturas.ListarAsync(id, int.MaxValue, cancellationToken);
        var ranking = await aparatos.ObtenerRankingAsync(id, hoy, cancellationToken);
        var lista = await aparatos.ListarAsync(id, cancellationToken);
        var meta = await metas.ObtenerProgresoAsync(id, hoy, cancellationToken);
        var tarifa = tarifaId is int t ? await tarifas.ObtenerParaEditarAsync(t, cancellationToken) : null;

        return new ReporteDatos
        {
            GeneradoEl = ahora,
            Lugar = lugar,
            Tarifa = tarifa is null ? null : new TarifaDelReporte(
                tarifa.Nombre,
                tarifa.Tipo,
                tarifa.CargoFijoMensual,
                tarifa.LimiteDacKwhMensual,
                tarifa.RangosConDesde().Select(r => new RangoDelReporte(r.Nombre, r.KwhDesde, r.KwhHasta, r.PrecioPorKwh)).ToList()),
            Resumen = resumen,
            Periodos = historial?.Periodos ?? [],
            Lecturas = listadas.Reverse().ToList(),
            Ranking = ranking.Estado == EstadoRanking.SinAparatos ? null : ranking,
            Aparatos = lista
                .Select(a => new AparatoDelReporte(
                    a.Nombre, a.Categoria, a.Cantidad, a.Watts, a.HorasPorDia, a.DiasPorSemana, a.CicloDeTrabajo,
                    ConsumoAparato.KwhPorMes(a)))
                .ToList(),
            Meta = meta is { Estado: not EstadoMeta.Inactiva } ? meta : null,
        };
    }
}
