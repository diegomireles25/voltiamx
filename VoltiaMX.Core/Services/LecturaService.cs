using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Registra lecturas del medidor (acumuladas) y cierra los periodos cuya fecha de corte ya pasó.
/// </summary>
public sealed class LecturaService(IDbContextFactory<VoltiaDbContext> fabrica, ITarifaCalculator calculadora) : ILecturaService
{
    public async Task<ResultadoLectura> PrevisualizarAsync(
        NuevaLectura entrada, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await CargarLugarAsync(db.Lugares.AsNoTracking(), entrada.LugarId, cancellationToken);
        return lugar is null
            ? ResultadoLectura.Rechazada(MotivoRechazoLectura.LugarNoExiste)
            : Planear(lugar, entrada, hoy).Resultado;
    }

    public async Task<ResultadoLectura> GuardarAsync(
        NuevaLectura entrada, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        await using var transaccion = await db.Database.BeginTransactionAsync(cancellationToken);

        var lugar = await CargarLugarAsync(db.Lugares, entrada.LugarId, cancellationToken);
        if (lugar is null)
        {
            return ResultadoLectura.Rechazada(MotivoRechazoLectura.LugarNoExiste);
        }

        var plan = Planear(lugar, entrada, hoy);
        if (!plan.Resultado.Aceptada)
        {
            return plan.Resultado;
        }

        if (plan.Reemplazada is { } existente)
        {
            existente.ValorKwh = entrada.ValorKwh;
            existente.Origen = entrada.Origen;
            existente.EsEstimada = false;
        }
        else
        {
            lugar.Lecturas.Add(plan.Nueva);
        }

        lugar.Lecturas.AddRange(plan.CortesEstimados);
        lugar.Periodos.AddRange(plan.Periodos);

        await db.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
        return plan.Resultado;
    }

    public async Task<ResultadoEliminacion> EliminarUltimaAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lecturas = await db.Lecturas.Where(l => l.LugarId == lugarId).ToListAsync(cancellationToken);
        var ultima = lecturas.MaxBy(l => l.Fecha);
        if (ultima is null)
        {
            return ResultadoEliminacion.SinLecturas;
        }

        if (await db.Periodos.AnyAsync(p => p.LugarId == lugarId && p.Fin >= ultima.Fecha, cancellationToken))
        {
            return ResultadoEliminacion.EsCorteDePeriodoCerrado;
        }

