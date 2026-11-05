using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

public sealed class MigracionTarifaEjemploTests
{
    private const string PoblarExtra = """
        INSERT INTO Tarifas (Id, Nombre, Tipo, LimiteDacKwhMensual, CargoFijoMensual, Vigencia) VALUES
            (4, 'Mi tarifa', 'Domestica', '850.0', '0.0', '2026-03-01');
        INSERT INTO TarifaRangos (TarifaId, Nombre, KwhDesde, KwhHasta, PrecioPorKwh, Orden) VALUES
            (4, 'Único', '0.0', NULL, '2.1', 1);
        INSERT INTO Aparatos (LugarId, Nombre, Categoria, Cantidad, Watts, HorasPorDia, DiasPorSemana, CicloDeTrabajo) VALUES
            (1, 'Refrigerador', 'Refrigeracion', 1, '250.0', '24.0', 7, '0.5');
        INSERT INTO Metas (LugarId, PorcentajeObjetivo, FechaInicio, Activa) VALUES (1, 10, '2026-05-01', 1);
        INSERT INTO AvisosEnviados (LugarId, Tipo, Clave, FechaEnvio) VALUES
            (1, 'Corte', '2026-07-01', '2026-06-28 09:00:00');
        INSERT INTO Inquilinos (Id, LugarId, Nombre, Activo, FechaAlta) VALUES (1, 1, 'Depto 1', 1, '2026-05-01');
        INSERT INTO Repartos (Id, LugarId, PeriodoInicio, PeriodoFin, TotalRepartido, OrigenTotal, Modo, KwhPrincipal, FechaCreacion) VALUES
            (1, 1, '2026-05-02', '2026-07-01', '700.0', 'Recibo', 'Proporcional', '400.0', '2026-07-02 10:00:00');
        INSERT INTO RepartoInquilinos (RepartoId, InquilinoId, NombreInquilino, LecturaAnterior, LecturaActual, Kwh, Cuota) VALUES
            (1, 1, 'Depto 1', '100.0', '300.0', '200.0', '233.0');
        """;

    private static readonly string[] Tablas =
    [
        "Lugares", "Tarifas", "TarifaRangos", "Periodos", "Lecturas", "Aparatos", "Metas", "AvisosEnviados",
        "Inquilinos", "Repartos", "RepartoInquilinos",
    ];

    private static async Task<Dictionary<string, long>> ContarAsync(SqliteConnection conexion)
    {
        var conteos = new Dictionary<string, long>();
        foreach (var tabla in Tablas)
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
            conteos[tabla] = (long)(await comando.ExecuteScalarAsync())!;
        }

        return conteos;
    }

    [Fact]
    public async Task Migrar_BasePoblada_ConservaLasFilasYMarcaSoloLasTarifasSembradas()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;

        await using (var anterior = new VoltiaDbContext(opciones))
        {
            var migraciones = anterior.Database.GetMigrations().ToList();
            await anterior.GetService<IMigrator>().MigrateAsync(migraciones.Single(m => m.EndsWith("_Inquilinos", StringComparison.Ordinal)));
            await anterior.Database.ExecuteSqlRawAsync(MigracionNombreTarifa40Tests.Poblar);
            await anterior.Database.ExecuteSqlRawAsync(PoblarExtra);
        }

        var antes = await ContarAsync(conexion);
        Assert.All(antes.Values, conteo => Assert.True(conteo > 0));

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(antes, await ContarAsync(conexion));

        // 1, 1C y PDBT tienen nombres sembrados; "Mi tarifa" la escribió la persona.
        var marcas = await db.Tarifas.OrderBy(t => t.Id).Select(t => new { t.Nombre, t.EsDeEjemplo }).ToListAsync();
        Assert.Equal(
            [("1", true), ("1C", true), ("PDBT", true), ("Mi tarifa", false)],
            marcas.Select(m => (m.Nombre, m.EsDeEjemplo)));
    }
}
