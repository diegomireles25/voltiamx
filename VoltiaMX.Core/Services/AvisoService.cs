using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Avisos pendientes del lugar principal (<see cref="LugarId"/> es null si no hay lugar o perfil).
/// </summary>
public sealed record AvisosDelLugar(int? LugarId, IReadOnlyList<AvisoPendiente> Pendientes);

public interface IAvisoService
{
    /// <summary>
    /// Evalúa los avisos del lugar principal y olvida las claves de DAC que ya no aplican. No marca nada
    /// como enviado: eso se hace después de mostrar la notificación.
    /// </summary>
    Task<AvisosDelLugar> EvaluarAsync(DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra que el aviso ya se mostró. Si ya estaba registrado, no hace nada.
    /// </summary>
    Task MarcarEnviadoAsync(int lugarId, AvisoPendiente aviso, DateTime ahora, CancellationToken cancellationToken = default);
}

public sealed class AvisoService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    IResumenDashboardService resumenes,
    IMetaService metas) : IAvisoService
{
    public async Task<AvisosDelLugar> EvaluarAsync(DateOnly hoy, CancellationToken cancellationToken = default)
    {
        bool activas, metasActivas;
        await using (var db = await fabrica.CreateDbContextAsync(cancellationToken))
        {
            var perfil = await db.Perfiles.AsNoTracking().Include(p => p.Usos).OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
            if (perfil is null)
            {
                return new AvisosDelLugar(null, []);
            }

            activas = perfil.NotificacionesActivas;
            // Fase 9b (D7): sin el módulo Metas (modo avanzado apagado) no hay avisos de meta.
            metasActivas = ModulosPorUso.Calcular(perfil.Usos.Select(u => u.Uso), perfil.ModoAvanzado).Contains(ModuloApp.Metas);
        }

        var resumen = await resumenes.ObtenerAsync(hoy, cancellationToken);
        if (resumen is null)
        {
            return new AvisosDelLugar(null, []);
        }

        var lugarId = resumen.LugarId;
        var meta = await metas.ObtenerProgresoAsync(lugarId, hoy, cancellationToken);

        await using var contexto = await fabrica.CreateDbContextAsync(cancellationToken);
        var enviadas = (await contexto.AvisosEnviados
            .Where(a => a.LugarId == lugarId)
            .Select(a => a.Clave)
            .ToListAsync(cancellationToken)).ToHashSet();

        var resultado = EvaluadorAvisos.Evaluar(resumen, meta, hoy, enviadas, activas, metasActivas);
        foreach (var olvidar in resultado.ARestablecer.Where(r => enviadas.Contains(r.Clave)))
        {
            await contexto.AvisosEnviados
                .Where(a => a.LugarId == lugarId && a.Tipo == olvidar.Tipo && a.Clave == olvidar.Clave)
                .ExecuteDeleteAsync(cancellationToken);
        }

        return new AvisosDelLugar(lugarId, resultado.Pendientes);
    }

    public async Task MarcarEnviadoAsync(int lugarId, AvisoPendiente aviso, DateTime ahora, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        if (await db.AvisosEnviados.AnyAsync(a => a.LugarId == lugarId && a.Tipo == aviso.Tipo && a.Clave == aviso.Clave, cancellationToken))
        {
            return;
        }

        db.AvisosEnviados.Add(new AvisoEnviado { LugarId = lugarId, Tipo = aviso.Tipo, Clave = aviso.Clave, FechaEnvio = ahora });
        await db.SaveChangesAsync(cancellationToken);
    }
}