        db.Lecturas.Remove(ultima);
        await db.SaveChangesAsync(cancellationToken);
        return ResultadoEliminacion.Eliminada;
    }

    public async Task<IReadOnlyList<LecturaListada>> ListarAsync(
        int lugarId, int cantidad, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cantidad);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lecturas = (await db.Lecturas.AsNoTracking().Where(l => l.LugarId == lugarId).ToListAsync(cancellationToken))
            .OrderBy(l => l.Fecha)
            .ToList();
        var cortes = (await db.Periodos.Where(p => p.LugarId == lugarId).Select(p => p.Fin).ToListAsync(cancellationToken))
            .ToHashSet();

        // El consumo se calcula con la anterior aunque quede fuera de las que se devuelven.
        return lecturas
            .Select((l, i) => new LecturaListada(
                l.Fecha,
                l.ValorKwh,
                i == 0 ? null : l.ValorKwh - lecturas[i - 1].ValorKwh,
                l.EsEstimada,
                cortes.Contains(l.Fecha)))
            .Reverse()
            .Take(cantidad)
            .ToList();
    }

    public async Task<LugarDeLecturas?> ObtenerLugarPrincipalAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugares = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Periodos)
            .Include(l => l.Lecturas)
            .AsSplitQuery()
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);

        return LugarPrincipal.Elegir(lugares) is { } lugar ? new LugarDeLecturas(lugar.Id, lugar.Nombre, lugar.EsDemo) : null;
    }

    private static Task<Lugar?> CargarLugarAsync(IQueryable<Lugar> lugares, int lugarId, CancellationToken cancellationToken)
        => lugares
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Lecturas)
            .Include(l => l.Periodos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);

    private sealed record Plan(
        ResultadoLectura Resultado,
        Lectura Nueva,
        Lectura? Reemplazada,
        IReadOnlyList<Lectura> CortesEstimados,
        IReadOnlyList<Periodo> Periodos);

    /// <summary>
    /// Valida la lectura y calcula, sin tocar la base, qué se guardaría: la lectura, las lecturas de
    /// corte interpoladas y los periodos que se cierran.
    /// </summary>
    private Plan Planear(Lugar lugar, NuevaLectura entrada, DateOnly hoy)
    {
        var nueva = new Lectura
        {
            LugarId = lugar.Id,
            Fecha = entrada.Fecha,
            ValorKwh = entrada.ValorKwh,
            Origen = entrada.Origen,
        };

        Plan Rechazo(MotivoRechazoLectura motivo) => new(ResultadoLectura.Rechazada(motivo), nueva, null, [], []);

        if (entrada.ValorKwh < 0m)
        {
            return Rechazo(MotivoRechazoLectura.ValorNegativo);
        }

        if (entrada.Fecha > hoy)
        {
            return Rechazo(MotivoRechazoLectura.FechaFutura);
        }

        DateOnly? ultimoCorte = lugar.Periodos.Count > 0 ? lugar.Periodos.Max(p => p.Fin) : null;
        if (entrada.Fecha <= ultimoCorte)
        {
            return Rechazo(MotivoRechazoLectura.PeriodoCerrado);
        }

        // Una lectura por día: la de la misma fecha se reemplaza.
        var existente = lugar.Lecturas.FirstOrDefault(l => l.Fecha == entrada.Fecha);
        var otras = lugar.Lecturas.Where(l => l != existente).OrderBy(l => l.Fecha).ToList();
        if (otras.LastOrDefault(l => l.Fecha < entrada.Fecha) is { } anterior && entrada.ValorKwh < anterior.ValorKwh)
        {
            return Rechazo(MotivoRechazoLectura.MenorQueAnterior);
        }

        if (otras.FirstOrDefault(l => l.Fecha > entrada.Fecha) is { } siguiente && entrada.ValorKwh > siguiente.ValorKwh)
        {
            return Rechazo(MotivoRechazoLectura.MayorQueSiguiente);
        }

        var serie = otras.Append(nueva).OrderBy(l => l.Fecha).ToList();
        var cortes = new List<Lectura>();
        var periodos = new List<Periodo>();
        var sinBase = false;

        var ventana = VentanaPeriodo.Calcular(lugar.DiaCorte, lugar.MesesPorPeriodo, ultimoCorte, serie[0].Fecha);
        while (entrada.Fecha >= ventana.Fin)
        {
            // Base: el corte del periodo anterior; si no hay (primer periodo o periodo del recibo),
            // la primera lectura del periodo, y entonces el periodo quedó medido a medias.
            var corteAnterior = ventana.Inicio.AddDays(-1);
            var lecturaBase = serie.FirstOrDefault(l => l.Fecha == corteAnterior)
                ?? serie.FirstOrDefault(l => l.Fecha >= ventana.Inicio && l.Fecha < ventana.Fin);
            if (lecturaBase is null)
            {
                sinBase = true;
                break;
            }

            var estimado = lecturaBase.Fecha != corteAnterior;
            var corte = serie.FirstOrDefault(l => l.Fecha == ventana.Fin);
            if (corte is null)
            {
                var antes = serie.Last(l => l.Fecha < ventana.Fin);
                var despues = serie.First(l => l.Fecha > ventana.Fin);
                corte = new Lectura
                {
                    LugarId = lugar.Id,
                    Fecha = ventana.Fin,
                    ValorKwh = Interpolar(antes, despues, ventana.Fin),
                    Origen = entrada.Origen,
                    EsEstimada = true,
                };
                cortes.Add(corte);
                serie.Insert(serie.IndexOf(despues), corte);
                estimado = true;
            }

            var kwh = corte.ValorKwh - lecturaBase.ValorKwh;
            periodos.Add(new Periodo
            {
                LugarId = lugar.Id,
                Inicio = ventana.Inicio,
                Fin = ventana.Fin,
                KwhTotales = kwh,
                Monto = calculadora.Calcular(kwh, lugar.Tarifa!, lugar.MesesPorPeriodo).Total,
                Estimado = estimado,
            });

            ventana = VentanaPeriodo.Calcular(lugar.DiaCorte, lugar.MesesPorPeriodo, ventana.Fin, ventana.Fin);
        }

        // El recibo que la persona tiene a la mano es el del último periodo que se cierra.
        if (entrada.MontoRecibo is decimal montoRecibo && periodos.Count > 0)
        {
            periodos[^1].MontoRecibo = montoRecibo;
        }

        var resultado = new ResultadoLectura
        {
            Reemplaza = existente is not null,
            EsPuntoDePartida = otras.Count == 0,
            SinLecturaBase = sinBase,
            Cierres = periodos
                .Select(p => new CierrePeriodo(p.Inicio, p.Fin, p.KwhTotales, p.Monto, p.Estimado))
                .ToList(),
        };

        return new Plan(resultado, nueva, existente, cortes, periodos);
    }

    /// <summary>
    /// Lectura de corte en línea recta entre la última lectura anterior y la primera posterior,
    /// redondeada a centésimos.
    /// </summary>
    private static decimal Interpolar(Lectura antes, Lectura despues, DateOnly fecha)
    {
        var avance = (decimal)(fecha.DayNumber - antes.Fecha.DayNumber) / (despues.Fecha.DayNumber - antes.Fecha.DayNumber);
        return Math.Round(antes.ValorKwh + (despues.ValorKwh - antes.ValorKwh) * avance, 2, MidpointRounding.AwayFromZero);
    }
}
