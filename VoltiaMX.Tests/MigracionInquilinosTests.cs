using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

public sealed class MigracionInquilinosTests
{
    private const string PoblarExtra = """
        INSERT INTO Aparatos (LugarId, Nombre, Categoria, Cantidad, Watts, HorasPorDia, DiasPorSemana, CicloDeTrabajo) VALUES
            (1, 'Refrigerador', 'Refrigeracion', 1, '250.0', '24.0', 7, '0.5');
        INSERT INTO Metas (LugarId, PorcentajeObjetivo, FechaInicio, Activa) VALUES (1, 10, '2026-05-01', 1);
        INSERT INTO AvisosEnviados (LugarId, Tipo, Clave, FechaEnvio) VALUES
            (1, 'Corte', '2026-07-01', '2026-06-28 09:00:00'), (2, 'Dac', '2026-09', '2026-09-20 09:00:00');
        """;

    private static readonly string[] Tablas =
        ["Lugares", "Tarifas", "TarifaRangos", "Periodos", "Lecturas", "Aparatos", "Metas", "AvisosEnviados"];

    private static async Task<Dictionary<string, long>> ContarAsync(SqliteConnection conexion, IEnumerable<string> tablas)
    {
        var conteos = new Dictionary<string, long>();
        foreach (var tabla in tablas)
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
            conteos[tabla] = (long)(await comando.ExecuteScalarAsync())!;
        }

        return conteos;
    }

    [Fact]
    public async Task Migrar_BasePoblada_ConservaLasFilasYCreaLasTablasVacias()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;

        await using (var anterior = new VoltiaDbContext(opciones))
        {
            var migraciones = anterior.Database.GetMigrations().ToList();
            await anterior.GetService<IMigrator>().MigrateAsync(migraciones.Single(m => m.EndsWith("_Avisos", StringComparison.Ordinal)));
            await anterior.Database.ExecuteSqlRawAsync(MigracionNombreTarifa40Tests.Poblar);
            await anterior.Database.ExecuteSqlRawAsync(PoblarExtra);
        }

        var antes = await ContarAsync(conexion, Tablas);
        Assert.All(antes.Values, conteo => Assert.True(conteo > 0));

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(antes, await ContarAsync(conexion, Tablas));
        Assert.Equal(0, await db.Inquilinos.CountAsync());
        Assert.Equal(0, await db.Repartos.CountAsync());
        Assert.Equal(0, await db.RepartoInquilinos.CountAsync());
    }
}
