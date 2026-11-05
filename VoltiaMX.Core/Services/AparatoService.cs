using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class AparatoService(IDbContextFactory<VoltiaDbContext> fabrica, ITarifaCalculator calculadora) : IAparatoService
{
    // EJEMPLO: valores típicos de referencia, no de un aparato real; la persona debe usar los de su etiqueta.
    private static readonly PlantillaAparato[] Plantillas =
    [
        new("AireAcondicionado", CategoriaAparato.Climatizacion, 1_100m, 8m, 7, 0.9m), // EJEMPLO
        new("Refrigerador", CategoriaAparato.Refrigeracion, 250m, 24m, 7, 0.5m), // EJEMPLO
        new("Lavadora", CategoriaAparato.Lavado, 500m, 1m, 3, 1m), // EJEMPLO
        new("IluminacionLed", CategoriaAparato.Iluminacion, 10m, 6m, 7, 1m), // EJEMPLO
        new("Televisor", CategoriaAparato.Electronica, 100m, 5m, 7, 1m), // EJEMPLO
        new("Computadora", CategoriaAparato.Electronica, 150m, 6m, 5, 1m), // EJEMPLO
        new("Microondas", CategoriaAparato.Cocina, 1_200m, 0.25m, 7, 1m), // EJEMPLO
        new("Ventilador", CategoriaAparato.Climatizacion, 60m, 8m, 7, 1m), // EJEMPLO
        new("Plancha", CategoriaAparato.Lavado, 1_000m, 1m, 2, 0.6m), // EJEMPLO
        new("BombaDeAgua", CategoriaAparato.Otro, 750m, 1m, 7, 1m), // EJEMPLO
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
            SugerenciasAhorro.Generar(lugar.Aparatos, calculadora, lugar.Tarifa, lugar.MesesPorPeriodo, medido));
    }

    public IReadOnlyList<PlantillaAparato> PlantillasDeReferencia() => Plantillas;
}
