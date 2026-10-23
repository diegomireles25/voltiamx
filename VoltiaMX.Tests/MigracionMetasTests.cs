using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

public sealed class MigracionMetasTests
{
    private const string MigracionAnterior = "20261023041640_Aparatos";

    private const string PoblarAparatos = """
        INSERT INTO Aparatos (LugarId, Nombre, Categoria, Cantidad, Watts, HorasPorDia, DiasPorSemana, CicloDeTrabajo) VALUES
            (1, 'Refrigerador', 'Refrigeracion', 1, '250.0', '24.0', 7, '0.5'),
            (3, 'Aire acondicionado', 'Climatizacion', 1, '1100.0', '8.0', 7, '0.9');
        """;

    private static async Task<long> ContarAsync(SqliteConnection conexion, string tabla)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
        return (long)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Migrar_BasePoblada_ConservaLasFilasYCreaMetasVacia()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;

        await using (var anterior = new VoltiaDbContext(opciones))
        {
            await anterior.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
            await anterior.Database.ExecuteSqlRawAsync(MigracionNombreTarifa40Tests.Poblar);
            await anterior.Database.ExecuteSqlRawAsync(PoblarAparatos);
        }

        var conteosAntes = await MigracionNombreTarifa40Tests.ContarAsync(conexion);
        var aparatosAntes = await ContarAsync(conexion, "Aparatos");
        Assert.All(conteosAntes.Values, conteo => Assert.True(conteo > 0));
        Assert.Equal(2, aparatosAntes);

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(conteosAntes, await MigracionNombreTarifa40Tests.ContarAsync(conexion));
        Assert.Equal(aparatosAntes, await ContarAsync(conexion, "Aparatos"));
        Assert.Equal(0, await db.Metas.CountAsync());
    }
}
