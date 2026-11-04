using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

public sealed class MigracionAvisosTests
{
    private const string PoblarAparatosYMetas = """
        INSERT INTO Aparatos (LugarId, Nombre, Categoria, Cantidad, Watts, HorasPorDia, DiasPorSemana, CicloDeTrabajo) VALUES
            (1, 'Refrigerador', 'Refrigeracion', 1, '250.0', '24.0', 7, '0.5');
        INSERT INTO Metas (LugarId, PorcentajeObjetivo, FechaInicio, Activa) VALUES
            (1, 10, '2026-05-01', 1),
            (3, 10, '2026-03-19', 1);
        """;

    private static async Task<long> ContarAsync(SqliteConnection conexion, string tabla)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Migrar_BasePoblada_ConservaLasFilasYCreaAvisosVacia()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;

        await using (var anterior = new VoltiaDbContext(opciones))
        {
            var migraciones = anterior.Database.GetMigrations().ToList();
            await anterior.GetService<IMigrator>().MigrateAsync(migraciones.Single(m => m.EndsWith("_Metas")));
            await anterior.Database.ExecuteSqlRawAsync(MigracionNombreTarifa40Tests.Poblar);
            await anterior.Database.ExecuteSqlRawAsync(PoblarAparatosYMetas);
        }

        var conteosAntes = await MigracionNombreTarifa40Tests.ContarAsync(conexion);
        var aparatosAntes = await ContarAsync(conexion, "Aparatos");
        var metasAntes = await ContarAsync(conexion, "Metas");
        Assert.All(conteosAntes.Values, conteo => Assert.True(conteo > 0));
        Assert.Equal((1, 2), (aparatosAntes, metasAntes));

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(conteosAntes, await MigracionNombreTarifa40Tests.ContarAsync(conexion));
        Assert.Equal(aparatosAntes, await ContarAsync(conexion, "Aparatos"));
        Assert.Equal(metasAntes, await ContarAsync(conexion, "Metas"));
        Assert.Equal(0, await db.AvisosEnviados.CountAsync());
    }
}
