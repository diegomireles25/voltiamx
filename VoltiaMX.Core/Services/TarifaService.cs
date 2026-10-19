using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class TarifaService(IDbContextFactory<VoltiaDbContext> fabrica) : ITarifaService
{
    public async Task<IReadOnlyList<Tarifa>> ObtenerTarifasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Tarifas.AsNoTracking().OrderBy(t => t.Nombre).ToListAsync(cancellationToken);
    }
}
