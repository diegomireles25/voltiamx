using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class PerfilServiceTests : IAsyncLifetime
{
    private FabricaDePrueba _fabrica = null!;
    private PerfilService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new PerfilService(_fabrica);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> IdDeTarifaAsync(string nombre)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Tarifas.Where(t => t.Nombre == nombre).Select(t => t.Id).SingleAsync();
    }

    private async Task<Lugar> LugarPrincipalAsync()
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Lugares.Include(l => l.Tarifa).Include(l => l.Periodos).SingleAsync(l => !l.EsDemo);
    }

    [Fact]
    public async Task ObtenerPerfilAsync_SinOnboarding_DevuelveNull()
    {
        Assert.Null(await _servicio.ObtenerPerfilAsync());
    }

    [Fact]
    public async Task GuardarOnboardingAsync_GuardaPerfilCompletadoConUsosModulosYObjetivo()
    {
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding
        {
            Usos = [UsoApp.Casa],
            Objetivo = TipoObjetivo.PagarMenos,
            PresupuestoMax = 800m,
            NotificacionesActivas = true,
        });

        var perfil = await _servicio.ObtenerPerfilAsync();

        Assert.NotNull(perfil);
        Assert.True(perfil.OnboardingCompletado);
        Assert.True(perfil.NotificacionesActivas);
        Assert.Equal([UsoApp.Casa], perfil.Usos.Select(u => u.Uso));
        Assert.Equal(TipoObjetivo.PagarMenos, perfil.Objetivo?.Tipo);
        Assert.Equal(800m, perfil.Objetivo?.PresupuestoMax);
        Assert.Equal(Enum.GetValues<ModuloApp>().Length, perfil.Modulos.Count);
        Assert.Equal(
            [ModuloApp.Lecturas, ModuloApp.Predictor, ModuloApp.Presupuesto, ModuloApp.Aparatos],
            await _servicio.ObtenerModulosActivosAsync());
    }

    [Fact]
    public async Task GuardarOnboardingAsync_ConTarifaElegida_CreaElLugarConEsaTarifa()
    {
        var tarifa1F = await IdDeTarifaAsync("1F");

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding
        {
            Usos = [UsoApp.Negocio],
            NombreLugar = "Tortillería",
            Estado = "Sonora",
            Municipio = "Hermosillo",
            TarifaId = tarifa1F,
        });

        var lugar = await LugarPrincipalAsync();
        Assert.Equal("Tortillería", lugar.Nombre);
        Assert.Equal(TipoLugar.Negocio, lugar.Tipo);
        Assert.Equal("1F", lugar.Tarifa?.Nombre);
        Assert.Equal("Sonora", lugar.Estado);
        Assert.Equal("Hermosillo", lugar.Municipio);
        Assert.False(lugar.TarifaPorConfirmar);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_SinTarifa_UsaLaTarifa1PorConfirmar()
    {
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], TarifaId = null });

        var lugar = await LugarPrincipalAsync();
        Assert.Equal("1", lugar.Tarifa?.Nombre);
        Assert.True(lugar.TarifaPorConfirmar);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_SinNombreDeLugar_UsaUnNombrePorTipo()
    {
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Rentas], NombreLugar = "  " });

        var lugar = await LugarPrincipalAsync();
        Assert.Equal(TipoLugar.Propiedad, lugar.Tipo);
        Assert.Equal("Mi propiedad", lugar.Nombre);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_ConRecibo_GuardaElPeriodoYElDiaDeCorte()
    {
        var recibo = new ReciboCapturado(new DateOnly(2026, 9, 14), MesesDelPeriodo: 2, Kwh: 718m, Monto: 1_020.50m);

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], Recibo = recibo });

        var lugar = await LugarPrincipalAsync();
        var periodo = Assert.Single(lugar.Periodos);
        Assert.Equal(new DateOnly(2026, 7, 15), periodo.Inicio);
        Assert.Equal(new DateOnly(2026, 9, 14), periodo.Fin);
        Assert.Equal(718m, periodo.KwhTotales);
        Assert.Equal(1_020.50m, periodo.Monto);
        Assert.Equal(14, lugar.DiaCorte);
        Assert.Equal(2, lugar.MesesPorPeriodo);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_SinRecibo_ElLugarQuedaBimestral()
    {
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa] });

        Assert.Equal(2, (await LugarPrincipalAsync()).MesesPorPeriodo);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_ReciboMensual_ElLugarQuedaMensual()
    {
        var recibo = new ReciboCapturado(new DateOnly(2026, 9, 30), MesesDelPeriodo: 1, Kwh: 300m, Monto: 410m);

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], Recibo = recibo });

        Assert.Equal(1, (await LugarPrincipalAsync()).MesesPorPeriodo);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_TarifaComercial_ElLugarQuedaMensual()
    {
        var pdbt = await IdDeTarifaAsync("PDBT");

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Negocio], TarifaId = pdbt });

        Assert.Equal(1, (await LugarPrincipalAsync()).MesesPorPeriodo);
    }

    private async Task<List<Lectura>> LecturasAsync()
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Lecturas.ToListAsync();
    }

    [Fact]
    public async Task GuardarOnboardingAsync_ReciboConLecturaDelMedidor_GuardaLaLecturaDelCorte()
    {
        var recibo = new ReciboCapturado(new DateOnly(2026, 9, 14), 2, 718m, 1_020.50m, LecturaMedidor: 48_210m);

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], Recibo = recibo });

        var lectura = Assert.Single(await LecturasAsync());
        Assert.Equal((new DateOnly(2026, 9, 14), 48_210m, OrigenLectura.Manual, false),
            (lectura.Fecha, lectura.ValorKwh, lectura.Origen, lectura.EsEstimada));
        Assert.Equal((await LugarPrincipalAsync()).Id, lectura.LugarId);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_ReciboSinLecturaDelMedidor_NoGuardaLecturas()
    {
        var recibo = new ReciboCapturado(new DateOnly(2026, 9, 14), 2, 718m, 1_020.50m);

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], Recibo = recibo });

        Assert.Empty(await LecturasAsync());
    }

    [Fact]
    public async Task GuardarOnboardingAsync_RepetidoConLecturaDelMedidor_NoLaDuplica()
    {
        var fecha = new DateOnly(2026, 9, 14);
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding
        {
            Usos = [UsoApp.Casa], Recibo = new ReciboCapturado(fecha, 2, 718m, 1_020.50m, LecturaMedidor: 48_210m),
        });

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding
        {
            Usos = [UsoApp.Casa], Recibo = new ReciboCapturado(fecha, 2, 718m, 1_020.50m, LecturaMedidor: 48_300m),
        });

        Assert.Equal(48_300m, Assert.Single(await LecturasAsync()).ValorKwh);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_SoloPresupuesto_ObjetivoNoPasarmeDePresupuesto()
    {
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { PresupuestoMax = 900m });

        var perfil = await _servicio.ObtenerPerfilAsync();
        Assert.Equal(TipoObjetivo.NoPasarmeDePresupuesto, perfil?.Objetivo?.Tipo);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_DosVeces_ActualizaSinDuplicar()
    {
        var recibo = new ReciboCapturado(new DateOnly(2026, 9, 14), 2, 718m, 1_020.50m);
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa, UsoApp.Solar], Recibo = recibo });

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding
        {
            Usos = [UsoApp.Negocio],
            NombreLugar = "Local",
            Recibo = recibo with { Kwh = 720m },
        });

        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(1, await db.Perfiles.CountAsync());
        Assert.Equal(1, await db.Lugares.CountAsync());
        var periodo = Assert.Single(await db.Periodos.ToListAsync());
        Assert.Equal(720m, periodo.KwhTotales);
        Assert.Equal([UsoApp.Negocio], await db.PerfilUsos.Select(u => u.Uso).ToListAsync());
        Assert.Equal("Local", (await db.Lugares.SingleAsync()).Nombre);
    }

    [Fact]
    public async Task GuardarOnboardingAsync_ConLugarDeDemo_NoLoUsaComoLugarPrincipal()
    {
        await using (var db = _fabrica.CreateDbContext())
        {
            var tarifa = await db.Tarifas.FirstAsync();
            db.Lugares.Add(new Lugar { Nombre = "Demo", TarifaId = tarifa.Id, EsDemo = true });
            await db.SaveChangesAsync();
        }

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], NombreLugar = "Mi casa" });

        await using var verificacion = _fabrica.CreateDbContext();
        Assert.Equal("Demo", (await verificacion.Lugares.SingleAsync(l => l.EsDemo)).Nombre);
        Assert.Equal("Mi casa", (await verificacion.Lugares.SingleAsync(l => !l.EsDemo)).Nombre);
    }

    [Fact]
    public async Task ObtenerLugarPrincipalAsync_DevuelveElLugarRealConSuTarifa()
    {
        await using (var db = _fabrica.CreateDbContext())
        {
            var tarifa = await db.Tarifas.FirstAsync();
            db.Lugares.Add(new Lugar { Nombre = "Demo", TarifaId = tarifa.Id, EsDemo = true });
            await db.SaveChangesAsync();
        }

        Assert.Null(await _servicio.ObtenerLugarPrincipalAsync());

        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa], NombreLugar = "Mi casa" });

        var lugar = await _servicio.ObtenerLugarPrincipalAsync();
        Assert.Equal("Mi casa", lugar?.Nombre);
        Assert.Equal("1", lugar?.Tarifa?.Nombre);
    }

    [Fact]
    public async Task CambiarModoAvanzadoAsync_RecalculaLosModulosConLosMismosUsos()
    {
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa] });

        await _servicio.CambiarModoAvanzadoAsync(true);

        var activos = await _servicio.ObtenerModulosActivosAsync();
        Assert.Contains(ModuloApp.Solar, activos);
        Assert.Contains(ModuloApp.Inquilinos, activos);
        Assert.True((await _servicio.ObtenerPerfilAsync())?.ModoAvanzado);

        await _servicio.CambiarModoAvanzadoAsync(false);

        Assert.DoesNotContain(ModuloApp.Solar, await _servicio.ObtenerModulosActivosAsync());
    }

    [Fact]
    public async Task ReiniciarOnboardingAsync_MarcaPendienteSinBorrarDatos()
    {
        await _servicio.GuardarOnboardingAsync(new DatosOnboarding { Usos = [UsoApp.Casa] });

        await _servicio.ReiniciarOnboardingAsync();

        var perfil = await _servicio.ObtenerPerfilAsync();
        Assert.False(perfil?.OnboardingCompletado);
        Assert.Equal([UsoApp.Casa], perfil?.Usos.Select(u => u.Uso));
    }
}
