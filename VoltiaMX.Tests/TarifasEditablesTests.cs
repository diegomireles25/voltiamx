using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class TarifasEditablesTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private FabricaDePrueba _fabrica = null!;
    private TarifaService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new TarifaService(_fabrica);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private static TarifaEditable Nueva(string nombre = "Mi tarifa") => new(
        null, nombre, TipoTarifa.Domestica, 0m, 850m, new DateOnly(2026, 1, 1),
        [new("Básico", 150m, 1.0m), new("Intermedio", 450m, 1.25m), new("Excedente", null, 3.5m)]);

    private async Task<int> IdAsync(string nombre)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Tarifas.Where(t => t.Nombre == nombre).Select(t => t.Id).SingleAsync();
    }

    private async Task<Tarifa> TarifaAsync(int id)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Tarifas.Include(t => t.Rangos).SingleAsync(t => t.Id == id);
    }

    private async Task<int> LugarAsync(string tarifa, bool porConfirmar = false, bool esDemo = false)
    {
        await using var db = _fabrica.CreateDbContext();
        var lugar = new Lugar { Nombre = esDemo ? "Demo" : "Mi casa", DiaCorte = 1, TarifaId = await IdAsync(tarifa), TarifaPorConfirmar = porConfirmar, EsDemo = esDemo };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    [Fact]
    public async Task Guardar_Nueva_LaCreaConRangosEnOrdenYDesdeCalculado()
    {
        var resultado = await _servicio.GuardarAsync(Nueva());

        Assert.True(resultado.Guardada);
        var tarifa = await TarifaAsync(resultado.Id!.Value);
        Assert.Equal(
            [(1, 0m, (decimal?)150m, 1.0m), (2, 150m, 450m, 1.25m), (3, 450m, null, 3.5m)],
            tarifa.Rangos.OrderBy(r => r.Orden).Select(r => (r.Orden, r.KwhDesde, r.KwhHasta, r.PrecioPorKwh)));
    }

    [Fact]
    public async Task Guardar_ConErrores_NoGuardaNada()
    {
        var resultado = await _servicio.GuardarAsync(Nueva("1C"));

        Assert.False(resultado.Guardada);
        Assert.Contains(new ErrorTarifa(CodigoErrorTarifa.NombreDuplicado), resultado.Errores);
        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(5, await db.Tarifas.CountAsync());
    }

    [Fact]
    public async Task Guardar_Edicion_CambiaPreciosYReemplazaRangosSinDuplicar()
    {
        var id = await IdAsync("1C");
        var editable = await _servicio.ObtenerParaEditarAsync(id);
        Assert.NotNull(editable);

        var resultado = await _servicio.GuardarAsync(editable with
        {
            Rangos = [new("Básico", 200m, 0.9m), new("Excedente", null, 4.0m)],
        });

        Assert.True(resultado.Guardada);
        var tarifa = await TarifaAsync(id);
        Assert.Equal(
            [(1, 0m, (decimal?)200m, 0.9m), (2, 200m, null, 4.0m)],
            tarifa.Rangos.OrderBy(r => r.Orden).Select(r => (r.Orden, r.KwhDesde, r.KwhHasta, r.PrecioPorKwh)));
        await using var db = _fabrica.CreateDbContext();
        Assert.Equal(2, await db.TarifaRangos.CountAsync(r => r.TarifaId == id));
    }

    [Fact]
    public async Task Guardar_SiFallaAlInsertarRangos_LaTarifaQuedaIntacta()
    {
        var falla = new FallaAlInsertarRangos();
        using var fabrica = await FabricaDePrueba.CrearAsync(falla);
        var servicio = new TarifaService(fabrica);
        int id;
        await using (var db = fabrica.CreateDbContext())
        {
            id = await db.Tarifas.Where(t => t.Nombre == "1C").Select(t => t.Id).SingleAsync();
        }

        var editable = await servicio.ObtenerParaEditarAsync(id);
        falla.Activa = true;
        await Assert.ThrowsAnyAsync<Exception>(() => servicio.GuardarAsync(editable! with
        {
            Nombre = "1C editada",
            Rangos = [new("Único", null, 2m)],
        }));

        await using var verificar = fabrica.CreateDbContext();
        var tarifa = await verificar.Tarifas.Include(t => t.Rangos).SingleAsync(t => t.Id == id);
        Assert.Equal("1C", tarifa.Nombre);
        Assert.Equal(3, tarifa.Rangos.Count);
    }

    [Fact]
    public async Task Duplicar_NombresRepetidos_CopiaCopia2()
    {
        var id = await IdAsync("1C");

        var primera = await _servicio.DuplicarAsync(id);
        var segunda = await _servicio.DuplicarAsync(id);

        Assert.Equal("1C (copia)", (await TarifaAsync(primera!.Value)).Nombre);
        var copia2 = await TarifaAsync(segunda!.Value);
        Assert.Equal("1C (copia 2)", copia2.Nombre);
        Assert.Equal(3, copia2.Rangos.Count);
        Assert.Equal(850m, copia2.LimiteDacKwhMensual);
    }

    [Fact]
    public async Task Eliminar_SinUso_LaBorraConSusRangos()
    {
        var id = await IdAsync("1F");

        var resultado = await _servicio.EliminarAsync(id);

        Assert.True(resultado.Eliminada);
        await using var db = _fabrica.CreateDbContext();
        Assert.False(await db.Tarifas.AnyAsync(t => t.Id == id));
        Assert.False(await db.TarifaRangos.AnyAsync(r => r.TarifaId == id));
    }

    [Fact]
    public async Task Eliminar_EnUso_SeRechazaYDiceQueLugaresLaUsan()
    {
        await LugarAsync("1C");
        await LugarAsync("1C", esDemo: true);

        var resultado = await _servicio.EliminarAsync(await IdAsync("1C"));

        Assert.Equal(MotivoNoEliminarTarifa.EnUso, resultado.Motivo);
        Assert.Equal(["Demo", "Mi casa"], resultado.LugaresQueLaUsan.Order());
    }

    [Fact]
    public async Task Eliminar_LaUltima_SeRechaza()
    {
        foreach (var nombre in new[] { "1", "1C", "1F", "DAC" })
        {
            Assert.True((await _servicio.EliminarAsync(await IdAsync(nombre))).Eliminada);
        }

        var resultado = await _servicio.EliminarAsync(await IdAsync("PDBT"));

        Assert.Equal(MotivoNoEliminarTarifa.EsLaUltima, resultado.Motivo);
    }

    [Fact]
    public async Task ListarConUso_CuentaRangosYLugares()
    {
        await LugarAsync("1C");

        var lista = await _servicio.ListarConUsoAsync();

        var unoC = lista.Single(t => t.Nombre == "1C");
        Assert.Equal(3, unoC.NumeroDeRangos);
        Assert.True(unoC.EnUso);
        Assert.False(lista.Single(t => t.Nombre == "1F").EnUso);
    }

    [Fact]
    public async Task Asignar_CambiaLaTarifaYLaDaPorConfirmada()
    {
        var lugar = await LugarAsync("1", porConfirmar: true);

        Assert.True(await _servicio.AsignarALugarAsync(lugar, await IdAsync("1C")));

        await using var db = _fabrica.CreateDbContext();
        var guardado = await db.Lugares.SingleAsync(l => l.Id == lugar);
        Assert.Equal(await IdAsync("1C"), guardado.TarifaId);
        Assert.False(guardado.TarifaPorConfirmar);
    }

    [Fact]
    public async Task TarifaEditada_ElResumenUsaLosPreciosNuevosSinCache()
    {
        var calculadora = new TarifaCalculator();
        await new DatosDemoService(_fabrica, calculadora).CargarAsync(Hoy);
        var resumenes = new ResumenDashboardService(
            _fabrica, new ProyeccionService(calculadora), new DacService(), new PresupuestoInversoService(calculadora));
        var antes = await resumenes.ObtenerAsync(Hoy);

        var editable = await _servicio.ObtenerParaEditarAsync(await IdAsync("1C"));
        var rangos = editable!.Rangos.ToList();
        rangos[^1] = rangos[^1] with { PrecioPorKwh = rangos[^1].PrecioPorKwh + 1m };
        await _servicio.GuardarAsync(editable with { Rangos = rangos });
        var despues = await resumenes.ObtenerAsync(Hoy);

        // El excedente sube $1 por kWh: el desglose del último rango sube exactamente sus kWh en pesos.
        var excedenteAntes = antes!.DesgloseProyectado!.Desglose[^1];
        var excedenteDespues = despues!.DesgloseProyectado!.Desglose[^1];
        Assert.Equal(excedenteAntes.Kwh, excedenteDespues.Kwh);
        Assert.Equal(excedenteAntes.Subtotal + excedenteAntes.Kwh, excedenteDespues.Subtotal);
        Assert.Equal(antes.MontoProyectado + excedenteAntes.Kwh, despues.MontoProyectado);
    }

    /// <summary>
    /// Falla después de insertar un rango, para comprobar que la transacción deshace todo.
    /// </summary>
    private sealed class FallaAlInsertarRangos : DbCommandInterceptor
    {
        // Se activa después de sembrar las tarifas de ejemplo.
        public bool Activa { get; set; }

        private void Revisar(DbCommand comando)
        {
            if (Activa && comando.CommandText.Contains("INSERT INTO \"TarifaRangos\"", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Falla simulada al guardar los rangos.");
            }
        }

        public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        {
            Revisar(command);
            return result;
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            Revisar(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Revisar(command);
            return ValueTask.FromResult(result);
        }
    }
}
