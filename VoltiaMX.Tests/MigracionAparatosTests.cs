using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

public sealed class MigracionAparatosTests
{
    private const string MigracionAnterior = "20261021045156_NombreTarifa40";

    [Fact]
    public async Task Migrar_BasePoblada_ConservaLasFilasYCreaAparatosVacia()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;

        await using (var anterior = new VoltiaDbContext(opciones))
        {
            await anterior.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
            await anterior.Database.ExecuteSqlRawAsync(MigracionNombreTarifa40Tests.Poblar);
        }

        var conteosAntes = await MigracionNombreTarifa40Tests.ContarAsync(conexion);
        Assert.All(conteosAntes.Values, conteo => Assert.True(conteo > 0));

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(conteosAntes, await MigracionNombreTarifa40Tests.ContarAsync(conexion));
        Assert.Equal(0, await db.Aparatos.CountAsync());
        Assert.Equal([2, 3, 2], await db.Lugares.OrderBy(l => l.Id).Select(l => l.TarifaId).ToListAsync());
    }
}
