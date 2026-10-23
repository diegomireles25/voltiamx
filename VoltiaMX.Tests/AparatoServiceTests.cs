using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class AparatoServiceTests : IAsyncLifetime
{
    private FabricaDePrueba _fabrica = null!;
    private AparatoService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new AparatoService(_fabrica);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> CrearLugarAsync(string nombre)
    {
        await using var db = _fabrica.CreateDbContext();
        var tarifa = await db.Tarifas.OrderBy(t => t.Id).FirstAsync();
        var lugar = new Lugar { Nombre = nombre, Tipo = TipoLugar.Casa, TarifaId = tarifa.Id, DiaCorte = 1 };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private static AparatoEditable Nuevo(int lugarId, string nombre = "Refrigerador") => new(
        null, lugarId, nombre, CategoriaAparato.Refrigeracion, 1, 250m, 24m, 7, 0.5m);

    [Fact]
    public async Task GuardarAsync_Nuevo_LoCreaYSeLista()
    {
        var lugarId = await CrearLugarAsync("Mi casa");

        var resultado = await _servicio.GuardarAsync(Nuevo(lugarId));

        Assert.Empty(resultado.Errores);
        Assert.NotNull(resultado.Id);
        var aparato = Assert.Single(await _servicio.ListarAsync(lugarId));
        Assert.Equal(resultado.Id, aparato.Id);
        Assert.Equal("Refrigerador", aparato.Nombre);
        Assert.Equal(CategoriaAparato.Refrigeracion, aparato.Categoria);
        Assert.Equal(0.5m, aparato.CicloDeTrabajo);
    }

    [Fact]
    public async Task GuardarAsync_ConId_LoEdita()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        var id = (await _servicio.GuardarAsync(Nuevo(lugarId))).Id;

        var resultado = await _servicio.GuardarAsync(Nuevo(lugarId, "  Refri cocina  ") with { Id = id, Watts = 300m, Cantidad = 2 });

        Assert.Empty(resultado.Errores);
        var aparato = Assert.Single(await _servicio.ListarAsync(lugarId));
        Assert.Equal(id, aparato.Id);
        Assert.Equal("Refri cocina", aparato.Nombre);
        Assert.Equal(300m, aparato.Watts);
        Assert.Equal(2, aparato.Cantidad);
    }

    [Fact]
    public async Task GuardarAsync_ConErrores_LosDevuelveYNoGuarda()
    {
        var lugarId = await CrearLugarAsync("Mi casa");

        var resultado = await _servicio.GuardarAsync(Nuevo(lugarId) with { Watts = 0m });

        Assert.Null(resultado.Id);
        Assert.Equal([CodigoErrorAparato.WattsFueraDeRango], resultado.Errores);
        Assert.Empty(await _servicio.ListarAsync(lugarId));
    }

    [Fact]
    public async Task GuardarAsync_EdicionConErrores_NoCambiaNada()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        var id = (await _servicio.GuardarAsync(Nuevo(lugarId))).Id;

        var resultado = await _servicio.GuardarAsync(Nuevo(lugarId) with { Id = id, HorasPorDia = 25m });

        Assert.Equal([CodigoErrorAparato.HorasFueraDeRango], resultado.Errores);
        Assert.Equal(24m, Assert.Single(await _servicio.ListarAsync(lugarId)).HorasPorDia);
    }

    [Fact]
    public async Task EliminarAsync_LoQuita()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        var id = (await _servicio.GuardarAsync(Nuevo(lugarId))).Id!.Value;
        await _servicio.GuardarAsync(Nuevo(lugarId, "Lavadora"));

        Assert.True(await _servicio.EliminarAsync(id));

        Assert.Equal(["Lavadora"], (await _servicio.ListarAsync(lugarId)).Select(a => a.Nombre));
        Assert.False(await _servicio.EliminarAsync(id));
    }

    [Fact]
    public async Task ListarAsync_OtroLugar_NoVeLosAparatosDelPrimero()
    {
        var casa = await CrearLugarAsync("Mi casa");
        var local = await CrearLugarAsync("Local");
        await _servicio.GuardarAsync(Nuevo(casa));

        Assert.Empty(await _servicio.ListarAsync(local));
        Assert.Single(await _servicio.ListarAsync(casa));
    }

    [Fact]
    public async Task ListarAsync_OrdenaPorNombre()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        await _servicio.GuardarAsync(Nuevo(lugarId, "Televisor"));
        await _servicio.GuardarAsync(Nuevo(lugarId, "Aire"));

        Assert.Equal(["Aire", "Televisor"], (await _servicio.ListarAsync(lugarId)).Select(a => a.Nombre));
    }

    [Fact]
    public async Task BorrarElLugar_BorraSusAparatosEnCascada()
    {
        var casa = await CrearLugarAsync("Mi casa");
        var local = await CrearLugarAsync("Local");
        await _servicio.GuardarAsync(Nuevo(casa));
        await _servicio.GuardarAsync(Nuevo(local));

        await using (var db = _fabrica.CreateDbContext())
        {
            await db.Lugares.Where(l => l.Id == casa).ExecuteDeleteAsync();
        }

        await using var verificacion = _fabrica.CreateDbContext();
        Assert.Equal([local], await verificacion.Aparatos.Select(a => a.LugarId).ToListAsync());
    }

    [Fact]
    public void PlantillasDeReferencia_DiezValidasYSinClavesRepetidas()
    {
        var plantillas = _servicio.PlantillasDeReferencia();

        Assert.Equal(10, plantillas.Count);
        Assert.Equal(plantillas.Count, plantillas.Select(p => p.Clave).Distinct().Count());
        foreach (var plantilla in plantillas)
        {
            var editable = new AparatoEditable(
                null, 1, plantilla.Clave, plantilla.Categoria, 1, plantilla.Watts, plantilla.HorasPorDia,
                plantilla.DiasPorSemana, plantilla.CicloDeTrabajo);
            Assert.Empty(AparatoValidador.Validar(editable));
        }
    }
}
