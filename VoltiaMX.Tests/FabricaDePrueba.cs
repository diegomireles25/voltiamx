using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

/// <summary>
/// Base SQLite en memoria con las migraciones y las tarifas de ejemplo ya aplicadas.
/// La base vive mientras la conexión siga abierta.
/// </summary>
internal sealed class FabricaDePrueba : IDbContextFactory<VoltiaDbContext>, IDisposable
{
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");

    private FabricaDePrueba()
    {
        _conexion.Open();
    }

    public static async Task<FabricaDePrueba> CrearAsync()
    {
        var fabrica = new FabricaDePrueba();
        await using var db = fabrica.CreateDbContext();
        await new DbInicializador(db).InicializarAsync();
        return fabrica;
    }

    public VoltiaDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(_conexion).Options);

    public void Dispose() => _conexion.Dispose();
}
