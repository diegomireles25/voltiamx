using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class MetaServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private FabricaDePrueba _fabrica = null!;
    private MetaService _servicio = null!;
    private ResumenDashboardService _resumenes = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        var calculadora = new TarifaCalculator();
        _resumenes = new ResumenDashboardService(
            _fabrica, new ProyeccionService(calculadora), new DacService(), new PresupuestoInversoService(calculadora));
        _servicio = new MetaService(_fabrica, _resumenes, calculadora);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> CrearLugarAsync(string nombre, params decimal[] kwhPorPeriodo)
    {
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.OrderBy(t => t.Id).FirstAsync();
        var lugar = new Lugar { Nombre = nombre, Tipo = TipoLugar.Casa, TarifaId = tarifa.Id, DiaCorte = 1, MesesPorPeriodo = 1 };
        var inicio = new DateOnly(2026, 5, 2);
        foreach (var kwh in kwhPorPeriodo)
        {
            lugar.Periodos.Add(new Periodo { Inicio = inicio, Fin = inicio.AddMonths(1).AddDays(-1), KwhTotales = kwh });
            inicio = inicio.AddMonths(1);
        }

        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    [Fact]
    public async Task GuardarAsync_Nueva_ActivaConFechaDeHoy()
    {
        var lugarId = await CrearLugarAsync("Mi casa");

        var resultado = await _servicio.GuardarAsync(lugarId, 15, Hoy);

        Assert.Empty(resultado.Errores);
        var meta = await _servicio.ObtenerAsync(lugarId);
        Assert.NotNull(meta);
        Assert.Equal(15, meta.PorcentajeObjetivo);
        Assert.Equal(Hoy, meta.FechaInicio);
        Assert.True(meta.Activa);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GuardarAsync_FueraDeRango_NoGuarda(int porcentaje)
    {
        var lugarId = await CrearLugarAsync("Mi casa");

        var resultado = await _servicio.GuardarAsync(lugarId, porcentaje, Hoy);

        Assert.Equal([CodigoErrorMeta.PorcentajeFueraDeRango], resultado.Errores);
        Assert.Null(await _servicio.ObtenerAsync(lugarId));
    }

    [Fact]
    public async Task GuardarAsync_CambiarElPorcentaje_ConservaLaFechaDeInicio()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        await _servicio.GuardarAsync(lugarId, 10, Hoy);

        await _servicio.GuardarAsync(lugarId, 40, Hoy.AddDays(20));

        var meta = await _servicio.ObtenerAsync(lugarId);
        Assert.Equal(40, meta!.PorcentajeObjetivo);
        Assert.Equal(Hoy, meta.FechaInicio);
    }

    [Fact]
    public async Task DesactivarYReactivar_LaFechaDeInicioEsLaDeLaReactivacion()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        await _servicio.GuardarAsync(lugarId, 10, Hoy);

        Assert.True(await _servicio.DesactivarAsync(lugarId));
        Assert.False((await _servicio.ObtenerAsync(lugarId))!.Activa);

        await _servicio.GuardarAsync(lugarId, 12, Hoy.AddDays(30));
        var meta = await _servicio.ObtenerAsync(lugarId);
        Assert.True(meta!.Activa);
        Assert.Equal(Hoy.AddDays(30), meta.FechaInicio);

        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(1, await db.Metas.CountAsync(m => m.LugarId == lugarId));
    }

    [Fact]
    public async Task DesactivarAsync_SinMeta_Falso()
    {
        Assert.False(await _servicio.DesactivarAsync(await CrearLugarAsync("Mi casa")));
    }

    [Fact]
    public async Task OtroLugar_NoInterfiere()
    {
        var casa = await CrearLugarAsync("Mi casa", 500m);
        var local = await CrearLugarAsync("Local", 900m, 100m);
        await _servicio.GuardarAsync(casa, 10, Hoy);
        await _servicio.GuardarAsync(local, 30, Hoy);

        Assert.Equal(10, (await _servicio.ObtenerAsync(casa))!.PorcentajeObjetivo);
        var progreso = await _servicio.ObtenerProgresoAsync(casa, Hoy);
        Assert.Equal(500m, progreso!.BaseKwh);
        Assert.Equal(450m, progreso.ObjetivoKwh);
    }

    [Fact]
    public async Task ObtenerProgresoAsync_SinMeta_Null()
    {
        Assert.Null(await _servicio.ObtenerProgresoAsync(await CrearLugarAsync("Mi casa", 500m), Hoy));
    }

    [Fact]
    public async Task ObtenerProgresoAsync_SinPeriodos_SinBase()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        await _servicio.GuardarAsync(lugarId, 10, Hoy);

        Assert.Equal(EstadoMeta.SinBase, (await _servicio.ObtenerProgresoAsync(lugarId, Hoy))!.Estado);
    }

    [Fact]
    public async Task ObtenerProgresoAsync_ConPeriodosSinLecturas_SinConsumoMedidoYAhorroEnPesos()
    {
        var lugarId = await CrearLugarAsync("Mi casa", 600m, 500m);
        await _servicio.GuardarAsync(lugarId, 10, Hoy);

        var progreso = await _servicio.ObtenerProgresoAsync(lugarId, Hoy);

        Assert.Equal(EstadoMeta.SinConsumoMedido, progreso!.Estado);
        Assert.Equal(500m, progreso.BaseKwh);
        // Con la tarifa del lugar (mensual): Calcular(500) − Calcular(450).
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.Include(t => t.Rangos).OrderBy(t => t.Id).FirstAsync();
        var calculadora = new TarifaCalculator();
        Assert.Equal(
            calculadora.Calcular(500m, tarifa).Total - calculadora.Calcular(450m, tarifa).Total,
            progreso.AhorroPesosObjetivo);
    }

    [Fact]
    public async Task BorrarElLugar_BorraSuMeta()
    {
        var casa = await CrearLugarAsync("Mi casa");
        var local = await CrearLugarAsync("Local");
        await _servicio.GuardarAsync(casa, 10, Hoy);
        await _servicio.GuardarAsync(local, 10, Hoy);

        await using (var db = _fabrica.CreateDbContext())
        {
            await db.Lugares.Where(l => l.Id == casa).ExecuteDeleteAsync();
        }

        await using var verificacion = _fabrica.CreateDbContext();
        Assert.Equal([local], await verificacion.Metas.Select(m => m.LugarId).ToListAsync());
    }

    [Fact]
    public async Task ObtenerDeLugarAsync_EsElMismoResumenQueElDelLugarPrincipal()
    {
        await new DatosDemoService(_fabrica, new TarifaCalculator()).CargarAsync(Hoy);
        await using var db = _fabrica.CreateDbContext();
        var demo = await db.Lugares.SingleAsync(l => l.EsDemo);

        var principal = await _resumenes.ObtenerAsync(Hoy);
        var deLugar = await _resumenes.ObtenerDeLugarAsync(demo.Id, Hoy);

        Assert.Equal(principal!.KwhProyectados, deLugar!.KwhProyectados);
        Assert.Equal(principal.KwhHastaHoy, deLugar.KwhHastaHoy);
        Assert.Equal(principal.DiasParaCorte, deLugar.DiasParaCorte);
        Assert.Null(await _resumenes.ObtenerDeLugarAsync(9_999, Hoy));
    }
}
