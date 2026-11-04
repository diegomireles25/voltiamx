using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class RepartoServiceTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 4);

    private sealed class Reloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    /// <summary>
    /// Lugar con dos periodos cerrados: sep-oct (400 kWh, monto 1,200, recibo 1,450) y jul-ago (300 kWh, 900, sin recibo).
    /// </summary>
    private static async Task<int> LugarAsync(FabricaDePrueba fabrica, string nombre = "Edificio", bool demo = false)
    {
        await using var db = fabrica.CreateDbContext();
        var lugar = new Lugar
        {
            Nombre = nombre,
            Tipo = TipoLugar.Propiedad,
            TarifaId = (await db.Tarifas.FirstAsync()).Id,
            DiaCorte = 1,
            EsDemo = demo,
            Periodos =
            [
                new() { Inicio = new(2026, 7, 2), Fin = new(2026, 9, 1), KwhTotales = 300m, Monto = 900m },
                new() { Inicio = new(2026, 9, 2), Fin = new(2026, 11, 1), KwhTotales = 400m, Monto = 1_200m, MontoRecibo = 1_450m },
            ],
        };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private static async Task<int[]> InquilinosAsync(InquilinoService servicio, int lugarId, params string[] nombres)
    {
        var ids = new List<int>();
        foreach (var nombre in nombres)
        {
            var r = await servicio.GuardarAsync(new InquilinoEditable(null, lugarId, nombre), Hoy);
            Assert.Empty(r.Errores);
            ids.Add(r.Id!.Value);
        }

        return [.. ids];
    }

    private static EntradaReparto Entrada(int lugarId, int[] ids, decimal total = 3_000m, ModoReparto modo = ModoReparto.SoloMedido,
        bool ultimo = true, OrigenTotal origen = OrigenTotal.Capturado, decimal[]? kwh = null, decimal anterior = 1_000m)
    {
        kwh ??= [100m, 100m, 150m];
        var (inicio, fin) = ultimo ? (new DateOnly(2026, 9, 2), new DateOnly(2026, 11, 1)) : (new DateOnly(2026, 7, 2), new DateOnly(2026, 9, 1));
        return new EntradaReparto(lugarId, inicio, fin, total, origen, modo,
            ids.Select((id, i) => new LecturaInquilino(id, anterior, anterior + kwh[i])).ToList());
    }

    private static async Task<(FabricaDePrueba Fabrica, RepartoService Repartos, InquilinoService Inquilinos)> CrearAsync(params IInterceptor[] interceptores)
    {
        var fabrica = await FabricaDePrueba.CrearAsync(interceptores);
        return (fabrica, new RepartoService(fabrica, new Reloj()), new InquilinoService(fabrica));
    }

    [Fact]
    public async Task Preparar_UsaElPeriodoMasRecienteYElMontoDelRecibo()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        await InquilinosAsync(inquilinos, lugar, "Depto 2", "Depto 1");

        var p = await repartos.PrepararAsync(lugar);

        Assert.Equal(new DateOnly(2026, 11, 1), p.Periodo!.Fin);
        Assert.Equal([new DateOnly(2026, 11, 1), new DateOnly(2026, 9, 1)], p.Periodos.Select(x => x.Fin));
        Assert.Equal((1_450m, OrigenTotal.MontoRecibo), (p.Total, p.Origen));
        Assert.Equal(["Depto 1", "Depto 2"], p.Filas.Select(f => f.Nombre));
        Assert.All(p.Filas, f => Assert.Null(f.LecturaAnterior));
    }

    [Fact]
    public async Task Preparar_SinMontoDelRecibo_UsaElCalculado()
    {
        var (fabrica, repartos, _) = await CrearAsync();
        using var __ = fabrica;
        var lugar = await LugarAsync(fabrica);

        var p = await repartos.PrepararAsync(lugar, new DateOnly(2026, 9, 1));

        Assert.Equal((900m, OrigenTotal.MontoCalculado), (p.Total, p.Origen));
    }

    [Fact]
    public async Task Preparar_LaAnteriorSalDelUltimoRepartoPrevio()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        Assert.Equal(EstadoGuardarReparto.Guardado, (await repartos.GuardarAsync(Entrada(lugar, ids, ultimo: false), false)).Estado);
        var nuevo = (await InquilinosAsync(inquilinos, lugar, "D"))[0];

        var p = await repartos.PrepararAsync(lugar);

        // jul-ago: 1000 -> 1100, 1100, 1150. D no tiene repartos: su anterior es obligatoria.
        Assert.Equal([1_100m, 1_100m, 1_150m, null], p.Filas.Select(f => f.LecturaAnterior));
        Assert.All(p.Filas, f => Assert.Null(f.LecturaActual));
        Assert.Equal(nuevo, p.Filas[3].InquilinoId);
        Assert.True(p.Periodos.Single(x => x.Fin == new DateOnly(2026, 9, 1)).TieneReparto);
        Assert.False(p.Periodos.Single(x => x.Fin == new DateOnly(2026, 11, 1)).TieneReparto);
    }

    [Fact]
    public async Task Preparar_PeriodoConReparto_PrecargaSusPropiasLecturas()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        await repartos.GuardarAsync(Entrada(lugar, ids), false);

        var p = await repartos.PrepararAsync(lugar);

        Assert.Equal([1_000m, 1_000m, 1_000m], p.Filas.Select(f => f.LecturaAnterior));
        Assert.Equal([1_100m, 1_100m, 1_150m], p.Filas.Select(f => f.LecturaActual));
    }

    [Fact]
    public async Task Guardar_CopiaFijaConCuotasYOrigen()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");

        var r = await repartos.GuardarAsync(Entrada(lugar, ids), false);
        var guardado = await repartos.ObtenerAsync(r.Id!.Value);

        // SoloMedido: 3000 x {100, 100, 150} / max(400, 350) = 750, 750, 1125; absorbe 375.
        Assert.Equal([750m, 750m, 1_125m], guardado!.Lineas.Select(l => l.Cuota));
        Assert.Equal(["A", "B", "C"], guardado.Lineas.Select(l => l.Nombre));
        Assert.Equal((3_000m, OrigenTotal.Capturado, 400m), (guardado.TotalRepartido, guardado.Origen, guardado.KwhPrincipal));
        Assert.Equal((2_625m, 375m, 350m, 7.5m), (guardado.TotalCobrado, guardado.AbsorbePropietario, guardado.KwhRepartidos, guardado.PrecioPromedioKwh));
        Assert.Equal(new DateTime(2026, 11, 4, 12, 0, 0), guardado.FechaCreacion);
        Assert.Empty(guardado.Avisos);
    }

    [Fact]
    public async Task Guardar_LaCopiaNoCambiaSiDespuesCambiaElPeriodoOElNombre()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        var id = (await repartos.GuardarAsync(Entrada(lugar, ids), false)).Id!.Value;

        await using (var db = fabrica.CreateDbContext())
        {
            var periodo = await db.Periodos.SingleAsync(p => p.LugarId == lugar && p.Fin == new DateOnly(2026, 11, 1));
            periodo.Monto = 9_999m;
            periodo.KwhTotales = 999m;
            await db.SaveChangesAsync();
        }

        await inquilinos.GuardarAsync(new InquilinoEditable(ids[0], lugar, "A renombrado"), Hoy);

        var guardado = await repartos.ObtenerAsync(id);
        Assert.Equal([750m, 750m, 1_125m], guardado!.Lineas.Select(l => l.Cuota));
        Assert.Equal(400m, guardado.KwhPrincipal);
        Assert.Equal("A", guardado.Lineas[0].Nombre);
    }

    [Fact]
    public async Task Guardar_ConAvisosQueBloquean_NoGuarda()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A");
        var entrada = new EntradaReparto(lugar, new(2026, 9, 2), new(2026, 11, 1), 3_000m, OrigenTotal.Capturado, ModoReparto.SoloMedido,
            [new LecturaInquilino(ids[0], 500m, 400m)]);

        var r = await repartos.GuardarAsync(entrada, false);

        Assert.Equal(EstadoGuardarReparto.Invalido, r.Estado);
        Assert.Contains(AvisoReparto.LecturaMenorQueAnterior, r.Avisos);
        Assert.Empty(await repartos.ListarAsync(lugar));
    }

    [Fact]
    public async Task Guardar_PeriodoConReparto_PideReemplazoYSoloAsiLoReemplaza()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        var primero = (await repartos.GuardarAsync(Entrada(lugar, ids), false)).Id;

        var sinPermiso = await repartos.GuardarAsync(Entrada(lugar, ids, modo: ModoReparto.ProporcionalAlMedido), false);
        Assert.Equal(EstadoGuardarReparto.YaExiste, sinPermiso.Estado);
        Assert.Equal(primero, sinPermiso.Id);

        var reemplazo = await repartos.GuardarAsync(Entrada(lugar, ids, modo: ModoReparto.ProporcionalAlMedido), true);
        Assert.Equal(EstadoGuardarReparto.Guardado, reemplazo.Estado);
        var lista = Assert.Single(await repartos.ListarAsync(lugar));
        Assert.Equal(3_000m, lista.TotalCobrado);
        Assert.Null(await repartos.ObtenerAsync(primero!.Value));
        await using var db = fabrica.CreateDbContext();
        Assert.Equal(3, await db.RepartoInquilinos.CountAsync());
    }

    [Fact]
    public async Task Guardar_SiFallaElReemplazo_QuedaElRepartoAnterior()
    {
        var falla = new FallaAlGuardarReparto();
        var (fabrica, repartos, inquilinos) = await CrearAsync(falla);
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        var primero = (await repartos.GuardarAsync(Entrada(lugar, ids), false)).Id!.Value;

        falla.Activa = true;
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repartos.GuardarAsync(Entrada(lugar, ids, modo: ModoReparto.ProporcionalAlMedido), true));

        var guardado = await repartos.ObtenerAsync(primero);
        Assert.NotNull(guardado);
        Assert.Equal([750m, 750m, 1_125m], guardado.Lineas.Select(l => l.Cuota));
        await using var db = fabrica.CreateDbContext();
        Assert.Equal(1, await db.Repartos.CountAsync());
        Assert.Equal(3, await db.RepartoInquilinos.CountAsync());
    }

    [Fact]
    public async Task Calcular_UsaLosKwhDelPeriodoEnLaBase()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");

        var c = await repartos.CalcularAsync(Entrada(lugar, ids, modo: ModoReparto.ProporcionalAlMedido));

        Assert.Equal(400m, c.KwhPrincipal);
        Assert.Equal([857.14m, 857.14m, 1_285.72m], c.Resultado.Cuotas);
    }

    [Fact]
    public async Task OtroLugar_NoInterfiere()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var otro = await LugarAsync(fabrica, "Otro");
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        await InquilinosAsync(inquilinos, otro, "A");
        await repartos.GuardarAsync(Entrada(lugar, ids), false);

        Assert.Empty(await repartos.ListarAsync(otro));
        Assert.False((await repartos.PrepararAsync(otro)).Periodos[0].TieneReparto);
        Assert.Single((await repartos.PrepararAsync(otro)).Filas);
        Assert.Single(await repartos.ListarAsync(lugar));
    }

    [Fact]
    public async Task BorrarElLugar_BorraInquilinosYRepartosSinErrores()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var otro = await LugarAsync(fabrica, "Otro");
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        var idsOtro = await InquilinosAsync(inquilinos, otro, "X", "Y", "Z");
        await repartos.GuardarAsync(Entrada(lugar, ids), false);
        await repartos.GuardarAsync(Entrada(otro, idsOtro), false);

        await using var db = fabrica.CreateDbContext();
        await db.Lugares.Where(l => l.Id == lugar).ExecuteDeleteAsync();

        Assert.Equal(3, await db.Inquilinos.CountAsync());
        Assert.Equal(1, await db.Repartos.CountAsync());
        Assert.Equal(3, await db.RepartoInquilinos.CountAsync());
        Assert.All(await db.Inquilinos.ToListAsync(), i => Assert.Equal(otro, i.LugarId));
    }

    [Fact]
    public async Task Eliminar_BorraElRepartoYSusLineas()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        var id = (await repartos.GuardarAsync(Entrada(lugar, ids), false)).Id!.Value;

        Assert.True(await repartos.EliminarAsync(id));

        await using var db = fabrica.CreateDbContext();
        Assert.Equal(0, await db.RepartoInquilinos.CountAsync());
        Assert.Equal(3, await db.Inquilinos.CountAsync());
    }

    [Fact]
    public async Task Inquilino_ConRepartos_SoloSeDaDeBajaYSigueEnElHistorial()
    {
        var (fabrica, repartos, inquilinos) = await CrearAsync();
        using var _ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A", "B", "C");
        var id = (await repartos.GuardarAsync(Entrada(lugar, ids), false)).Id!.Value;

        Assert.Equal(ResultadoEliminarInquilino.TieneRepartos, await inquilinos.EliminarAsync(ids[0]));
        await inquilinos.DarDeBajaAsync(ids[0]);

        var lista = await inquilinos.ListarAsync(lugar);
        var a = lista.Single(i => i.Id == ids[0]);
        Assert.False(a.Activo);
        Assert.True(a.TieneRepartos);
        Assert.Equal(ids[0], lista[^1].Id);
        Assert.Equal("A", (await repartos.ObtenerAsync(id))!.Lineas[0].Nombre);
        Assert.DoesNotContain((await repartos.PrepararAsync(lugar)).Filas, f => f.InquilinoId == ids[0]);

        Assert.Empty(await inquilinos.ReactivarAsync(ids[0]));
        Assert.True((await inquilinos.ListarAsync(lugar)).Single(i => i.Id == ids[0]).Activo);
    }

    [Fact]
    public async Task Inquilino_SinRepartos_SeElimina()
    {
        var (fabrica, _, inquilinos) = await CrearAsync();
        using var __ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, "A");

        Assert.Equal(ResultadoEliminarInquilino.Eliminado, await inquilinos.EliminarAsync(ids[0]));
        Assert.Equal(ResultadoEliminarInquilino.NoExiste, await inquilinos.EliminarAsync(ids[0]));
        Assert.Empty(await inquilinos.ListarAsync(lugar));
    }

    [Fact]
    public async Task Inquilino_Validaciones()
    {
        var (fabrica, _, inquilinos) = await CrearAsync();
        using var __ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var otro = await LugarAsync(fabrica, "Otro");
        var ids = await InquilinosAsync(inquilinos, lugar, "Depto 1");

        Task<ResultadoGuardarInquilino> Guardar(string nombre, int? id = null, int? enLugar = null)
            => inquilinos.GuardarAsync(new InquilinoEditable(id, enLugar ?? lugar, nombre), Hoy);

        Assert.Equal([CodigoErrorInquilino.NombreVacio], (await Guardar("   ")).Errores);
        Assert.Equal([CodigoErrorInquilino.NombreMuyLargo], (await Guardar(new string('x', 41))).Errores);
        Assert.Equal([CodigoErrorInquilino.NombreRepetido], (await Guardar("  DEPTO 1 ")).Errores);
        Assert.Empty((await Guardar("Depto 1", enLugar: otro)).Errores);
        Assert.Empty((await Guardar("depto 1", ids[0])).Errores);
        Assert.Equal("depto 1", (await inquilinos.ListarAsync(lugar)).Single().Nombre);
        Assert.Empty((await Guardar(new string('x', 40))).Errores);
    }

    [Fact]
    public async Task Inquilino_MaximoDeTreintaActivos()
    {
        var (fabrica, _, inquilinos) = await CrearAsync();
        using var __ = fabrica;
        var lugar = await LugarAsync(fabrica);
        var ids = await InquilinosAsync(inquilinos, lugar, [.. Enumerable.Range(1, 30).Select(n => $"Depto {n}")]);

        Assert.Equal([CodigoErrorInquilino.MaximoAlcanzado], (await inquilinos.GuardarAsync(new InquilinoEditable(null, lugar, "Depto 31"), Hoy)).Errores);

        // Con uno de baja cabe otro; al reactivar se vuelve a pasar del máximo.
        await inquilinos.DarDeBajaAsync(ids[0]);
        Assert.Empty((await inquilinos.GuardarAsync(new InquilinoEditable(null, lugar, "Depto 31"), Hoy)).Errores);
        Assert.Equal([CodigoErrorInquilino.MaximoAlcanzado], await inquilinos.ReactivarAsync(ids[0]));
        Assert.Empty((await inquilinos.GuardarAsync(new InquilinoEditable(ids[0], lugar, "Depto 1 bis"), Hoy)).Errores);
    }

    /// <summary>
    /// Falla al guardar un reparto nuevo cuando está activa (simula un error después de borrar el anterior).
    /// </summary>
    private sealed class FallaAlGuardarReparto : SaveChangesInterceptor
    {
        public bool Activa { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Activa && eventData.Context!.ChangeTracker.Entries<Reparto>().Any(e => e.State == EntityState.Added))
            {
                throw new InvalidOperationException("fallo simulado");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
