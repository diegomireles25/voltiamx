using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class DbInicializadorTests : IDisposable
{
    // La base en memoria vive mientras la conexión siga abierta.
    private readonly SqliteConnection _conexion = new("Data Source=:memory:");

    public DbInicializadorTests()
    {
        _conexion.Open();
    }

    public void Dispose() => _conexion.Dispose();

    private VoltiaDbContext CrearContexto()
        => new(new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(_conexion).Options);

    private async Task InicializarAsync()
    {
        await using var db = CrearContexto();
        await new DbInicializador(db).InicializarAsync();
    }

    [Fact]
    public async Task InicializarAsync_BaseVacia_AplicaLaMigracionYSiembraLasTarifasDeEjemplo()
    {
        await InicializarAsync();

        await using var db = CrearContexto();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());

        var tarifas = await db.Tarifas.Include(t => t.Rangos).OrderBy(t => t.Nombre).ToListAsync();
        Assert.Equal(["1", "1C", "1F", "DAC", "PDBT"], tarifas.Select(t => t.Nombre));
        Assert.All(tarifas.Where(t => t.Nombre.StartsWith('1')), t => Assert.Equal(3, t.Rangos.Count));
        Assert.Single(tarifas.Single(t => t.Nombre == "DAC").Rangos);
        Assert.Single(tarifas.Single(t => t.Nombre == "PDBT").Rangos);
        Assert.All(tarifas, t => Assert.True(t.EsDeEjemplo));
    }

    [Fact]
    public async Task InicializarAsync_TarifasDeEjemplo_SonValidasParaElCalculador()
    {
        await InicializarAsync();

        await using var db = CrearContexto();
        var calculadora = new TarifaCalculator();
        foreach (var tarifa in await db.Tarifas.Include(t => t.Rangos).ToListAsync())
        {
            // Lanza si los rangos tienen huecos, traslapes o un último rango con límite.
            Assert.True(calculadora.Calcular(500m, tarifa, mesesDelPeriodo: 2).Total > 0m);
        }
    }

    [Fact]
    public async Task InicializarAsync_SegundaVez_NoDuplicaNiSobrescribeLasTarifas()
    {
        await InicializarAsync();
        await using (var db = CrearContexto())
        {
            var rango = await db.TarifaRangos.FirstAsync();
            rango.PrecioPorKwh = 9.99m; // el usuario editó un precio
            await db.SaveChangesAsync();
        }

        await InicializarAsync();

        await using var verificacion = CrearContexto();
        Assert.Equal(5, await verificacion.Tarifas.CountAsync());
        Assert.Equal(11, await verificacion.TarifaRangos.CountAsync());
        Assert.Contains(await verificacion.TarifaRangos.ToListAsync(), r => r.PrecioPorKwh == 9.99m);
    }
}
