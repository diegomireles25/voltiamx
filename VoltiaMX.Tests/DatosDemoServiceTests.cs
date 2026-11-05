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

        // Lecturas acumuladas: el periodo actual se mide desde el último corte (hace 24 días).
        var lugar = await LugarDemoAsync();
        var ultimoCorte = lugar.Periodos.Max(p => p.Fin);
        Assert.Equal(Hoy.AddDays(-24), ultimoCorte);
        var lecturas = lugar.Lecturas.OrderBy(l => l.Fecha).ToList();
        var baseActual = lecturas.Single(l => l.Fecha == ultimoCorte);
        Assert.Equal(Hoy, lecturas[^1].Fecha);
        Assert.Equal(17.2m * 24, lecturas[^1].ValorKwh - baseActual.ValorKwh);
        Assert.True(lecturas.Count(l => l.Fecha > ultimoCorte) >= 2);
    }

    [Fact]
    public async Task CargarAsync_CadaPeriodoCerradoSigueLasReglasYTieneSuLecturaDeCorte()
    {
        await _servicio.CargarAsync(Hoy);

        var lugar = await LugarDemoAsync();
        Assert.Equal(2, lugar.MesesPorPeriodo);
        var periodos = lugar.Periodos.OrderBy(p => p.Inicio).ToList();
        var lecturas = lugar.Lecturas.ToDictionary(l => l.Fecha, l => l.ValorKwh);
        Assert.DoesNotContain(lugar.Lecturas, l => l.EsEstimada);
        foreach (var periodo in periodos)
        {
            Assert.False(periodo.Estimado);
            Assert.Equal(periodo.KwhTotales, lecturas[periodo.Fin] - lecturas[periodo.Inicio.AddDays(-1)]);
        }

        for (var i = 1; i < periodos.Count; i++)
        {
            var esperada = VentanaPeriodo.Calcular(lugar.DiaCorte, 2, periodos[i - 1].Fin, periodos[i - 1].Fin);
            Assert.Equal(esperada, new Ventana(periodos[i].Inicio, periodos[i].Fin));
        }
    }

    [Fact]
    public async Task CargarAsync_LaDemoEsCoherenteConElServicioDeLecturas()
    {
        await _servicio.CargarAsync(Hoy);
        var lugar = await LugarDemoAsync();
        var lecturas = new LecturaService(_fabrica, new TarifaCalculator());
        var ultima = lugar.Lecturas.MaxBy(l => l.Fecha)!;

        var hoy = await lecturas.PrevisualizarAsync(new NuevaLectura(lugar.Id, Hoy, ultima.ValorKwh + 1m), Hoy);
        var enCerrado = await lecturas.PrevisualizarAsync(
            new NuevaLectura(lugar.Id, lugar.Periodos.Max(p => p.Fin), ultima.ValorKwh), Hoy);

        Assert.True(hoy.Aceptada);
        Assert.True(hoy.Reemplaza);
        Assert.Empty(hoy.Cierres);
        Assert.Equal(MotivoRechazoLectura.PeriodoCerrado, enCerrado.Rechazo);
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

    [Fact]
    public async Task CargarAsync_CreaLosCuatroAparatosDeLaDemo()
    {
        await _servicio.CargarAsync(Hoy);

        await using var db = _fabrica.CreateDbContext();
        var nombres = await db.Aparatos.Where(a => a.Lugar!.EsDemo).Select(a => a.Nombre).OrderBy(n => n).ToListAsync();
        Assert.Equal(["Aire acondicionado", "Iluminación", "Lavadora", "Refrigerador"], nombres);
    }

    [Fact]
    public async Task CargarAsync_ElRankingDeLaDemoDa46_19_9Y8PorCiento()
    {
        await _servicio.CargarAsync(Hoy);
        var lugar = await LugarDemoAsync();

        var ranking = await new AparatoService(_fabrica, new TarifaCalculator()).ObtenerRankingAsync(lugar.Id, Hoy);

        Assert.Equal(EstadoRanking.Normal, ranking.Estado);
        Assert.Equal(["Aire acondicionado", "Refrigerador", "Lavadora", "Iluminación"], ranking.Filas.Select(f => f.Nombre));
        var esperados = new[] { 0.46m, 0.19m, 0.09m, 0.08m };
        for (var i = 0; i < esperados.Length; i++)
        {
            Assert.InRange(ranking.Filas[i].Porcentaje!.Value, esperados[i] - 0.02m, esperados[i] + 0.02m);
        }

        Assert.InRange(ranking.SinIdentificar!.Value, 0.16m, 0.20m);
    }

    [Fact]
    public async Task QuitarAsync_BorraLosAparatosDeLaDemo()
    {
        await _servicio.CargarAsync(Hoy);

        await _servicio.QuitarAsync();

        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(0, await db.Aparatos.CountAsync());
    }

    [Fact]
    public async Task CargarAsync_CreaLaMetaDel10PorCientoConHistorialYRacha()
    {
        await _servicio.CargarAsync(Hoy);
        var lugar = await LugarDemoAsync();
        var calculadora = new TarifaCalculator();
        var metas = new MetaService(
            _fabrica,
            new ResumenDashboardService(_fabrica, new ProyeccionService(calculadora), new DacService(), new PresupuestoInversoService(calculadora), new TarifaCalculator()),
            calculadora);

        var meta = await metas.ObtenerAsync(lugar.Id);
        Assert.Equal(10, meta!.PorcentajeObjetivo);
        Assert.True(meta.Activa);
        Assert.Equal(Hoy.AddMonths(-7), meta.FechaInicio);

        // Entran los 3 últimos bimestres: 1046 contra 912 (−14.7 %, no), 871 contra 1046 (16.7 %, sí) y
        // 718 contra 871 (17.6 %, sí). Hoy: base 718 → objetivo 646.2; consumido 412.8; proyectado ≈1049.
        var progreso = await metas.ObtenerProgresoAsync(lugar.Id, Hoy);
        Assert.Equal([true, true, false], progreso!.Historial.Select(f => f.Cumplio));
        Assert.Equal(2, progreso.Racha);
        Assert.Equal(EstadoMeta.EnRiesgo, progreso.Estado);
        Assert.Equal(646.2m, progreso.ObjetivoKwh);
    }

    [Fact]
    public async Task QuitarAsync_BorraLaMetaDeLaDemo()
    {
        await _servicio.CargarAsync(Hoy);

        await _servicio.QuitarAsync();

        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(0, await db.Metas.CountAsync());
    }

    [Fact]
    public async Task CargarAsync_CreaTresInquilinosYElRepartoDelUltimoPeriodo()
    {
        await _servicio.CargarAsync(Hoy);

        var lugar = await LugarDemoAsync();
        var repartos = new RepartoService(_fabrica);
        var inquilinos = await new InquilinoService(_fabrica).ListarAsync(lugar.Id);
        Assert.Equal(["Depto 1", "Depto 2", "Depto 3"], inquilinos.Select(i => i.Nombre));
        Assert.All(inquilinos, i => Assert.True(i.Activo && i.TieneRepartos));

        // SoloMedido sobre el último bimestre (718 kWh): los submedidores suman 220 + 210 + 190 = 620,
        // así que el propietario absorbe la parte de 98 kWh.
        var listado = Assert.Single(await repartos.ListarAsync(lugar.Id));
        var reparto = (await repartos.ObtenerAsync(listado.Id))!;
        var ultimo = lugar.Periodos.MaxBy(p => p.Fin)!;
        Assert.Equal((ultimo.Inicio, ultimo.Fin, ultimo.Monto, 718m), (reparto.Inicio, reparto.Fin, reparto.TotalRepartido, reparto.KwhPrincipal));
        Assert.Equal((OrigenTotal.MontoCalculado, ModoReparto.SoloMedido), (reparto.Origen, reparto.Modo));
        Assert.Equal([220m, 210m, 190m], reparto.Lineas.Select(l => l.Kwh));
        Assert.Equal(Math.Round(ultimo.Monto * 620m / 718m, 2, MidpointRounding.AwayFromZero), reparto.TotalCobrado);
        Assert.True(reparto.EsDemo);
        Assert.Empty(reparto.Avisos);

        // Un reparto nuevo de ese periodo precarga las lecturas guardadas.
        var preparado = await repartos.PrepararAsync(lugar.Id);
        Assert.True(preparado.Periodo!.TieneReparto);
        Assert.Equal([3_400m, 5_820m, 1_430m], preparado.Filas.Select(f => f.LecturaActual));
    }

    [Fact]
    public async Task QuitarAsync_BorraInquilinosYRepartosDeLaDemo()
    {
        await _servicio.CargarAsync(Hoy);

        await _servicio.QuitarAsync();

        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(0, await db.Inquilinos.CountAsync());
        Assert.Equal(0, await db.Repartos.CountAsync());
        Assert.Equal(0, await db.RepartoInquilinos.CountAsync());
    }
}
