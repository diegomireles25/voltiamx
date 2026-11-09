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
        => ProcesarAsync(lugarId, total, null, fechaCorte, porcentaje, hoy, mesesPorPeriodo, guardar: false, cancellationToken);

    public Task<ResultadoReciboPorMonto> AgregarPorMontoAsync(
        int lugarId, decimal total, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, int? mesesPorPeriodo = null,
        CancellationToken cancellationToken = default)
        => ProcesarAsync(lugarId, total, null, fechaCorte, porcentaje, hoy, mesesPorPeriodo, guardar: true, cancellationToken);

    public Task<ResultadoReciboPorMonto> PrevisualizarConConsumoAsync(
        int lugarId, decimal kwh, decimal importe, DateOnly? fechaCorte, DateOnly hoy, int? mesesPorPeriodo = null,
        CancellationToken cancellationToken = default)
        => ProcesarAsync(
            lugarId, importe, kwh, fechaCorte, EstimadorPorMonto.CargosPorDefecto, hoy, mesesPorPeriodo, guardar: false, cancellationToken);

    public Task<ResultadoReciboPorMonto> AgregarConConsumoAsync(
        int lugarId, decimal kwh, decimal importe, DateOnly? fechaCorte, DateOnly hoy, int? mesesPorPeriodo = null,
        CancellationToken cancellationToken = default)
        => ProcesarAsync(
            lugarId, importe, kwh, fechaCorte, EstimadorPorMonto.CargosPorDefecto, hoy, mesesPorPeriodo, guardar: true, cancellationToken);

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
        int lugarId, decimal total, decimal? kwhLeido, DateOnly? fechaCorte, decimal porcentaje, DateOnly hoy, int? mesesPorPeriodo,
        bool guardar, CancellationToken cancellationToken)
    {
        if (EstimadorPorMonto.Validar(total, porcentaje) is { } invalido)
        {
            return ResultadoReciboPorMonto.Rechazado(invalido);
        }

        if (kwhLeido is <= 0m or > EstimadorPorMonto.ConsumoMaximo)
        {
            return ResultadoReciboPorMonto.Rechazado(ErrorReciboPorMonto.ConsumoFueraDeRango);
        }

        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Periodos)
            .Include(l => l.Lecturas)
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

        // Fase 9i: el recibo termina en su fecha de corte y empieza al día siguiente del corte del recibo anterior
        // (sin anterior, sus meses antes); las fechas reales no caen a meses exactos.
        var meses = Math.Max(lugar.MesesPorPeriodo, 1);
        var fin = fechaCorte ?? VentanaPeriodo.CorteEnOAntesDe(lugar.DiaCorte, hoy);
        if (ValidadorFechaCorte.Validar(fin, hoy) is not null)
        {
            return ResultadoReciboPorMonto.Rechazado(ErrorReciboPorMonto.FechaFutura, fin.AddMonths(-meses).AddDays(1), fin);
        }

        var ubicacion = CortesDeRecibos.Ubicar([.. lugar.Periodos], lugar.Lecturas.Select(l => l.Fecha).ToHashSet(), fin, meses);
        var inicio = ubicacion.Inicio;
        if (ubicacion.Error is { } choque)
        {
            return ResultadoReciboPorMonto.Rechazado(choque, inicio, fin, ubicacion.ChoqueInicio, ubicacion.ChoqueFin);
        }

        // Fase 9k (D6): con el consumo leído del recibo no se estima; su monto es el cálculo con la tarifa del lugar.
        var estimacion = kwhLeido is decimal kwh
            ? new EstimacionPorMonto(null, kwh, calculadora.Calcular(kwh, tarifa, meses).Total, total, meses)
            : EstimadorPorMonto.Estimar(inverso, calculadora, tarifa, meses, fin.DayNumber - inicio.DayNumber + 1, total, porcentaje);
        if (estimacion.Error is { } error)
        {
            return ResultadoReciboPorMonto.Rechazado(error, inicio, fin);
        }

        if (guardar)
        {
            // El recibo siguiente (por monto) empieza al día siguiente del corte nuevo; sus kWh y su monto no cambian.
            if (ubicacion is { Siguiente: { } siguiente, NuevoInicioSiguiente: DateOnly nuevoInicio })
            {
                siguiente.Inicio = nuevoInicio;
            }

            lugar.Periodos.Add(new Periodo
            {
                Inicio = inicio,
                Fin = fin,
                KwhTotales = estimacion.Kwh,
                Monto = estimacion.Monto,
                Estimado = kwhLeido is null,
                MontoRecibo = total,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return new ResultadoReciboPorMonto(null, estimacion.Kwh, estimacion.Monto, inicio, fin);
    }
}
