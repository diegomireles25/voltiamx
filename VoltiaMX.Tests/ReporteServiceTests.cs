using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class ReporteServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private FabricaDePrueba _fabrica = null!;
    private ResumenDashboardService _resumenes = null!;
    private AparatoService _aparatos = null!;
    private ReporteService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        var calculadora = new TarifaCalculator();
        var proyeccion = new ProyeccionService(calculadora);
        var presupuesto = new PresupuestoInversoService(calculadora);
        _resumenes = new ResumenDashboardService(_fabrica, proyeccion, new DacService(), presupuesto, new TarifaCalculator());
        _aparatos = new AparatoService(_fabrica, new TarifaCalculator());
        _servicio = new ReporteService(
            _fabrica,
            _resumenes,
            new HistorialService(_fabrica, proyeccion, presupuesto),
            new LecturaService(_fabrica, calculadora),
            _aparatos,
            new MetaService(_fabrica, _resumenes, calculadora),
            new TarifaService(_fabrica, calculadora));
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> DemoAsync()
    {
        await new DatosDemoService(_fabrica, new TarifaCalculator()).CargarAsync(Hoy);
        await using var db = _fabrica.CreateDbContext();
        return await db.Lugares.Where(l => l.EsDemo).Select(l => l.Id).SingleAsync();
    }

    [Fact]
    public async Task ObtenerAsync_Demo_LasMismasCifrasQueElDashboard()
    {
        var lugarId = await DemoAsync();

        var datos = await _servicio.ObtenerAsync(lugarId, Hoy);

        // El reporte reutiliza el Resumen del Dashboard: pago proyectado ($1,572.20 con 1,049.20 kWh a la
        // fecha de prueba), desglose por rango, historial y DAC salen idénticos, no recalculados.
        var dashboard = await _resumenes.ObtenerAsync(Hoy);
        Assert.NotNull(datos.Resumen);
        Assert.Equal(dashboard!.MontoProyectado, datos.Resumen.MontoProyectado);
        Assert.Equal(dashboard.KwhProyectados, datos.Resumen.KwhProyectados);
        Assert.Equal(dashboard.DesgloseProyectado!.Desglose, datos.Resumen.DesgloseProyectado!.Desglose);
        Assert.Equal(dashboard.DesgloseProyectado.Total, datos.Resumen.DesgloseProyectado.Total);
        Assert.Equal(dashboard.Dac, datos.Resumen.Dac);
        Assert.Equal(dashboard.Historial, datos.Resumen.Historial);

        // El ranking es el de Aparatos (46/19/9/8 % de la demo).
        var ranking = await _aparatos.ObtenerRankingAsync(lugarId, Hoy);
        Assert.Equal(ranking.Filas, datos.Ranking!.Filas);
        Assert.Equal(4, datos.Aparatos.Count);

        Assert.True(datos.EsDemo);
        Assert.Equal("Casa de ejemplo", datos.Lugar!.Nombre);
        Assert.Equal("1C", datos.Tarifa!.Nombre);
        Assert.Equal(0m, datos.Tarifa.Rangos[0].Desde);
        Assert.Equal(6, datos.Periodos.Count(p => !p.EsActual));
        Assert.Equal(10, datos.Meta!.PorcentajeObjetivo);

        // Lecturas de la más antigua a la más reciente.
        Assert.Equal(datos.Lecturas.OrderBy(l => l.Fecha).Select(l => l.Fecha), datos.Lecturas.Select(l => l.Fecha));
    }

    [Fact]
    public async Task ObtenerAsync_LugarSinDatos_TodoVacio()
    {
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.OrderBy(t => t.Id).FirstAsync();
        var lugar = new Lugar { Nombre = "Mi casa", Tipo = TipoLugar.Casa, TarifaId = tarifa.Id, DiaCorte = 1 };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();

        var datos = await _servicio.ObtenerAsync(lugar.Id, Hoy);

        Assert.Equal("Mi casa", datos.Lugar!.Nombre);
        Assert.Null(datos.Resumen!.MontoProyectado);
        Assert.Null(datos.Resumen.DesgloseProyectado);
        Assert.Null(datos.Resumen.Dac);
        Assert.Empty(datos.Periodos);
        Assert.Empty(datos.Lecturas);
        Assert.Empty(datos.Aparatos);
        Assert.Null(datos.Ranking);
        Assert.Null(datos.Meta);
        Assert.False(datos.EsDemo);
    }

    [Fact]
    public async Task ObtenerAsync_TarifaComercial_SinLimiteDac()
    {
        await using var db = _fabrica.CreateDbContext();
        var comercial = await db.Tarifas.FirstAsync(t => t.Tipo == TipoTarifa.Comercial);
        var lugar = new Lugar { Nombre = "Local", Tipo = TipoLugar.Negocio, TarifaId = comercial.Id, DiaCorte = 1, MesesPorPeriodo = 1 };
        lugar.Periodos.Add(new Periodo { Inicio = new(2026, 8, 2), Fin = new(2026, 9, 1), KwhTotales = 900m });
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();

        var datos = await _servicio.ObtenerAsync(lugar.Id, Hoy);

        Assert.Null(datos.Tarifa!.LimiteDacKwhMensual);
        Assert.Null(datos.Resumen!.Dac);
        Assert.Single(datos.Periodos);
    }

    [Fact]
    public async Task ObtenerAsync_OtroLugarNoInterfiere()
    {
        await DemoAsync();
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.OrderBy(t => t.Id).FirstAsync();
        var otro = new Lugar { Nombre = "Otro", Tipo = TipoLugar.Casa, TarifaId = tarifa.Id, DiaCorte = 1 };
        db.Lugares.Add(otro);
        await db.SaveChangesAsync();

        var datos = await _servicio.ObtenerAsync(otro.Id, Hoy);

        Assert.Equal("Otro", datos.Lugar!.Nombre);
        Assert.Empty(datos.Lecturas);
        Assert.Empty(datos.Aparatos);
        Assert.Null(datos.Meta);
    }

    [Fact]
    public async Task ObtenerAsync_SinLugar_ReporteVacio()
    {
        var datos = await _servicio.ObtenerAsync(null, Hoy);

        Assert.Null(datos.Lugar);
        Assert.Null(datos.Resumen);
        Assert.Empty(datos.Periodos);
        Assert.False(datos.EsDemo);
    }
}
