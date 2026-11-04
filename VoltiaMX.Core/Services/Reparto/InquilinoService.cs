using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum CodigoErrorInquilino
{
    NombreVacio,
    NombreMuyLargo,
    NombreRepetido,
    MaximoAlcanzado,
}

public enum ResultadoEliminarInquilino
{
    Eliminado,

    /// <summary>
    /// Figura en algún reparto: solo se puede dar de baja.
    /// </summary>
    TieneRepartos,
    NoExiste,
}

/// <summary>
/// Inquilino nuevo (<see cref="Id"/> null) o editado.
/// </summary>
public sealed record InquilinoEditable(int? Id, int LugarId, string Nombre);

public sealed record InquilinoListado(int Id, string Nombre, bool Activo, DateOnly FechaAlta, bool TieneRepartos);

public sealed record ResultadoGuardarInquilino(int? Id, IReadOnlyList<CodigoErrorInquilino> Errores);

/// <summary>
/// Reglas del nombre y del máximo de inquilinos activos por lugar.
/// </summary>
public static class InquilinoValidador
{
    public const int LargoMaximoNombre = 40;
    public const int MaximoActivos = 30;

    /// <param name="otrosNombres">Nombres de los demás inquilinos del lugar (activos o no).</param>
    /// <param name="activos">Inquilinos activos del lugar sin contar a este.</param>
    public static IReadOnlyList<CodigoErrorInquilino> Validar(string? nombre, IEnumerable<string> otrosNombres, int activos)
    {
        var errores = new List<CodigoErrorInquilino>();
        var limpio = nombre?.Trim() ?? string.Empty;
        if (limpio.Length == 0)
        {
            errores.Add(CodigoErrorInquilino.NombreVacio);
        }
        else if (limpio.Length > LargoMaximoNombre)
        {
            errores.Add(CodigoErrorInquilino.NombreMuyLargo);
        }
        else if (otrosNombres.Any(o => string.Equals(o.Trim(), limpio, StringComparison.CurrentCultureIgnoreCase)))
        {
            errores.Add(CodigoErrorInquilino.NombreRepetido);
        }

        if (activos >= MaximoActivos)
        {
            errores.Add(CodigoErrorInquilino.MaximoAlcanzado);
        }

        return errores;
    }
}

public interface IInquilinoService
{
    Task<IReadOnlyList<InquilinoListado>> ListarAsync(int lugarId, CancellationToken cancellationToken = default);

    Task<ResultadoGuardarInquilino> GuardarAsync(InquilinoEditable inquilino, DateOnly hoy, CancellationToken cancellationToken = default);

    Task DarDeBajaAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vuelve a activarlo si no se pasa del máximo de activos.
    /// </summary>
    Task<IReadOnlyList<CodigoErrorInquilino>> ReactivarAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Solo se elimina si no figura en ningún reparto.
    /// </summary>
    Task<ResultadoEliminarInquilino> EliminarAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class InquilinoService(IDbContextFactory<VoltiaDbContext> fabrica) : IInquilinoService
{
    public async Task<IReadOnlyList<InquilinoListado>> ListarAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var conRepartos = await db.RepartoInquilinos
            .Where(l => l.InquilinoId != null && l.Reparto!.LugarId == lugarId)
            .Select(l => l.InquilinoId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var inquilinos = await db.Inquilinos
            .AsNoTracking()
            .Where(i => i.LugarId == lugarId)
            .ToListAsync(cancellationToken);

        return inquilinos
            .OrderByDescending(i => i.Activo)
            .ThenBy(i => i.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(i => i.Id)
            .Select(i => new InquilinoListado(i.Id, i.Nombre, i.Activo, i.FechaAlta, conRepartos.Contains(i.Id)))
            .ToList();
    }

    public async Task<ResultadoGuardarInquilino> GuardarAsync(InquilinoEditable inquilino, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inquilino);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var otros = await db.Inquilinos
            .Where(i => i.LugarId == inquilino.LugarId && i.Id != inquilino.Id)
            .Select(i => new { i.Nombre, i.Activo })
            .ToListAsync(cancellationToken);

        Inquilino? entidad = null;
        if (inquilino.Id is int id)
        {
            entidad = await db.Inquilinos.FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
                ?? throw new InvalidOperationException($"El inquilino {id} ya no existe.");
        }

        // Editar a uno dado de baja no lo cuenta como activo nuevo.
        var activosQueCuentan = entidad is { Activo: false } ? 0 : otros.Count(o => o.Activo);
        var errores = InquilinoValidador.Validar(inquilino.Nombre, otros.Select(o => o.Nombre), activosQueCuentan);
        if (errores.Count > 0)
        {
            return new ResultadoGuardarInquilino(inquilino.Id, errores);
        }

        if (entidad is null)
        {
            entidad = new Inquilino { LugarId = inquilino.LugarId, Activo = true, FechaAlta = hoy };
            db.Inquilinos.Add(entidad);
        }

        entidad.Nombre = inquilino.Nombre.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return new ResultadoGuardarInquilino(entidad.Id, []);
    }

    public async Task DarDeBajaAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        await db.Inquilinos.Where(i => i.Id == id).ExecuteUpdateAsync(s => s.SetProperty(i => i.Activo, false), cancellationToken);
    }

    public async Task<IReadOnlyList<CodigoErrorInquilino>> ReactivarAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var inquilino = await db.Inquilinos.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (inquilino is null || inquilino.Activo)
        {
            return [];
        }

        var activos = await db.Inquilinos.CountAsync(i => i.LugarId == inquilino.LugarId && i.Activo, cancellationToken);
        if (activos >= InquilinoValidador.MaximoActivos)
        {
            return [CodigoErrorInquilino.MaximoAlcanzado];
        }

        inquilino.Activo = true;
        await db.SaveChangesAsync(cancellationToken);
        return [];
    }

    public async Task<ResultadoEliminarInquilino> EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        if (await db.RepartoInquilinos.AnyAsync(l => l.InquilinoId == id, cancellationToken))
        {
            return ResultadoEliminarInquilino.TieneRepartos;
        }

        return await db.Inquilinos.Where(i => i.Id == id).ExecuteDeleteAsync(cancellationToken) > 0
            ? ResultadoEliminarInquilino.Eliminado
            : ResultadoEliminarInquilino.NoExiste;
    }
}
