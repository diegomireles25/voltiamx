using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Periodo cerrado que se puede repartir.
/// </summary>
public sealed record PeriodoRepartible(
    DateOnly Inicio,
    DateOnly Fin,
    decimal KwhTotales,
    decimal Monto,
    decimal? MontoRecibo,
    bool Estimado,
    bool TieneReparto);

/// <summary>
/// Un inquilino activo con sus lecturas precargadas (null = hay que capturarla).
/// </summary>
public sealed record FilaPreparada(int InquilinoId, string Nombre, decimal? LecturaAnterior, decimal? LecturaActual);

/// <summary>
/// Lo que necesita la pantalla de un reparto nuevo: periodos, total con su origen y lecturas precargadas.
/// </summary>
public sealed record PreparacionReparto(
    int LugarId,
    string NombreLugar,
    bool EsDemo,
    IReadOnlyList<PeriodoRepartible> Periodos,
    PeriodoRepartible? Periodo,
    decimal Total,
    OrigenTotal Origen,
    IReadOnlyList<FilaPreparada> Filas);

public sealed record LecturaInquilino(int InquilinoId, decimal Anterior, decimal Actual);

public sealed record EntradaReparto(
    int LugarId,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFin,
    decimal Total,
    OrigenTotal Origen,
    ModoReparto Modo,
    IReadOnlyList<LecturaInquilino> Lecturas);

public sealed record CalculoReparto(decimal KwhPrincipal, ResultadoReparto Resultado);

public enum EstadoGuardarReparto
{
    Guardado,

    /// <summary>
    /// Ya hay un reparto de ese periodo: hay que confirmar el reemplazo.
    /// </summary>
    YaExiste,
    Invalido,
}

public sealed record ResultadoGuardarReparto(EstadoGuardarReparto Estado, int? Id, IReadOnlyList<AvisoReparto> Avisos);

public sealed record RepartoListado(
    int Id,
    DateOnly Inicio,
    DateOnly Fin,
    decimal TotalRepartido,
    decimal TotalCobrado,
    int Inquilinos,
    DateTime FechaCreacion);

public sealed record LineaRepartoGuardada(
    int? InquilinoId,
    string Nombre,
    decimal LecturaAnterior,
    decimal LecturaActual,
    decimal Kwh,
    decimal Cuota);

/// <summary>
/// Reparto tal como se guardó. Las cifras derivadas salen de la copia fija, sin recalcular cuotas.
/// </summary>
public sealed record RepartoGuardado(
    int Id,
    int LugarId,
    string NombreLugar,
    bool EsDemo,
    DateOnly Inicio,
    DateOnly Fin,
    decimal TotalRepartido,
    OrigenTotal Origen,
    ModoReparto Modo,
    decimal KwhPrincipal,
    DateTime FechaCreacion,
    IReadOnlyList<LineaRepartoGuardada> Lineas)
{
    public decimal KwhRepartidos => Lineas.Sum(l => l.Kwh);

    public decimal TotalCobrado => Lineas.Sum(l => l.Cuota);

    public decimal AbsorbePropietario => TotalRepartido - TotalCobrado;

    public decimal? PrecioPromedioKwh => KwhRepartidos > 0 ? Math.Round(TotalCobrado / KwhRepartidos, 4) : null;

    public IReadOnlyList<AvisoReparto> Avisos => KwhRepartidos > KwhPrincipal + Repartidor.ToleranciaKwh
        ? [AvisoReparto.SubmedidoresExcedenPrincipal]
        : [];
}

public interface IRepartoService
{
    /// <summary>
    /// Periodos cerrados del lugar y los datos precargados del periodo elegido (el más reciente si es null).
    /// </summary>
    Task<PreparacionReparto> PrepararAsync(int lugarId, DateOnly? periodoFin = null, CancellationToken cancellationToken = default);

