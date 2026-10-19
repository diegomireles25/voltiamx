using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class DatosDemoServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private FabricaDePrueba _fabrica = null!;
    private DatosDemoService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new DatosDemoService(_fabrica, new TarifaCalculator());
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Lugar> LugarDemoAsync()
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Lugares
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Periodos)
            .Include(l => l.Lecturas)
            .SingleAsync(l => l.EsDemo);
    }

    [Fact]
    public async Task HayDemoAsync_SinCargar_EsFalso()
    {
        Assert.False(await _servicio.HayDemoAsync());
    }

    [Fact]
    public async Task CargarAsync_CreaUnLugarDemoConDoceMesesDeHistorial()
    {
        await _servicio.CargarAsync(Hoy);

        var lugar = await LugarDemoAsync();
        Assert.True(await _servicio.HayDemoAsync());
        Assert.Equal("1C", lugar.Tarifa?.Nombre);

        // Seis bimestres consecutivos, sin huecos, que terminan antes de hoy.
        var periodos = lugar.Periodos.OrderBy(p => p.Inicio).ToList();
        Assert.Equal(6, periodos.Count);
        for (var i = 1; i < periodos.Count; i++)
        {
            Assert.Equal(periodos[i - 1].Fin.AddDays(1), periodos[i].Inicio);
        }

        Assert.True(periodos[^1].Fin < Hoy);
    }

    [Fact]
    public async Task CargarAsync_LosMontosSalenDelCalculadorDeTarifas()
    {
        await _servicio.CargarAsync(Hoy);

        var lugar = await LugarDemoAsync();
        var calculadora = new TarifaCalculator();
        Assert.All(lugar.Periodos, p =>
            Assert.Equal(calculadora.Calcular(p.KwhTotales, lugar.Tarifa!, mesesDelPeriodo: 2).Total, p.Monto));
    }

    [Fact]
    public async Task CargarAsync_LecturasDelPeriodoActualHastaHoy()
    {
        await _servicio.CargarAsync(Hoy);

        var lugar = await LugarDemoAsync();
        var inicioActual = lugar.Periodos.Max(p => p.Fin).AddDays(1);
        var lecturas = lugar.Lecturas.OrderBy(l => l.Fecha).ToList();
        Assert.True(lecturas.Count >= 2);
        Assert.Equal(inicioActual, lecturas[0].Fecha);
        Assert.Equal(Hoy, lecturas[^1].Fecha);
        Assert.True(lecturas[^1].ValorKwh > lecturas[0].ValorKwh);
    }

    [Fact]
    public async Task CargarAsync_DosVeces_NoDuplica()
    {
        await _servicio.CargarAsync(Hoy);
        await _servicio.CargarAsync(Hoy);

        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(1, await db.Lugares.CountAsync(l => l.EsDemo));
        Assert.Equal(6, await db.Periodos.CountAsync());
    }

    [Fact]
    public async Task QuitarAsync_BorraLaDemoEnCascadaSinTocarLosDatosReales()
    {
        await using (var db = _fabrica.CreateDbContext())
        {
            var tarifa = await db.Tarifas.FirstAsync();
            db.Lugares.Add(new Lugar
            {
                Nombre = "Mi casa",
                TarifaId = tarifa.Id,
                Periodos = [new Periodo { Inicio = new(2026, 7, 1), Fin = new(2026, 8, 31), KwhTotales = 500m, Monto = 700m }],
            });
            await db.SaveChangesAsync();
        }

        await _servicio.CargarAsync(Hoy);
        await _servicio.QuitarAsync();

        await using var verificacion = _fabrica.CreateDbContext();
        Assert.False(await _servicio.HayDemoAsync());
        Assert.Equal("Mi casa", (await verificacion.Lugares.SingleAsync()).Nombre);
        Assert.Equal(1, await verificacion.Periodos.CountAsync());
        Assert.Equal(0, await verificacion.Lecturas.CountAsync());
    }
}
