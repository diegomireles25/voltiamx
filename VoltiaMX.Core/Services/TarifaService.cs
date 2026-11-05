using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class TarifaService(IDbContextFactory<VoltiaDbContext> fabrica, ITarifaCalculator? calculadora = null) : ITarifaService
{
    private readonly ITarifaCalculator _calculadora = calculadora ?? new TarifaCalculator();

    public async Task<IReadOnlyList<Tarifa>> ObtenerTarifasAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Tarifas.AsNoTracking().OrderBy(t => t.Nombre).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TarifaConUso>> ListarConUsoAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var tarifas = await db.Tarifas
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new { t.Id, t.Nombre, t.Tipo, Rangos = t.Rangos.Count })
            .ToListAsync(cancellationToken);
        var usos = await db.Lugares
            .AsNoTracking()
            .Select(l => new { l.TarifaId, l.Nombre })
            .ToListAsync(cancellationToken);

        return tarifas
            .Select(t => new TarifaConUso(
                t.Id, t.Nombre, t.Tipo, t.Rangos,
                usos.Where(u => u.TarifaId == t.Id).Select(u => u.Nombre).ToList()))
            .ToList();
    }

    public async Task<TarifaEditable?> ObtenerParaEditarAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var tarifa = await db.Tarifas.AsNoTracking().Include(t => t.Rangos).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return tarifa is null ? null : AEditable(tarifa, tarifa.Id, tarifa.Nombre);
    }

    public async Task<ResultadoGuardarTarifa> GuardarAsync(TarifaEditable tarifa, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tarifa);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var otrosNombres = await db.Tarifas
            .Where(t => tarifa.Id == null || t.Id != tarifa.Id)
            .Select(t => t.Nombre)
            .ToListAsync(cancellationToken);

        var errores = TarifaValidador.Validar(tarifa, otrosNombres);
        if (errores.Count > 0)
        {
            return new ResultadoGuardarTarifa(tarifa.Id, errores);
        }

        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        Tarifa entidad;
        if (tarifa.Id is int id)
        {
            entidad = await db.Tarifas.Include(t => t.Rangos).FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                ?? throw new InvalidOperationException($"La tarifa {id} ya no existe.");

            // Primero se borran los rangos viejos para no chocar con el índice único (TarifaId, Orden).
            db.TarifaRangos.RemoveRange(entidad.Rangos);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            entidad = new Tarifa();
            db.Tarifas.Add(entidad);
        }

        entidad.Nombre = tarifa.Nombre.Trim();
        entidad.Tipo = tarifa.Tipo;
        entidad.CargoFijoMensual = tarifa.CargoFijoMensual;
        entidad.LimiteDacKwhMensual = tarifa.LimiteDacKwhMensual;
        entidad.Vigencia = tarifa.Vigencia!.Value;
        // Lo que se guarda en el editor son los precios de la persona (D3): deja de ser de ejemplo.
        entidad.EsDeEjemplo = false;
        entidad.Rangos = [.. tarifa.RangosConDesde()];

        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return new ResultadoGuardarTarifa(entidad.Id, []);
    }

    public async Task<int?> DuplicarAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var original = await db.Tarifas.AsNoTracking().Include(t => t.Rangos).FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (original is null)
        {
            return null;
        }

        var nombres = (await db.Tarifas.Select(t => t.Nombre).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resultado = await GuardarAsync(AEditable(original, null, NombreDeCopia(original.Nombre, nombres)), cancellationToken);
        return resultado.Id;
    }

    public async Task<ResultadoEliminarTarifa> EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var tarifa = await db.Tarifas.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (tarifa is null)
        {
            return new ResultadoEliminarTarifa(MotivoNoEliminarTarifa.NoExiste, []);
        }

        var lugares = await db.Lugares.Where(l => l.TarifaId == id).Select(l => l.Nombre).ToListAsync(cancellationToken);
        if (lugares.Count > 0)
        {
            return new ResultadoEliminarTarifa(MotivoNoEliminarTarifa.EnUso, lugares);
        }

        if (await db.Tarifas.CountAsync(cancellationToken) == 1)
        {
            return new ResultadoEliminarTarifa(MotivoNoEliminarTarifa.EsLaUltima, []);
        }

        db.Tarifas.Remove(tarifa);
        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoEliminarTarifa(null, []);
    }

    public async Task<bool> AsignarALugarAsync(int lugarId, int tarifaId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares.FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);
        if (lugar is null || !await db.Tarifas.AnyAsync(t => t.Id == tarifaId, cancellationToken))
        {
            return false;
        }

        lugar.TarifaId = tarifaId;
        lugar.TarifaPorConfirmar = false;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ConteoRecalculo> ContarParaRecalcularAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var conRecibo = await db.Periodos
            .Where(p => p.LugarId == lugarId)
            .Select(p => p.MontoRecibo != null)
            .ToListAsync(cancellationToken);
        return new ConteoRecalculo(conRecibo.Count(c => !c), conRecibo.Count(c => c));
    }

    public async Task<ResultadoRecalculo> RecalcularPeriodosAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Periodos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);
        if (lugar?.Tarifa is null)
        {
            return new ResultadoRecalculo(0, 0, 0);
        }

        var recalculados = 0;
        var cambiados = 0;
        foreach (var periodo in lugar.Periodos)
        {
            // El monto real del recibo manda: esos periodos no se tocan.
            if (periodo.MontoRecibo is not null)
            {
                continue;
            }

            recalculados++;
            var monto = _calculadora.Calcular(periodo.KwhTotales, lugar.Tarifa, lugar.MesesPorPeriodo).Total;
            if (monto != periodo.Monto)
            {
                periodo.Monto = monto;
                cambiados++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoRecalculo(recalculados, cambiados, lugar.Periodos.Count - recalculados);
    }

    private static TarifaEditable AEditable(Tarifa tarifa, int? id, string nombre) => new(
        id,
        nombre,
        tarifa.Tipo,
        tarifa.CargoFijoMensual,
        tarifa.LimiteDacKwhMensual,
        tarifa.Vigencia,
        tarifa.Rangos.OrderBy(r => r.Orden).Select(r => new RangoEditable(r.Nombre, r.KwhHasta, r.PrecioPorKwh)).ToList());

    /// <summary>
    /// "X (copia)", "X (copia 2)"...; si no cabe en el largo máximo, se recorta el nombre original.
    /// </summary>
    private static string NombreDeCopia(string original, IReadOnlySet<string> existentes)
    {
        for (var n = 1; ; n++)
        {
            var sufijo = n == 1 ? " (copia)" : $" (copia {n})";
            var baseNombre = original.Length + sufijo.Length > TarifaValidador.LargoMaximoNombre
                ? original[..(TarifaValidador.LargoMaximoNombre - sufijo.Length)]
                : original;
            var candidato = baseNombre + sufijo;
            if (!existentes.Contains(candidato))
            {
                return candidato;
            }
        }
    }
}
