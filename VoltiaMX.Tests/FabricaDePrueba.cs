using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

/// <summary>
/// Base SQLite en memoria con las migraciones y las tarifas de ejemplo ya aplicadas.
/// La base vive mientras la conexión siga abierta. Los interceptores sirven para simular fallas.
/// </summary>
internal sealed class FabricaDePrueba : IDbContextFactory<VoltiaDbContext>, IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");
    private readonly IInterceptor[] _interceptores;

    private FabricaDePrueba(IInterceptor[] interceptores)
    {
        _interceptores = interceptores;
        _conexion.Open();
    }

    public static async Task<FabricaDePrueba> CrearAsync(params IInterceptor[] interceptores)
    {
        var fabrica = new FabricaDePrueba(interceptores);
        await using var db = fabrica.CreateDbContext();
        await new DbInicializador(db).InicializarAsync();
        return fabrica;
    }

    public VoltiaDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(_conexion).AddInterceptors(_interceptores).Options);

    public void Dispose() => _conexion.Dispose();
}
