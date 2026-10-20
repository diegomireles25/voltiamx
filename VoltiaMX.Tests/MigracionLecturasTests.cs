using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Tests;

public sealed class MigracionLecturasTests
{
    private const string MigracionAnterior = "20261019212003_Perfil";

    /// <summary>
    /// Una demo como la que se guardaba antes de la migración Lecturas: un lugar, seis bimestres y
    /// las lecturas del periodo actual. Se escribe con SQL porque el modelo de hoy ya tiene las
    /// columnas nuevas.
    /// </summary>
    private static string DemoDelEsquemaAnterior()
    {
        var sql = new StringBuilder("""
            INSERT INTO Tarifas (Id, Nombre, Tipo, LimiteDacKwhMensual, CargoFijoMensual, Vigencia)
                VALUES (1, '1C', 'Domestica', '850.0', '0.0', '2026-01-01');
            INSERT INTO Lugares (Id, Nombre, Tipo, TarifaId, DiaCorte, Estado, Municipio, EsDemo, TarifaPorConfirmar)
                VALUES (1, 'Casa de ejemplo', 'Casa', 1, 26, 'Sonora', 'Hermosillo', 1, 0);
            """);

        var inicio = new DateOnly(2025, 9, 27);
        foreach (var kwh in new[] { 820, 764, 912, 1_046, 871, 718 })
        {
            var fin = inicio.AddMonths(2).AddDays(-1);
            sql.Append($"INSERT INTO Periodos (LugarId, Inicio, Fin, KwhTotales, Monto) VALUES (1, '{inicio:yyyy-MM-dd}', '{fin:yyyy-MM-dd}', '{kwh}.0', '{kwh * 2}.0');");
            inicio = fin.AddDays(1);
        }

        for (var dia = 0; dia < 9; dia++)
        {
            var fecha = inicio.AddDays(dia * 3);
            sql.Append($"INSERT INTO Lecturas (LugarId, Fecha, ValorKwh, Origen) VALUES (1, '{fecha:yyyy-MM-dd}', '{48_210 + dia * 3 * 17}.0', 'Manual');");
        }

        return sql.ToString();
    }

    [Fact]
    public async Task Migrar_BaseConDemoDelEsquemaAnterior_ConservaLasFilasConLosValoresPorDefecto()
    {
        await using var conexion = new SqliteConnection("Data Source=:memory:");
        await conexion.OpenAsync();
        var opciones = new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(conexion).Options;

        await using (var anterior = new VoltiaDbContext(opciones))
        {
            await anterior.GetService<IMigrator>().MigrateAsync(MigracionAnterior);
            await anterior.Database.ExecuteSqlRawAsync(DemoDelEsquemaAnterior());
        }

        await using var db = new VoltiaDbContext(opciones);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        var lugar = await db.Lugares.Include(l => l.Periodos).Include(l => l.Lecturas).SingleAsync();
        Assert.Equal(2, lugar.MesesPorPeriodo);
        Assert.Equal(6, lugar.Periodos.Count);
        Assert.All(lugar.Periodos, p =>
        {
            Assert.False(p.Estimado);
            Assert.Null(p.MontoRecibo);
        });
        Assert.Equal(9, lugar.Lecturas.Count);
        Assert.All(lugar.Lecturas, l => Assert.False(l.EsEstimada));
        Assert.Equal(718m, lugar.Periodos.MaxBy(p => p.Fin)!.KwhTotales);
    }
}
