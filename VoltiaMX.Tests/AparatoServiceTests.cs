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

    // 1 kW × 24 h × 7 días × 52/12 = 728 kWh al mes; con ciclo 0.25 → 182 y con 0.125 → 91.
    private static AparatoEditable DeUnKilowatt(int lugarId, string nombre, decimal ciclo) => new(
        null, lugarId, nombre, CategoriaAparato.Otro, 1, 1_000m, 24m, 7, ciclo);

    private async Task AgregarPeriodoAsync(int lugarId, decimal kwh)
    {
        await using var db = _fabrica.CreateDbContext();
        db.Periodos.Add(new Periodo { LugarId = lugarId, Inicio = new(2026, 7, 2), Fin = new(2026, 9, 1), KwhTotales = kwh });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ObtenerRankingAsync_CuentaAMano_ContraElUltimoPeriodoCerrado()
    {
        // Periodo bimestral de 728 kWh = 364 al mes: 182 / 364 = 50 %, 91 / 364 = 25 %, 25 % sin identificar.
        var lugarId = await CrearLugarAsync("Mi casa");
        await AgregarPeriodoAsync(lugarId, 728m);
        await _servicio.GuardarAsync(DeUnKilowatt(lugarId, "Lavadora", 0.125m));
        await _servicio.GuardarAsync(DeUnKilowatt(lugarId, "Aire", 0.25m));

        var ranking = await _servicio.ObtenerRankingAsync(lugarId, new DateOnly(2026, 10, 19));

        Assert.Equal(EstadoRanking.Normal, ranking.Estado);
        Assert.Equal(364m, ranking.KwhMedidoPorMes);
        Assert.Equal(["Aire", "Lavadora"], ranking.Filas.Select(f => f.Nombre));
        Assert.Equal([0.5m, 0.25m], ranking.Filas.Select(f => Math.Round(f.Porcentaje!.Value, 4)));
        Assert.Equal(0.25m, Math.Round(ranking.SinIdentificar!.Value, 4));
    }

    [Fact]
    public async Task ObtenerRankingAsync_SinAparatos()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        await AgregarPeriodoAsync(lugarId, 728m);

        Assert.Equal(EstadoRanking.SinAparatos, (await _servicio.ObtenerRankingAsync(lugarId, new DateOnly(2026, 10, 19))).Estado);
    }

    [Fact]
    public async Task ObtenerRankingAsync_LugarSinLecturasNiPeriodos_SinConsumoMedido()
    {
        var lugarId = await CrearLugarAsync("Mi casa");
        await _servicio.GuardarAsync(DeUnKilowatt(lugarId, "Aire", 0.25m));

        var ranking = await _servicio.ObtenerRankingAsync(lugarId, new DateOnly(2026, 10, 19));

        Assert.Equal(EstadoRanking.SinConsumoMedido, ranking.Estado);
        Assert.Null(Assert.Single(ranking.Filas).Porcentaje);
    }

    [Fact]
    public async Task ObtenerRankingAsync_SoloCuentaLosAparatosDelLugar()
    {
        var casa = await CrearLugarAsync("Mi casa");
        var local = await CrearLugarAsync("Local");
        await AgregarPeriodoAsync(casa, 728m);
        await _servicio.GuardarAsync(DeUnKilowatt(casa, "Aire", 0.25m));
        await _servicio.GuardarAsync(DeUnKilowatt(local, "Horno", 1m));

        var ranking = await _servicio.ObtenerRankingAsync(casa, new DateOnly(2026, 10, 19));

        Assert.Equal(["Aire"], ranking.Filas.Select(f => f.Nombre));
    }
}
