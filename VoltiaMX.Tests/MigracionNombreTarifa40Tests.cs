using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

public sealed class MigracionNombreTarifa40Tests
{
    private const string MigracionAnterior = "20261020172358_Lecturas";

    /// <summary>
    /// Base poblada con el esquema anterior: tres tarifas con rangos, un lugar real con un periodo
    /// cerrado por lecturas (con su lectura de corte) y otro del recibo (sin lectura de corte), y un
    /// lugar de demo con su periodo y lecturas.
    /// </summary>
    internal const string Poblar = """
        INSERT INTO Tarifas (Id, Nombre, Tipo, LimiteDacKwhMensual, CargoFijoMensual, Vigencia) VALUES
            (1, '1', 'Domestica', '250.0', '0.0', '2026-01-01'),
            (2, '1C', 'Domestica', '850.0', '0.0', '2026-01-01'),
            (3, 'PDBT', 'Comercial', NULL, '60.0', '2026-01-01');
        INSERT INTO TarifaRangos (TarifaId, Nombre, KwhDesde, KwhHasta, PrecioPorKwh, Orden) VALUES
            (1, 'Básico', '0.0', '75.0', '1.0', 1), (1, 'Intermedio', '75.0', '140.0', '1.2', 2), (1, 'Excedente', '140.0', NULL, '3.5', 3),
            (2, 'Básico', '0.0', '150.0', '1.0', 1), (2, 'Intermedio', '150.0', '450.0', '1.25', 2), (2, 'Excedente', '450.0', NULL, '3.5', 3),
            (3, 'Único', '0.0', NULL, '4.2', 1);
        INSERT INTO Lugares (Id, Nombre, Tipo, TarifaId, DiaCorte, MesesPorPeriodo, EsDemo, TarifaPorConfirmar) VALUES
            (1, 'Mi casa', 'Casa', 2, 1, 2, 0, 0),
            (2, 'Local', 'Negocio', 3, 15, 1, 0, 0),
            (3, 'Casa de ejemplo', 'Casa', 2, 26, 2, 1, 0);
        INSERT INTO Periodos (Id, LugarId, Inicio, Fin, KwhTotales, Monto, Estimado, MontoRecibo) VALUES
            (1, 1, '2026-05-02', '2026-07-01', '600.0', '700.0', 0, NULL),
            (2, 2, '2026-08-16', '2026-09-15', '300.0', '1290.5', 0, NULL),
            (3, 3, '2026-07-27', '2026-09-26', '718.0', '823.0', 0, NULL);
        INSERT INTO Lecturas (LugarId, Fecha, ValorKwh, Origen, EsEstimada) VALUES
            (1, '2026-05-01', '1000.0', 'Manual', 0), (1, '2026-07-01', '1600.0', 'Manual', 0), (1, '2026-07-20', '1800.0', 'Manual', 0),
            (2, '2026-09-20', '5000.0', 'Manual', 0),
            (3, '2026-07-26', '47492.0', 'Manual', 0), (3, '2026-09-26', '48210.0', 'Manual', 0);
        """;

    internal static async Task<Dictionary<string, long>> ContarAsync(SqliteConnection conexion)
    {
        var conteos = new Dictionary<string, long>();
        foreach (var tabla in new[] { "Tarifas", "TarifaRangos", "Lugares", "Periodos", "Lecturas" })
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
            conteos[tabla] = (long)(await comando.ExecuteScalarAsync())!;
        }

        return conteos;
    }

    private static async Task<List<string>> IndicesUnicosAsync(SqliteConnection conexion)
    {
        await using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT name FROM sqlite_master WHERE type = 'index' AND sql LIKE 'CREATE UNIQUE%' ORDER BY name";
        var nombres = new List<string>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
        {
            nombres.Add(lector.GetString(0));
        }

        return nombres;
    }

    [Fact]
    public async Task Migrar_BasePoblada_ConservaFilasRelacionesEIndices()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;

        await using (var anterior = new VoltiaDbContext(opciones))
        {
            await anterior.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
            await anterior.Database.ExecuteSqlRawAsync(Poblar);
        }

        var conteosAntes = await ContarAsync(conexion);
        var indicesAntes = await IndicesUnicosAsync(conexion);

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(conteosAntes, await ContarAsync(conexion));
        // Se migra hasta la última versión: las migraciones posteriores pueden traer tablas nuevas con
        // sus índices (p. ej. IX_Metas_LugarId); lo que importa es que no se pierda ninguno de los que había.
        Assert.Subset((await IndicesUnicosAsync(conexion)).ToHashSet(), indicesAntes.ToHashSet());
        Assert.Contains("IX_Tarifas_Nombre", indicesAntes);
        Assert.Contains("IX_TarifaRangos_TarifaId_Orden", indicesAntes);

        var tarifas = await db.Tarifas.Include(t => t.Rangos).OrderBy(t => t.Id).ToListAsync();
        Assert.Equal([3, 3, 1], tarifas.Select(t => t.Rangos.Count));
        Assert.Equal([2, 3, 2], await db.Lugares.OrderBy(l => l.Id).Select(l => l.TarifaId).ToListAsync());
    }

    [Fact]
    public async Task Migrar_CopiaElMontoDelReciboSoloEnPeriodosRealesSinLecturaDeCorte()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;
        await using (var anterior = new VoltiaDbContext(opciones))
        {
            await anterior.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
            await anterior.Database.ExecuteSqlRawAsync(Poblar);
        }

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        // Periodo 1: tiene lectura de corte (1 jul). Periodo 2: del recibo, sin lectura el 15 sep.
        // Periodo 3: de la demo. Solo el 2 recibe MontoRecibo.
        var montosRecibo = await db.Periodos.OrderBy(p => p.Id).Select(p => p.MontoRecibo).ToListAsync();
        Assert.Equal([null, 1290.5m, null], montosRecibo);
    }
}