    Task<CalculoReparto> CalcularAsync(EntradaReparto entrada, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda la copia fija en una sola transacción. Si ya hay reparto del periodo, solo lo reemplaza con
    /// <paramref name="reemplazar"/>.
    /// </summary>
    Task<ResultadoGuardarReparto> GuardarAsync(EntradaReparto entrada, bool reemplazar, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RepartoListado>> ListarAsync(int lugarId, CancellationToken cancellationToken = default);

    Task<RepartoGuardado?> ObtenerAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class RepartoService(IDbContextFactory<VoltiaDbContext> fabrica, TimeProvider? reloj = null) : IRepartoService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<PreparacionReparto> PrepararAsync(int lugarId, DateOnly? periodoFin = null, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Periodos)
            .Include(l => l.Inquilinos)
            .Include(l => l.Repartos).ThenInclude(r => r.Inquilinos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken)
            ?? throw new InvalidOperationException($"El lugar {lugarId} ya no existe.");

        var periodos = lugar.Periodos
            .OrderByDescending(p => p.Fin)
            .Select(p => new PeriodoRepartible(
                p.Inicio, p.Fin, p.KwhTotales, p.Monto, p.MontoRecibo, p.Estimado,
                lugar.Repartos.Any(r => r.PeriodoInicio == p.Inicio && r.PeriodoFin == p.Fin)))
            .ToList();

        var elegido = periodoFin is { } fin ? periodos.FirstOrDefault(p => p.Fin == fin) ?? periodos.FirstOrDefault() : periodos.FirstOrDefault();
        var (total, origen) = elegido switch
        {
            { MontoRecibo: { } recibo } => (recibo, OrigenTotal.MontoRecibo),
            { } p => (p.Monto, OrigenTotal.MontoCalculado),
            null => (0m, OrigenTotal.MontoCalculado),
        };

        var mismo = elegido is null ? null : lugar.Repartos.FirstOrDefault(r => r.PeriodoInicio == elegido.Inicio && r.PeriodoFin == elegido.Fin);
        var anteriores = elegido is null
            ? []
            : lugar.Repartos.Where(r => r.PeriodoFin < elegido.Fin).OrderByDescending(r => r.PeriodoFin).ToList();

        var filas = lugar.Inquilinos
            .Where(i => i.Activo)
            .OrderBy(i => i.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(i => i.Id)
            .Select(i =>
            {
                // Si el periodo ya tiene reparto, sus propias lecturas; si no, la anterior es la actual del
                // último reparto previo de ese inquilino.
                if (mismo?.Inquilinos.FirstOrDefault(l => l.InquilinoId == i.Id) is { } propia)
                {
                    return new FilaPreparada(i.Id, i.Nombre, propia.LecturaAnterior, propia.LecturaActual);
                }

                var previa = anteriores.SelectMany(r => r.Inquilinos).FirstOrDefault(l => l.InquilinoId == i.Id);
                return new FilaPreparada(i.Id, i.Nombre, previa?.LecturaActual, null);
            })
            .ToList();

        return new PreparacionReparto(lugar.Id, lugar.Nombre, lugar.EsDemo, periodos, elegido, total, origen, filas);
    }

    public async Task<CalculoReparto> CalcularAsync(EntradaReparto entrada, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var kwhPrincipal = await KwhPrincipalAsync(db, entrada, cancellationToken);
        return new CalculoReparto(kwhPrincipal, Calcular(entrada, kwhPrincipal));
    }

    public async Task<ResultadoGuardarReparto> GuardarAsync(EntradaReparto entrada, bool reemplazar, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var kwhPrincipal = await KwhPrincipalAsync(db, entrada, cancellationToken);
        var resultado = Calcular(entrada, kwhPrincipal);
        if (!resultado.PuedeGuardar)
        {
            return new ResultadoGuardarReparto(EstadoGuardarReparto.Invalido, null, resultado.Avisos);
        }

        var existente = await db.Repartos
            .Where(r => r.LugarId == entrada.LugarId && r.PeriodoInicio == entrada.PeriodoInicio && r.PeriodoFin == entrada.PeriodoFin)
            .Select(r => (int?)r.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existente is not null && !reemplazar)
        {
            return new ResultadoGuardarReparto(EstadoGuardarReparto.YaExiste, existente, resultado.Avisos);
        }

        var ids = entrada.Lecturas.Select(l => l.InquilinoId).ToList();
        var nombres = await db.Inquilinos
            .Where(i => i.LugarId == entrada.LugarId && ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre, cancellationToken);
        if (nombres.Count != ids.Distinct().Count())
        {
            throw new InvalidOperationException("Algún inquilino ya no existe o es de otro lugar.");
        }

        var reparto = new Reparto
        {
            LugarId = entrada.LugarId,
            PeriodoInicio = entrada.PeriodoInicio,
            PeriodoFin = entrada.PeriodoFin,
            TotalRepartido = entrada.Total,
            OrigenTotal = entrada.Origen,
            Modo = entrada.Modo,
            KwhPrincipal = kwhPrincipal,
            FechaCreacion = _reloj.GetLocalNow().DateTime,
            Inquilinos = entrada.Lecturas.Select((l, i) => new RepartoInquilino
            {
                InquilinoId = l.InquilinoId,
                NombreInquilino = nombres[l.InquilinoId],
                LecturaAnterior = l.Anterior,
                LecturaActual = l.Actual,
                Kwh = resultado.Kwh[i],
                Cuota = resultado.Cuotas[i],
            }).ToList(),
        };

        // Reemplazo y alta juntos: si algo falla, queda el reparto anterior.
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);
        if (existente is int id)
        {
            await db.Repartos.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken);
        }

        db.Repartos.Add(reparto);
        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return new ResultadoGuardarReparto(EstadoGuardarReparto.Guardado, reparto.Id, resultado.Avisos);
    }

