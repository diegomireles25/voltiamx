using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class MetaService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    IResumenDashboardService resumenes,
    ITarifaCalculator calculadora) : IMetaService
{
    public async Task<Meta?> ObtenerAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Metas.AsNoTracking().FirstOrDefaultAsync(m => m.LugarId == lugarId, cancellationToken);
    }

    public async Task<ResultadoGuardarMeta> GuardarAsync(
        int lugarId, int porcentaje, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        var errores = MetaValidador.Validar(porcentaje);
        if (errores.Count > 0)
        {
            return new ResultadoGuardarMeta(errores);
        }

        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var meta = await db.Metas.FirstOrDefaultAsync(m => m.LugarId == lugarId, cancellationToken);
        if (meta is null)
        {
            meta = new Meta { LugarId = lugarId };
            db.Metas.Add(meta);
        }

        if (meta.Id == 0 || !meta.Activa)
        {
            meta.FechaInicio = hoy;
        }

        meta.PorcentajeObjetivo = porcentaje;
        meta.Activa = true;
        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoGuardarMeta([]);
    }

    public async Task<bool> DesactivarAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Metas
            .Where(m => m.LugarId == lugarId)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.Activa, false), cancellationToken) > 0;
    }

    public async Task<ProgresoMeta?> ObtenerProgresoAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        Meta? meta;
        Lugar? lugar;
        await using (var db = await fabrica.CreateDbContextAsync(cancellationToken))
        {
            meta = await db.Metas.AsNoTracking().FirstOrDefaultAsync(m => m.LugarId == lugarId, cancellationToken);
            if (meta is null)
            {
                return null;
            }

            lugar = await db.Lugares
                .AsNoTracking()
                .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
                .Include(l => l.Periodos)
                .AsSplitQuery()
                .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);
        }

        // Consumo y proyección del periodo actual: los mismos del Dashboard.
        var resumen = await resumenes.ObtenerDeLugarAsync(lugarId, hoy, cancellationToken);
        var tarifa = lugar?.Tarifa;
        var meses = Math.Max(lugar?.MesesPorPeriodo ?? 1, 1);

        return CalculoMeta.Calcular(
            meta,
            lugar?.Periodos ?? [],
            resumen?.KwhHastaHoy,
            resumen?.KwhProyectados,
            resumen?.DiasParaCorte ?? 0,
            tarifa is null ? null : kwh => calculadora.Calcular(kwh, tarifa, meses).Total);
    }
}
