using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9g: unidades de un aparato típico que se usan distinto (3 aires con 8, 4 y 2 horas). Sin migración: cada
/// unidad es un Aparato de cantidad 1 llamado "{aparato} · {apodo}"; con las mismas horas, un solo Aparato de
/// cantidad N (como en la 9d).
/// </summary>
public sealed class UnidadesAparatoTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);
    private const string Aire = "Aire acondicionado";

    private FabricaDePrueba _fabrica = null!;
    private AparatoService _servicio = null!;
    private readonly TarifaCalculator _calculadora = new();

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new AparatoService(_fabrica, _calculadora);
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private PlantillaAparato Plantilla(string clave) => _servicio.PlantillasDeReferencia().Single(p => p.Clave == clave);

    private IEnumerable<PlantillaConNombre> ConNombres()
        => _servicio.PlantillasDeReferencia().Select(p => new PlantillaConNombre(p, p.Clave switch
        {
            "AireAcondicionado" => Aire,
            "Refrigerador" => "Refrigerador",
            _ => p.Clave,
        }, []));

    // Lugar mensual con la tarifa de tres tramos (0–100 a $1, 100–200 a $2, 200+ a $4) y un recibo de 300 kWh.
    private async Task<int> LugarAsync()
    {
        await using var db = _fabrica.CreateDbContext();
        var lugar = new Lugar
        {
            Nombre = "Mi casa",
            Tipo = TipoLugar.Casa,
            Tarifa = TarifasDePrueba.TresTramos(),
            DiaCorte = 1,
            MesesPorPeriodo = 1,
            Periodos = [new Periodo { Inicio = new(2026, 9, 1), Fin = new(2026, 9, 30), KwhTotales = 300m }],
        };
        db.Lugares.Add(lugar);
        await db.SaveChangesAsync();
        return lugar.Id;
    }

    private async Task<List<Aparato>> AparatosAsync(int lugarId)
    {
        await using var db = _fabrica.CreateDbContext();
        return await db.Aparatos.Where(a => a.LugarId == lugarId).OrderBy(a => a.Id).ToListAsync();
    }

    private AparatoElegido TresAires(IReadOnlyList<int?>? ids = null, IReadOnlyList<int>? otros = null)
        => new(Plantilla("AireAcondicionado"), Aire, 8m, 3, null, otros,
        [
            new UnidadElegida(ids?[0], "Sala", 8m),
            new UnidadElegida(ids?[1], "Recámara 1", 4m),
            new UnidadElegida(ids?[2], "Recámara 2", 2m),
        ]);

    // ---- Nombres de las unidades ----

    [Fact]
    public void NombreUnidad_ConApodoSinApodoYRecortado()
    {
        Assert.Equal(" · ", AparatosTipicos.SeparadorUnidad);
        Assert.Equal("Aire acondicionado · Sala", AparatosTipicos.NombreUnidad(Aire, "  Sala ", 1));
        Assert.Equal("Aire acondicionado · 2", AparatosTipicos.NombreUnidad(Aire, "", 2));
        // El nombre de un aparato admite hasta 40 caracteres: el apodo se recorta para que quepa.
        var largo = AparatosTipicos.NombreUnidad(Aire, new string('x', 50), 1);
        Assert.Equal(AparatoValidador.LargoMaximoNombre, largo.Length);
        Assert.StartsWith("Aire acondicionado · xxx", largo);
    }

    // ---- Guardar unidades con la cuenta a mano (D2, D3) ----

    [Fact]
    public async Task TresAires_CadaUnoDistinto_UnaUnidadPorAparato()
    {
        // Cada aire: 1,100 W × horas × 7 días × 52/12 × 0.9.
        // 8 h: 1.1 × 8 × 7 × 4.3333 × 0.9 = 240.24 kWh; 4 h: 120.12; 2 h: 60.06. El grupo: 420.42 kWh al mes.
        var lugarId = await LugarAsync();

        var resultado = await _servicio.GuardarElegidosAsync(lugarId, [TresAires()], []);

        Assert.True(resultado.Guardado);
        var aparatos = await AparatosAsync(lugarId);
        Assert.Equal(["Aire acondicionado · Sala", "Aire acondicionado · Recámara 1", "Aire acondicionado · Recámara 2"], aparatos.Select(a => a.Nombre));
        Assert.All(aparatos, a => Assert.Equal((1, 1_100m, 7, 0.9m), (a.Cantidad, a.Watts, a.DiasPorSemana, a.CicloDeTrabajo)));
        Assert.Equal([240.24m, 120.12m, 60.06m], aparatos.Select(a => Math.Round(ConsumoAparato.KwhPorMes(a), 2)));
        Assert.Equal(420.42m, Math.Round(aparatos.Sum(ConsumoAparato.KwhPorMes), 2));
    }

    [Fact]
    public async Task CostoDelGrupo_NoEsLaSumaDeLosCostos()
    {
        // Recibo de 300 kWh: Total(300) = 100 + 200 + 100 × 4 = $700.
        // Cada uno, contra 300 kWh:
        //   8 h: Total(300 − 240.24 = 59.76) = $59.76 → 700 − 59.76 = $640.24.
        //   4 h: Total(179.88) = 100 + 79.88 × 2 = $259.76 → $440.24.
        //   2 h: Total(239.94) = 100 + 200 + 39.94 × 4 = $459.76 → $240.24.
        //   Suma: $1,320.72.
        // El grupo (420.42 kWh) se limita al consumo (300): Total(0) = $0 → $700. No es la suma: por los tramos y el límite.
        var lugarId = await LugarAsync();
        await _servicio.GuardarElegidosAsync(lugarId, [TresAires()], []);

        var costos = await _servicio.ObtenerCostosAsync(lugarId, Hoy);
        var individuales = costos.Costos.Filas.Select(f => Math.Round(f.CostoMensual!.Value, 2)).ToList();
        var grupo = Assert.Single(_servicio.Agrupar(costos));

        Assert.Equal([640.24m, 440.24m, 240.24m], individuales);
        Assert.Equal(1_320.72m, individuales.Sum());
        Assert.Equal(("Aire acondicionado · 3", 3), (grupo.Nombre, grupo.Registros));
        Assert.Equal(420.42m, Math.Round(grupo.KwhPorMes, 2));
        Assert.Equal(700m, Math.Round(grupo.CostoMensual!.Value, 2));
    }

    [Fact]
    public void Agrupar_UnSoloRegistro_ConservaSuNombreYSuCifra()
    {
        // La demo y un refrigerador solo: el agrupamiento no los cambia.
        var filas = new[]
        {
            new CostoDeAparato(1, "Refrigerador", CategoriaAparato.Refrigeracion, 91m, 364m),
            new CostoDeAparato(2, "Iluminación", CategoriaAparato.Iluminacion, 30m, 120m),
        };

        var grupos = GruposDeAparatos.Agrupar(filas, _calculadora, TarifasDePrueba.TresTramos(), 1, 300m);

        Assert.Equal(["Refrigerador", "Iluminación"], grupos.Select(g => g.Nombre));
        Assert.Equal([364m, 120m], grupos.Select(g => g.CostoMensual));
    }

    [Fact]
    public void Agrupar_SinPesos_OrdenaPorConsumo()
    {
        var filas = new[]
        {
            new CostoDeAparato(1, "Ventilador · 1", CategoriaAparato.Climatizacion, 10m, null),
            new CostoDeAparato(2, "Refrigerador", CategoriaAparato.Refrigeracion, 15m, null),
            new CostoDeAparato(3, "Ventilador · 2", CategoriaAparato.Climatizacion, 10m, null),
        };

        var grupos = GruposDeAparatos.Agrupar(filas, _calculadora, null, 1, null);

        Assert.Equal(["Ventilador · 2", "Refrigerador"], grupos.Select(g => g.Nombre));
        Assert.All(grupos, g => Assert.Null(g.CostoMensual));
    }

    // ---- Emparejar por nombre exacto y por prefijo ----

    [Fact]
    public void Emparejar_PorNombreExactoYPorPrefijo_SinTocarLosPropios()
    {
        var aparatos = new[]
        {
            new Aparato { Id = 1, Nombre = "Aire acondicionado · Sala" },
            new Aparato { Id = 2, Nombre = "Refrigerador" },
            new Aparato { Id = 3, Nombre = "aire acondicionado · Recámara" },
            // Nombre propio con el separador que no empieza con ninguna plantilla.
            new Aparato { Id = 4, Nombre = "Horno · grande" },
            // Empieza igual pero sin el separador: es un nombre propio.
            new Aparato { Id = 5, Nombre = "Aire acondicionado viejo" },
        };

        var emparejados = AparatosTipicos.Emparejar(ConNombres(), aparatos);

        var aire = emparejados.Single(e => e.Nombre == Aire);
        Assert.Equal([1, 3], aire.Registros.Select(a => a.Id));
        Assert.Equal(1, aire.Existente!.Id);
        Assert.Equal([2], emparejados.Single(e => e.Nombre == "Refrigerador").Registros.Select(a => a.Id));
        Assert.DoesNotContain(emparejados.SelectMany(e => e.Registros), a => a.Id is 4 or 5);
    }

    // ---- Convertir de "mismas horas" a "distintas" y de vuelta ----

    [Fact]
    public async Task DeMismasHorasADistintas_ConservaLaPotenciaAjustadaSinDuplicar()
    {
        var lugarId = await LugarAsync();
        await _servicio.GuardarElegidosAsync(lugarId, [new AparatoElegido(Plantilla("AireAcondicionado"), Aire, 8m, 3, null)], []);
        var unico = Assert.Single(await AparatosAsync(lugarId));
        // La persona ajustó la potencia en el editor completo: 900 W, 5 días y ciclo 0.7.
        await _servicio.GuardarAsync(new AparatoEditable(unico.Id, lugarId, unico.Nombre, unico.Categoria, 3, 900m, 8m, 5, 0.7m));

        await _servicio.GuardarElegidosAsync(lugarId, [TresAires(ids: [unico.Id, null, null])], []);

        var aparatos = await AparatosAsync(lugarId);
        Assert.Equal(3, aparatos.Count);
        Assert.Equal(unico.Id, aparatos[0].Id);
        Assert.All(aparatos, a => Assert.Equal((1, 900m, 5, 0.7m), (a.Cantidad, a.Watts, a.DiasPorSemana, a.CicloDeTrabajo)));
        Assert.Equal([8m, 4m, 2m], aparatos.Select(a => a.HorasPorDia));
    }

    [Fact]
    public async Task DeDistintasAMismasHoras_LasHorasDelPrimeroYLaSumaDeLasCantidades()
    {
        // Precisión (1): al volver a "mismas horas", el aparato que queda toma las horas del PRIMER registro (8) y
        // la cantidad es la suma de las cantidades (3). Conserva la potencia ajustada del primero.
        var lugarId = await LugarAsync();
        await _servicio.GuardarElegidosAsync(lugarId, [TresAires()], []);
        var registros = await AparatosAsync(lugarId);
        await _servicio.GuardarAsync(new AparatoEditable(
            registros[0].Id, lugarId, registros[0].Nombre, registros[0].Categoria, 1, 950m, 8m, 7, 0.9m));
        registros = await AparatosAsync(lugarId);

        var (horas, cantidad) = AparatosTipicos.MismasHoras(registros);
        var elegido = new AparatoElegido(Plantilla("AireAcondicionado"), Aire, horas, cantidad, registros[0].Id, [registros[1].Id, registros[2].Id]);
        await _servicio.GuardarElegidosAsync(lugarId, [elegido], []);

        Assert.Equal((8m, 3), (horas, cantidad));
        var queda = Assert.Single(await AparatosAsync(lugarId));
        Assert.Equal((registros[0].Id, Aire, 3, 8m, 950m), (queda.Id, queda.Nombre, queda.Cantidad, queda.HorasPorDia, queda.Watts));
    }

    [Fact]
    public async Task OtraVezConLasMismasUnidades_ActualizaSinDuplicar()
    {
        var lugarId = await LugarAsync();
        await _servicio.GuardarElegidosAsync(lugarId, [TresAires()], []);
        var ids = (await AparatosAsync(lugarId)).Select(a => (int?)a.Id).ToList();

        var otra = TresAires(ids) with
        {
            Unidades = [new UnidadElegida(ids[0], "Sala", 10m), new UnidadElegida(ids[1], "Recámara 1", 4m), new UnidadElegida(ids[2], "Recámara 2", 2m)],
        };
        await _servicio.GuardarElegidosAsync(lugarId, [otra], []);

        var aparatos = await AparatosAsync(lugarId);
        Assert.Equal(ids, aparatos.Select(a => (int?)a.Id));
        Assert.Equal([10m, 4m, 2m], aparatos.Select(a => a.HorasPorDia));
    }

    [Fact]
    public async Task UnaUnidadMenos_SeElimina()
    {
        var lugarId = await LugarAsync();
        await _servicio.GuardarElegidosAsync(lugarId, [TresAires()], []);
        var ids = (await AparatosAsync(lugarId)).Select(a => a.Id).ToList();

        var dos = new AparatoElegido(Plantilla("AireAcondicionado"), Aire, 8m, 2, null, ids,
            [new UnidadElegida(ids[0], "Sala", 8m), new UnidadElegida(ids[1], "Recámara 1", 4m)]);
        await _servicio.GuardarElegidosAsync(lugarId, [dos], []);

        Assert.Equal(["Aire acondicionado · Sala", "Aire acondicionado · Recámara 1"], (await AparatosAsync(lugarId)).Select(a => a.Nombre));
    }

    [Fact]
    public async Task UnAparatoPropioConElSeparador_QuedaIntacto()
    {
        var lugarId = await LugarAsync();
        var propio = (await _servicio.GuardarAsync(new AparatoEditable(
            null, lugarId, "Horno · grande", CategoriaAparato.Cocina, 1, 1_800m, 2m, 3, 1m))).Id!.Value;

        await _servicio.GuardarElegidosAsync(lugarId, [TresAires()], []);
        var aparatos = await AparatosAsync(lugarId);
        var aire = AparatosTipicos.Emparejar(ConNombres(), aparatos).Single(e => e.Nombre == Aire);
        await _servicio.GuardarElegidosAsync(lugarId, [], aire.Registros.Select(a => a.Id).ToList());

        var queda = Assert.Single(await AparatosAsync(lugarId));
        Assert.Equal((propio, "Horno · grande", 1_800m), (queda.Id, queda.Nombre, queda.Watts));
    }

    // ---- Qué se reemplaza (para la confirmación) ----

    [Fact]
    public void Cambios_DeDistintasAMismasHoras()
    {
        var registros = new List<Aparato>
        {
            new() { Id = 1, Nombre = "Aire acondicionado · Sala", Cantidad = 1, HorasPorDia = 8m },
            new() { Id = 2, Nombre = "Aire acondicionado · Recámara 1", Cantidad = 1, HorasPorDia = 4m },
            new() { Id = 3, Nombre = "Aire acondicionado · Recámara 2", Cantidad = 1, HorasPorDia = 2m },
        };
        var elegido = new AparatoElegido(Plantilla("AireAcondicionado"), Aire, 8m, 3, 1, [2, 3]);

        var cambio = AparatosTipicos.Cambios(elegido, registros);

        Assert.NotNull(cambio);
        Assert.Equal(
            (TipoCambioAparato.AUnoConMismasHoras, Aire, 3, 1, 8m, 3),
            (cambio.Tipo, cambio.Nombre, cambio.RegistrosAntes, cambio.RegistrosDespues, cambio.HorasPorDia, cambio.Cantidad));
        Assert.Empty(cambio.Quitadas);
    }

    [Fact]
    public void Cambios_DeMismasHorasADistintas_YUnidadesQueSeQuitan()
    {
        var unico = new List<Aparato> { new() { Id = 1, Nombre = Aire, Cantidad = 3, HorasPorDia = 8m } };
        var aDistintas = AparatosTipicos.Cambios(TresAires(ids: [1, null, null]), unico);
        Assert.Equal((TipoCambioAparato.AUnidades, 1, 3), (aDistintas!.Tipo, aDistintas.RegistrosAntes, aDistintas.RegistrosDespues));

        var tres = new List<Aparato>
        {
            new() { Id = 1, Nombre = "Aire acondicionado · Sala", Cantidad = 1 },
            new() { Id = 2, Nombre = "Aire acondicionado · Recámara 1", Cantidad = 1 },
            new() { Id = 3, Nombre = "Aire acondicionado · Recámara 2", Cantidad = 1 },
        };
        var dos = new AparatoElegido(Plantilla("AireAcondicionado"), Aire, 8m, 2, null, [1, 2, 3],
            [new UnidadElegida(1, "Sala", 8m), new UnidadElegida(2, "Recámara 1", 4m)]);
        var menos = AparatosTipicos.Cambios(dos, tres);
        Assert.Equal(TipoCambioAparato.SoloQuitar, menos!.Tipo);
        Assert.Equal(["Aire acondicionado · Recámara 2"], menos.Quitadas);

        // Sin cambio de forma ni unidades de menos: no hay nada que confirmar.
        Assert.Null(AparatosTipicos.Cambios(TresAires(ids: [1, 2, 3]), tres));
        Assert.Null(AparatosTipicos.Cambios(new AparatoElegido(Plantilla("AireAcondicionado"), Aire, 6m, 3, 1), unico));
    }
}
