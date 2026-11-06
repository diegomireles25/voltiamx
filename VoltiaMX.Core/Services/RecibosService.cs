using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class RecibosService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    IPresupuestoInversoService inverso,
    ITarifaCalculator calculadora) : IRecibosService
{
    public async Task<DateOnly?> UltimoCorteAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var diaCorte = await db.Lugares.Where(l => l.Id == lugarId).Select(l => (int?)l.DiaCorte).FirstOrDefaultAsync(cancellationToken);
        return diaCorte is int dia ? VentanaPeriodo.CorteEnOAntesDe(dia, hoy) : null;
    }

    public Task<ResultadoReciboPorMonto> PrevisualizarPorMontoAsync(
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, int? mesesPorPeriodo = null,
        CancellationToken cancellationToken = default)
        => ProcesarAsync(lugarId, total, fechaCorte, porcentaje, hoy, mesesPorPeriodo, guardar: false, cancellationToken);

    public Task<ResultadoReciboPorMonto> AgregarPorMontoAsync(
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, int? mesesPorPeriodo = null,
        CancellationToken cancellationToken = default)
        => ProcesarAsync(lugarId, total, fechaCorte, porcentaje, hoy, mesesPorPeriodo, guardar: true, cancellationToken);

    public async Task<bool> TieneRecibosAsync(int lugarId, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Periodos.AnyAsync(p => p.LugarId == lugarId, cancellationToken);
    }

    public async Task<EstimacionPorMonto> EstimarSinLugarAsync(
        int? tarifaId, int meses, decimal total, decimal porcentaje, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var tarifas = db.Tarifas.AsNoTracking().Include(t => t.Rangos);
        var tarifa = tarifaId is int id
            ? await tarifas.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            : await tarifas.FirstOrDefaultAsync(t => t.Nombre == PerfilService.TarifaPorDefecto, cancellationToken)
                ?? await tarifas.OrderBy(t => t.Id).FirstOrDefaultAsync(cancellationToken);
        if (tarifa is null)
        {
            return new EstimacionPorMonto(ErrorReciboPorMonto.LugarNoExiste, 0m, 0m, 0m);
        }

        // Comercial siempre es mensual, igual que al guardar el onboarding.
        var m = tarifa.Tipo == TipoTarifa.Comercial ? 1 : Math.Clamp(meses, 1, 2);
        return EstimadorPorMonto.Estimar(inverso, calculadora, tarifa, m, 30 * m, total, porcentaje);
    }

    private async Task<ResultadoReciboPorMonto> ProcesarAsync(
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, int? mesesPorPeriodo, bool guardar,
        CancellationToken cancellationToken)
    {
        if (EstimadorPorMonto.Validar(total, porcentaje) is { } invalido)
        {
            return ResultadoReciboPorMonto.Rechazado(invalido);
        }

        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Periodos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);
        if (lugar?.Tarifa is not { } tarifa)
        {
            return ResultadoReciboPorMonto.Rechazado(ErrorReciboPorMonto.LugarNoExiste);
        }

        // D3: en el primer recibo cuenta la respuesta a "¿Cada cuánto te llega el recibo?"; después, la del lugar.
        if (lugar.Periodos.Count == 0 && mesesPorPeriodo is 1 or 2)
        {
            lugar.MesesPorPeriodo = mesesPorPeriodo.Value;
        }

        // El recibo cubre los meses del periodo que terminan en la fecha de corte (igual que en el onboarding).
        var meses = Math.Max(lugar.MesesPorPeriodo, 1);
        var fin = fechaCorte ?? VentanaPeriodo.CorteEnOAntesDe(lugar.DiaCorte, hoy);
        var inicio = fin.AddMonths(-meses).AddDays(1);
        if (ValidadorFechaCorte.Validar(fin, hoy) is not null)
        {
            return ResultadoReciboPorMonto.Rechazado(ErrorReciboPorMonto.FechaFutura, inicio, fin);
        }

        if (lugar.Periodos.Any(p => p.Inicio <= fin && inicio <= p.Fin))
        {
            return ResultadoReciboPorMonto.Rechazado(ErrorReciboPorMonto.Traslape, inicio, fin);
        }

        var estimacion = EstimadorPorMonto.Estimar(
            inverso, calculadora, tarifa, meses, fin.DayNumber - inicio.DayNumber + 1, total, porcentaje);
        if (estimacion.Error is { } error)
        {
            return ResultadoReciboPorMonto.Rechazado(error, inicio, fin);
        }

        if (guardar)
        {
            lugar.Periodos.Add(new Periodo
            {
                Inicio = inicio,
                Fin = fin,
                KwhTotales = estimacion.Kwh,
                Monto = estimacion.Monto,
                Estimado = true,
                MontoRecibo = total,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ResultadoReciboPorMonto(null, estimacion.Kwh, estimacion.Monto, inicio, fin);
    }
}
