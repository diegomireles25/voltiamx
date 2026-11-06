using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class AparatoService(IDbContextFactory<VoltiaDbContext> fabrica, ITarifaCalculator calculadora) : IAparatoService
{
    // EJEMPLO: valores típicos de referencia, no de un aparato real; la persona debe usar los de su etiqueta.
    // Fase 9d (D3): aparatos típicos de un hogar, de los que más suelen pesar al mes a los que menos (≈ kWh al mes
    // con estos valores, solo para el orden). SuelenSerVarios: "Elegir mis aparatos" pregunta cuántos hay.
    private static readonly PlantillaAparato[] Plantillas =
    [
        new("AireAcondicionado", CategoriaAparato.Climatizacion, 1_100m, 8m, 7, 0.9m, SuelenSerVarios: true), // EJEMPLO ≈ 240
        new("Refrigerador", CategoriaAparato.Refrigeracion, 250m, 24m, 7, 0.5m), // EJEMPLO ≈ 91
        new("CalentadorDeAgua", CategoriaAparato.Otro, 1_500m, 1m, 7, 1m), // EJEMPLO ≈ 46
        new("BombaDeAgua", CategoriaAparato.Otro, 750m, 1m, 7, 1m), // EJEMPLO ≈ 23
        new("Computadora", CategoriaAparato.Electronica, 150m, 6m, 5, 1m, SuelenSerVarios: true), // EJEMPLO ≈ 20
        new("Televisor", CategoriaAparato.Electronica, 100m, 5m, 7, 1m, SuelenSerVarios: true), // EJEMPLO ≈ 15
        new("Ventilador", CategoriaAparato.Climatizacion, 60m, 8m, 7, 1m, SuelenSerVarios: true), // EJEMPLO ≈ 15
        new("Microondas", CategoriaAparato.Cocina, 1_200m, 0.25m, 7, 1m), // EJEMPLO ≈ 9
        new("Modem", CategoriaAparato.Electronica, 10m, 24m, 7, 1m), // EJEMPLO ≈ 7
        new("Lavadora", CategoriaAparato.Lavado, 500m, 1m, 3, 1m), // EJEMPLO ≈ 6.5
        new("Cafetera", CategoriaAparato.Cocina, 800m, 0.25m, 7, 1m), // EJEMPLO ≈ 6
        new("Plancha", CategoriaAparato.Lavado, 1_000m, 1m, 2, 0.6m), // EJEMPLO ≈ 5
        new("IluminacionLed", CategoriaAparato.Iluminacion, 10m, 5m, 7, 1m, SuelenSerVarios: true), // EJEMPLO ≈ 1.5 por foco
        new("Licuadora", CategoriaAparato.Cocina, 400m, 0.1m, 7, 1m), // EJEMPLO ≈ 1.2
    ];

    public async Task<IReadOnlyList<Aparato>> ListarAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Aparatos
            .AsNoTracking()
            .Where(a => a.LugarId == lugarId)
            .OrderBy(a => a.Nombre)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<ResultadoGuardarAparato> GuardarAsync(AparatoEditable aparato, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aparato);
        var errores = AparatoValidador.Validar(aparato);
        if (errores.Count > 0)
        {
            return new ResultadoGuardarAparato(aparato.Id, errores);
        }

        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        Aparato entidad;
        if (aparato.Id is int id)
        {
            entidad = await db.Aparatos.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
                ?? throw new InvalidOperationException($"El aparato {id} ya no existe.");
        }
        else
        {
            entidad = new Aparato { LugarId = aparato.LugarId };
            db.Aparatos.Add(entidad);
        }

        entidad.Nombre = aparato.Nombre.Trim();
        entidad.Categoria = aparato.Categoria;
        entidad.Cantidad = aparato.Cantidad;
        entidad.Watts = aparato.Watts;
        entidad.HorasPorDia = aparato.HorasPorDia;
        entidad.DiasPorSemana = aparato.DiasPorSemana;
        entidad.CicloDeTrabajo = aparato.CicloDeTrabajo;

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoGuardarAparato(entidad.Id, []);
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Aparatos.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    public async Task<RankingAparatos> ObtenerRankingAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Lecturas)
            .Include(l => l.Periodos)
            .Include(l => l.Aparatos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);

        return lugar is null
            ? RankingAparatos.Calcular([], null)
            : RankingAparatos.Calcular(lugar.Aparatos, ConsumoMedido.KwhPorMes(lugar, hoy));
    }

    public async Task<CostosDelLugar> ObtenerCostosAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Lecturas)
            .Include(l => l.Periodos)
            .Include(l => l.Aparatos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);

        if (lugar is null)
        {
            return new CostosDelLugar(RankingAparatos.Calcular([], null), new CostosAparatos(false, []), SugerenciasDeAhorro.Ninguna);
        }

        var medido = ConsumoMedido.KwhPorMes(lugar, hoy);
        return new CostosDelLugar(
            RankingAparatos.Calcular(lugar.Aparatos, medido),
            CostoAparato.Calcular(lugar.Aparatos, calculadora, lugar.Tarifa, lugar.MesesPorPeriodo, medido),
            SugerenciasAhorro.Generar(lugar.Aparatos, calculadora, lugar.Tarifa, lugar.MesesPorPeriodo, medido),
            lugar.Tarifa,
            lugar.MesesPorPeriodo,
            medido,
            ConsumoMedido.EsAproximado(lugar, hoy));
    }

    public IReadOnlyList<PlantillaAparato> PlantillasDeReferencia() => Plantillas;

    public IReadOnlyList<GrupoAparatos> Agrupar(CostosDelLugar costos)
    {
        ArgumentNullException.ThrowIfNull(costos);
        return GruposDeAparatos.Agrupar(costos.Costos.Filas, calculadora, costos.Tarifa, costos.MesesPorPeriodo, costos.ConsumoMedidoMes);
    }

    public async Task<ResultadoGuardarElegidos> GuardarElegidosAsync(
        int lugarId, IReadOnlyList<AparatoElegido> elegidos, IReadOnlyList<int> quitar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(elegidos);
        ArgumentNullException.ThrowIfNull(quitar);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var existentes = await db.Aparatos.Where(a => a.LugarId == lugarId).ToDictionaryAsync(a => a.Id, cancellationToken);

        // Primero se valida todo: con un error no se guarda nada. Fase 9g: un elegido puede quedar como un aparato
        // (mismas horas) o como una unidad por aparato; los registros que ya no quedan se quitan.
        var errores = new Dictionary<string, IReadOnlyList<CodigoErrorAparato>>();
        var cambios = new List<(Aparato? Existente, AparatoEditable Editable)>();
        var sobran = new List<Aparato>();
        foreach (var elegido in elegidos)
        {
            // Los registros de la plantilla: el aparato, los demás y los de cada unidad (que ya existían).
            var ids = (elegido.Id is int id ? new[] { id } : [])
                .Concat(elegido.Otros ?? [])
                .Concat(elegido.Unidades?.Where(u => u.Id is not null).Select(u => u.Id!.Value) ?? [])
                .Distinct();
            var registros = ids.Select(i => existentes.GetValueOrDefault(i)).OfType<Aparato>().ToList();
            var (quedan, quitarDeEste) = AparatosTipicos.Plan(elegido, lugarId, registros);
            foreach (var (_, editable) in quedan)
            {
                var deEste = AparatoValidador.Validar(editable);
                if (deEste.Count > 0)
                {
                    errores[elegido.Plantilla.Clave] = deEste;
                }
            }

            cambios.AddRange(quedan);
            sobran.AddRange(quitarDeEste);
        }

        if (errores.Count > 0)
        {
            return new ResultadoGuardarElegidos(errores);
        }

        foreach (var (existente, editable) in cambios)
        {
            var entidad = existente ?? db.Aparatos.Add(new Aparato { LugarId = lugarId }).Entity;
            entidad.Nombre = editable.Nombre.Trim();
            entidad.Categoria = editable.Categoria;
            entidad.Cantidad = editable.Cantidad;
            entidad.Watts = editable.Watts;
            entidad.HorasPorDia = editable.HorasPorDia;
            entidad.DiasPorSemana = editable.DiasPorSemana;
            entidad.CicloDeTrabajo = editable.CicloDeTrabajo;
        }

        // Solo los que la persona desmarcó (y las unidades que ya no quedan), y solo de este lugar.
        foreach (var id in quitar.Concat(sobran.Select(s => s.Id)).Distinct())
        {
            if (existentes.TryGetValue(id, out var aparato))
            {
                db.Aparatos.Remove(aparato);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoGuardarElegidos(errores);
    }
}
