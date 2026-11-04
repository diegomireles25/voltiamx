using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class AvisoServiceTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private FabricaDePrueba _fabrica = null!;
    private PerfilService _perfiles = null!;
    private AvisoService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        var calculadora = new TarifaCalculator();
        var resumenes = new ResumenDashboardService(
            _fabrica, new ProyeccionService(calculadora), new DacService(), new PresupuestoInversoService(calculadora));
        _perfiles = new PerfilService(_fabrica);
        _servicio = new AvisoService(_fabrica, resumenes, new MetaService(_fabrica, resumenes, calculadora));
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Perfil de Casa con avisos activos y su lugar con dos bimestres cerrados de 1200 kWh y un límite
    /// DAC de 250 kWh al mes: 1200 / 2 = 600 al mes, más de 250 → riesgo DAC Alto.
    /// </summary>
    private async Task<int> LugarConDacAltoAsync(bool notificaciones = true)
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], NotificacionesActivas = notificaciones });
        await using var db = _fabrica.CreateDbContext();
        var lugar = await db.Lugares.Include(l => l.Tarifa).SingleAsync(l => !l.EsDemo);
        lugar.Tarifa!.LimiteDacKwhMensual = 250m;
        lugar.MesesPorPeriodo = 2;
        lugar.Periodos.Add(new Periodo { Inicio = new(2026, 5, 2), Fin = new(2026, 7, 1), KwhTotales = 1_200m });
        lugar.Periodos.Add(new Periodo { Inicio = new(2026, 7, 2), Fin = new(2026, 9, 1), KwhTotales = 1_200m });
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private async Task CambiarConsumoAsync(decimal kwh)
    {
        await using var db = _fabrica.CreateDbContext();
        await db.Periodos.ExecuteUpdateAsync(p => p.SetProperty(x => x.KwhTotales, kwh));
    }

    private async Task<AvisoPendiente?> DacAsync() => (await _servicio.EvaluarAsync(Hoy)).Pendientes.SingleOrDefault(a => a.Tipo == TipoAviso.Dac);

    [Fact]
    public async Task EvaluarAsync_DevuelveElAvisoYElLugarPrincipal()
    {
        var lugarId = await LugarConDacAltoAsync();

        var avisos = await _servicio.EvaluarAsync(Hoy);

        Assert.Equal(lugarId, avisos.LugarId);
        Assert.Equal("Dac:Alto", Assert.Single(avisos.Pendientes, a => a.Tipo == TipoAviso.Dac).Clave);
    }

    [Fact]
    public async Task EvaluarAsync_YaMarcado_NoSeDevuelve()
    {
        var lugarId = await LugarConDacAltoAsync();
        var aviso = (await DacAsync())!;

        await _servicio.MarcarEnviadoAsync(lugarId, aviso, new DateTime(2026, 10, 19, 9, 0, 0));

        Assert.Null(await DacAsync());
    }

    [Fact]
    public async Task MarcarEnviadoAsync_DosVeces_NoDuplica()
    {
        var lugarId = await LugarConDacAltoAsync();
        var aviso = (await DacAsync())!;

        await _servicio.MarcarEnviadoAsync(lugarId, aviso, DateTime.Now);
        await _servicio.MarcarEnviadoAsync(lugarId, aviso, DateTime.Now);

        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(1, await db.AvisosEnviados.CountAsync());
    }

    [Fact]
    public async Task EvaluarAsync_ClaveEnviadaEnOtroLugar_NoInterfiere()
    {
        await LugarConDacAltoAsync();
        await using (var db = _fabrica.CreateDbContext())
        {
            var tarifa = await db.Tarifas.FirstAsync();
            var otro = new Lugar { Nombre = "Local", Tipo = TipoLugar.Negocio, TarifaId = tarifa.Id, DiaCorte = 1 };
            otro.AvisosEnviados.Add(new AvisoEnviado { Tipo = TipoAviso.Dac, Clave = "Dac:Alto", FechaEnvio = DateTime.Now });
            db.Lugares.Add(otro);
            await db.SaveChangesAsync();
        }

        Assert.NotNull(await DacAsync());
    }

    [Fact]
    public async Task EvaluarAsync_NotificacionesApagadas_NadaYNoTocaLasClaves()
    {
        var lugarId = await LugarConDacAltoAsync();
        await _servicio.MarcarEnviadoAsync(lugarId, (await DacAsync())!, DateTime.Now);
        await CambiarConsumoAsync(100m); // en Bajo restablecería, pero apagado no toca nada
        await _perfiles.CambiarNotificacionesAsync(false);

        var avisos = await _servicio.EvaluarAsync(Hoy);

        Assert.Empty(avisos.Pendientes);
        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(["Dac:Alto"], await db.AvisosEnviados.Select(a => a.Clave).ToListAsync());
    }

    [Fact]
    public async Task EvaluarAsync_DacBajaYVuelveASubir_AvisaDeNuevo()
    {
        var lugarId = await LugarConDacAltoAsync();
        await _servicio.MarcarEnviadoAsync(lugarId, (await DacAsync())!, DateTime.Now);

        // 100 / 2 = 50 al mes contra 250: Bajo. Se olvida el aviso de Alto.
        await CambiarConsumoAsync(100m);
        Assert.Null(await DacAsync());
        await using (var db = _fabrica.CreateDbContext())
        {
            Assert.Equal(0, await db.AvisosEnviados.CountAsync());
        }

        // Vuelve a 600 al mes: Alto otra vez, se avisa de nuevo.
        await CambiarConsumoAsync(1_200m);
        Assert.Equal("Dac:Alto", (await DacAsync())?.Clave);
    }

    [Fact]
    public async Task BorrarElLugar_BorraSusAvisosEnviados()
    {
        var lugarId = await LugarConDacAltoAsync();
        await _servicio.MarcarEnviadoAsync(lugarId, (await DacAsync())!, DateTime.Now);

        await using var db = _fabrica.CreateDbContext();
        await db.Lugares.Where(l => l.Id == lugarId).ExecuteDeleteAsync();

        Assert.Equal(0, await db.AvisosEnviados.CountAsync());
    }

    [Fact]
    public async Task EvaluarAsync_SinPerfil_Nada()
    {
        var avisos = await _servicio.EvaluarAsync(Hoy);

        Assert.Null(avisos.LugarId);
        Assert.Empty(avisos.Pendientes);
    }

    [Fact]
    public async Task EvaluarAsync_ConLaDemoComoLugarPrincipal_Nada()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], NotificacionesActivas = true });
        await new DatosDemoService(_fabrica, new TarifaCalculator()).CargarAsync(Hoy);

        Assert.Empty((await _servicio.EvaluarAsync(Hoy)).Pendientes);
    }

    [Fact]
    public async Task CambiarNotificacionesAsync_GuardaLaPreferencia()
    {
        await _perfiles.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], NotificacionesActivas = false });

        Assert.True(await _perfiles.CambiarNotificacionesAsync(true));
        Assert.True((await _perfiles.ObtenerPerfilAsync())!.NotificacionesActivas);
    }

    [Fact]
    public async Task CambiarNotificacionesAsync_SinPerfil_Falso()
    {
        Assert.False(await _perfiles.CambiarNotificacionesAsync(true));
    }
}
