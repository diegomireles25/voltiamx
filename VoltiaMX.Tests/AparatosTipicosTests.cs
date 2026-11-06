using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// "Elegir mis aparatos" (Fase 9d, D1): marcar aparatos típicos y contestar solo cuántas horas al día se usan (y
/// cuántos hay, si suele haber varios). Los demás datos salen de la plantilla.
/// </summary>
public sealed class AparatosTipicosTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private FabricaDePrueba _fabrica = null!;
    private AparatoService _servicio = null!;

    public async Task InitializeAsync()
    {
        _fabrica = await FabricaDePrueba.CrearAsync();
        _servicio = new AparatoService(_fabrica, new TarifaCalculator());
    }

    public Task DisposeAsync()
    {
        _fabrica.Dispose();
        return Task.CompletedTask;
    }

    private PlantillaAparato Plantilla(string clave) => _servicio.PlantillasDeReferencia().Single(p => p.Clave == clave);

    // Lugar mensual con la tarifa de tres tramos (0–100 a $1, 100–200 a $2, 200+ a $4) y un recibo de 300 kWh.
    private async Task<int> LugarConReciboAsync(decimal kwh = 300m, bool estimado = false)
    {
        await using var db = _fabrica.CreateDbContext();
        var lugar = new Lugar
        {
            Nombre = "Mi casa",
            Tipo = TipoLugar.Casa,
            Tarifa = TarifasDePrueba.TresTramos(),
            DiaCorte = 1,
            MesesPorPeriodo = 1,
            Periodos = [new Periodo { Inicio = new(2026, 9, 1), Fin = new(2026, 9, 30), KwhTotales = kwh, Estimado = estimado }],
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

    private AparatoElegido Elegido(string clave, string nombre, decimal horas, int cantidad = 1, int? id = null)
        => new(Plantilla(clave), nombre, horas, cantidad, id);

    // ---- La lista de aparatos típicos (D3) ----

    [Fact]
    public void Plantillas_CatorceEnOrdenDeLoQueSuelenPesar()
    {
        Assert.Equal(
            [
                "AireAcondicionado", "Refrigerador", "CalentadorDeAgua", "BombaDeAgua", "Computadora", "Televisor", "Ventilador",
                "Microondas", "Modem", "Lavadora", "Cafetera", "Plancha", "IluminacionLed", "Licuadora",
            ],
            _servicio.PlantillasDeReferencia().Select(p => p.Clave));
    }

    [Fact]
    public void Plantillas_SoloLosQueSuelenSerVariosPreguntanCuantos()
    {
        Assert.Equal(
            ["AireAcondicionado", "Computadora", "Televisor", "Ventilador", "IluminacionLed"],
            _servicio.PlantillasDeReferencia().Where(p => p.SuelenSerVarios).Select(p => p.Clave));
    }

    // ---- Guardar (D1) con la cuenta a mano ----

    [Fact]
    public async Task GuardarElegidos_TresAparatos_ConLaCuentaAMano()
    {
        // Recibo de 300 kWh al mes: Total(300) = 100 + 200 + 100 × 4 = $700.
        // Aire 1,100 W × 6 h × 7 días × 52/12 × 0.9 = 180.18 kWh → Total(119.82) = 100 + 19.82 × 2 = $139.64 → $560.36.
        // Refrigerador 250 W × 24 h × 7 × 52/12 × 0.5 = 91 kWh → Total(209) = 100 + 200 + 9 × 4 = $336 → $364.
        // 4 focos de 10 W × 5 h × 7 × 52/12 = 6.0667 kWh → Total(293.93) = 300 + 93.93 × 4 = $675.73 → $24.27.
        var lugarId = await LugarConReciboAsync();

        var resultado = await _servicio.GuardarElegidosAsync(lugarId,
            [
                Elegido("AireAcondicionado", "Aire acondicionado", 6m),
                Elegido("Refrigerador", "Refrigerador", 24m),
                Elegido("IluminacionLed", "Foco o lámpara LED", 5m, cantidad: 4),
            ],
            []);

        Assert.True(resultado.Guardado);
        var aparatos = await AparatosAsync(lugarId);
        Assert.Equal(3, aparatos.Count);
        var aire = aparatos.Single(a => a.Nombre == "Aire acondicionado");
        Assert.Equal((CategoriaAparato.Climatizacion, 1_100m, 6m, 7, 0.9m, 1), (aire.Categoria, aire.Watts, aire.HorasPorDia, aire.DiasPorSemana, aire.CicloDeTrabajo, aire.Cantidad));
        Assert.Equal(4, aparatos.Single(a => a.Nombre == "Foco o lámpara LED").Cantidad);

        var costos = await _servicio.ObtenerCostosAsync(lugarId, Hoy);
        Assert.Equal(["Aire acondicionado", "Refrigerador", "Foco o lámpara LED"], costos.Costos.Filas.Select(f => f.Nombre));
        Assert.Equal([180.18m, 91m, 6.07m], costos.Costos.Filas.Select(f => Math.Round(f.KwhPorMes, 2)));
        Assert.Equal([560.36m, 364m, 24.27m], costos.Costos.Filas.Select(f => Math.Round(f.CostoMensual!.Value, 2)));
    }

    [Fact]
    public async Task GuardarElegidos_OtraVez_ActualizaSinDuplicar()
    {
        var lugarId = await LugarConReciboAsync();
        await _servicio.GuardarElegidosAsync(lugarId, [Elegido("Ventilador", "Ventilador", 8m)], []);
        var id = Assert.Single(await AparatosAsync(lugarId)).Id;

        await _servicio.GuardarElegidosAsync(lugarId, [Elegido("Ventilador", "Ventilador", 3m, cantidad: 2, id: id)], []);

        var ventilador = Assert.Single(await AparatosAsync(lugarId));
        Assert.Equal((id, 3m, 2), (ventilador.Id, ventilador.HorasPorDia, ventilador.Cantidad));
    }

    [Fact]
    public async Task GuardarElegidos_NoCambiaLaPotenciaAjustadaEnElEditorCompleto()
    {
        // E2: la persona ajustó la potencia de su aire (900 W, ciclo 0.7) en el editor completo; al volver a guardar
        // desde "Elegir mis aparatos" solo cambian las horas y la cantidad.
        var lugarId = await LugarConReciboAsync();
        await _servicio.GuardarElegidosAsync(lugarId, [Elegido("AireAcondicionado", "Aire acondicionado", 6m)], []);
        var aire = Assert.Single(await AparatosAsync(lugarId));
        await _servicio.GuardarAsync(new AparatoEditable(
            aire.Id, lugarId, aire.Nombre, aire.Categoria, 1, 900m, aire.HorasPorDia, 5, 0.7m));

        await _servicio.GuardarElegidosAsync(lugarId, [Elegido("AireAcondicionado", "Aire acondicionado", 4m, cantidad: 2, id: aire.Id)], []);

        var despues = Assert.Single(await AparatosAsync(lugarId));
        Assert.Equal((900m, 5, 0.7m), (despues.Watts, despues.DiasPorSemana, despues.CicloDeTrabajo));
        Assert.Equal((4m, 2), (despues.HorasPorDia, despues.Cantidad));
    }

    [Fact]
    public async Task GuardarElegidos_Quitar_EliminaSoloLosDesmarcados()
    {
        var lugarId = await LugarConReciboAsync();
        await _servicio.GuardarElegidosAsync(lugarId,
            [Elegido("Plancha", "Plancha", 1m), Elegido("Licuadora", "Licuadora", 0.1m)], []);
        var plancha = (await AparatosAsync(lugarId)).Single(a => a.Nombre == "Plancha");

        await _servicio.GuardarElegidosAsync(lugarId, [], [plancha.Id]);

        Assert.Equal(["Licuadora"], (await AparatosAsync(lugarId)).Select(a => a.Nombre));
    }

    [Fact]
    public async Task GuardarElegidos_UnAparatoConNombrePropio_QuedaIntacto()
    {
        // Precisión (1): un aparato que no sale de ninguna plantilla no aparece en "Elegir mis aparatos" ni se toca,
        // aunque se desmarquen todos los típicos.
        var lugarId = await LugarConReciboAsync();
        var propio = (await _servicio.GuardarAsync(new AparatoEditable(
            null, lugarId, "Horno de pan", CategoriaAparato.Cocina, 1, 1_800m, 2m, 3, 1m))).Id!.Value;
        await _servicio.GuardarElegidosAsync(lugarId, [Elegido("Microondas", "Microondas", 0.25m)], []);
        var aparatos = await AparatosAsync(lugarId);
        var emparejados = AparatosTipicos.Emparejar(
            _servicio.PlantillasDeReferencia().Select(p => new PlantillaConNombre(p, p.Clave == "Microondas" ? "Microondas" : p.Clave, [])),
            aparatos);
        var microondas = emparejados.Single(e => e.Existente is not null);

        // Se desmarca todo lo típico: solo se quita el microondas.
        await _servicio.GuardarElegidosAsync(lugarId, [], [microondas.Existente!.Id]);

        var queda = Assert.Single(await AparatosAsync(lugarId));
        Assert.Equal((propio, "Horno de pan", 1_800m, 2m, 3), (queda.Id, queda.Nombre, queda.Watts, queda.HorasPorDia, queda.DiasPorSemana));
        Assert.DoesNotContain(emparejados, e => e.Existente?.Id == propio);
    }

    [Fact]
    public async Task GuardarElegidos_ConUnError_NoGuardaNada()
    {
        var lugarId = await LugarConReciboAsync();

        var resultado = await _servicio.GuardarElegidosAsync(lugarId,
            [Elegido("Refrigerador", "Refrigerador", 24m), Elegido("Ventilador", "Ventilador", 30m)], []);

        Assert.False(resultado.Guardado);
        Assert.Equal([CodigoErrorAparato.HorasFueraDeRango], resultado.Errores["Ventilador"]);
        Assert.Empty(await AparatosAsync(lugarId));
    }

    // ---- Emparejar con lo que ya hay (E1) ----

    [Fact]
    public void Emparejar_PorNombreActualOAnterior_SinImportarMayusculasNiEspacios()
    {
        var plantillas = _servicio.PlantillasDeReferencia();
        PlantillaConNombre Con(string clave, string nombre, params string[] anteriores)
            => new(plantillas.Single(p => p.Clave == clave), nombre, anteriores);
        var aparatos = new[]
        {
            new Aparato { Id = 1, Nombre = "  refrigerador " },
            new Aparato { Id = 2, Nombre = "Televisor" },
            new Aparato { Id = 3, Nombre = "Mi tele del cuarto" },
        };

        var emparejados = AparatosTipicos.Emparejar(
            [Con("Refrigerador", "Refrigerador"), Con("Televisor", "Televisión", "Televisor"), Con("Plancha", "Plancha")], aparatos);

        Assert.Equal([1, 2, (int?)null], emparejados.Select(e => e.Existente?.Id));
        Assert.Equal(["Refrigerador", "Televisión", "Plancha"], emparejados.Select(e => e.Nombre));
    }

    [Fact]
    public void Emparejar_DosAparatosConElMismoNombre_SoloUnoPorPlantilla()
    {
        var plantilla = _servicio.PlantillasDeReferencia().Single(p => p.Clave == "Ventilador");
        var aparatos = new[] { new Aparato { Id = 7, Nombre = "Ventilador" }, new Aparato { Id = 8, Nombre = "Ventilador" } };

        var emparejado = Assert.Single(AparatosTipicos.Emparejar([new PlantillaConNombre(plantilla, "Ventilador", [])], aparatos));

        Assert.Equal(7, emparejado.Existente!.Id);
    }

    // ---- D4: el consumo de un recibo por monto es aproximado ----

    [Fact]
    public async Task Costos_ConUnReciboPorMonto_SonAproximados()
    {
        var lugarId = await LugarConReciboAsync(estimado: true);
        await _servicio.GuardarElegidosAsync(lugarId, [Elegido("Refrigerador", "Refrigerador", 24m)], []);

        var costos = await _servicio.ObtenerCostosAsync(lugarId, Hoy);

        Assert.True(costos.ConsumoAproximado);
        Assert.True(costos.Costos.ConPesos);
        Assert.Equal(364m, Math.Round(costos.Costos.Filas[0].CostoMensual!.Value, 2));
    }

    [Fact]
    public async Task Costos_ConUnReciboConLecturas_NoSonAproximados()
    {
        var lugarId = await LugarConReciboAsync(estimado: false);
        await _servicio.GuardarElegidosAsync(lugarId, [Elegido("Refrigerador", "Refrigerador", 24m)], []);

        Assert.False((await _servicio.ObtenerCostosAsync(lugarId, Hoy)).ConsumoAproximado);
    }

    [Fact]
    public void ConsumoAproximado_ConLecturasDelReciboActual_NoLoEsAunqueElAnteriorSeaPorMonto()
    {
        // Recibo por monto que cerró el 30 de septiembre; dos lecturas en octubre: ya se mide el consumo.
        var lugar = new Lugar
        {
            DiaCorte = 30,
            MesesPorPeriodo = 1,
            Periodos = [new Periodo { Inicio = new(2026, 9, 1), Fin = new(2026, 9, 30), KwhTotales = 300m, Estimado = true }],
            Lecturas = [new Lectura { Fecha = new(2026, 10, 1), ValorKwh = 1_000m }, new Lectura { Fecha = new(2026, 10, 15), ValorKwh = 1_140m }],
        };

        Assert.False(ConsumoMedido.EsAproximado(lugar, Hoy));
        Assert.False(ConsumoMedido.EsAproximado(new Lugar { MesesPorPeriodo = 1, DiaCorte = 1 }, Hoy));
    }
}
