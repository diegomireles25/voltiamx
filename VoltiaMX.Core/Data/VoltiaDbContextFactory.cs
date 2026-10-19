using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VoltiaMX.Core.Data;

/// <summary>
/// Solo para las herramientas de <c>dotnet ef</c> (Core es una biblioteca sin host).
/// La app configura su propia conexión al registrar el DbContext.
/// </summary>
public sealed class VoltiaDbContextFactory : IDesignTimeDbContextFactory<VoltiaDbContext>
{
    public VoltiaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VoltiaDbContext>()
            .UseSqlite("Data Source=voltia-design.db")
            .Options;

        return new VoltiaDbContext(options);
    }
}