    public async Task<IReadOnlyList<RepartoListado>> ListarAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var repartos = await db.Repartos
            .AsNoTracking()
            .Include(r => r.Inquilinos)
            .Where(r => r.LugarId == lugarId)
            .ToListAsync(cancellationToken);

        return repartos
            .OrderByDescending(r => r.PeriodoFin)
            .Select(r => new RepartoListado(
                r.Id, r.PeriodoInicio, r.PeriodoFin, r.TotalRepartido, r.Inquilinos.Sum(l => l.Cuota), r.Inquilinos.Count, r.FechaCreacion))
            .ToList();
    }

    public async Task<RepartoGuardado?> ObtenerAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var r = await db.Repartos
            .AsNoTracking()
            .Include(x => x.Lugar)
            .Include(x => x.Inquilinos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (r is null)
        {
            return null;
        }

        return new RepartoGuardado(
            r.Id, r.LugarId, r.Lugar!.Nombre, r.Lugar.EsDemo, r.PeriodoInicio, r.PeriodoFin, r.TotalRepartido, r.OrigenTotal,
            r.Modo, r.KwhPrincipal, r.FechaCreacion,
            r.Inquilinos
                .OrderBy(l => l.Id)
                .Select(l => new LineaRepartoGuardada(l.InquilinoId, l.NombreInquilino, l.LecturaAnterior, l.LecturaActual, l.Kwh, l.Cuota))
                .ToList());
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Repartos.Where(r => r.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
    }

    private static ResultadoReparto Calcular(EntradaReparto entrada, decimal kwhPrincipal)
        => Repartidor.Calcular(
            entrada.Total,
            entrada.Modo,
            kwhPrincipal,
            entrada.Lecturas.Select(l => new LecturasSubmedidor(l.Anterior, l.Actual)).ToList());

    private static async Task<decimal> KwhPrincipalAsync(VoltiaDbContext db, EntradaReparto entrada, CancellationToken cancellationToken)
        => await db.Periodos
            .Where(p => p.LugarId == entrada.LugarId && p.Inicio == entrada.PeriodoInicio && p.Fin == entrada.PeriodoFin)
            .Select(p => (decimal?)p.KwhTotales)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("El periodo ya no existe.");
}
