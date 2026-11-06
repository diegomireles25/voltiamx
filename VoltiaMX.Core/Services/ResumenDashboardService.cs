using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class ResumenDashboardService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    IProyeccionService proyeccion,
    IDacService dac,
    IPresupuestoInversoService presupuesto,
    ITarifaCalculator calculadora) : IResumenDashboardService
{
    private const int PeriodosCerradosEnHistorial = 5;

    public async Task<ResumenDashboard?> ObtenerAsync(DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugares = await ConDatos(db).OrderBy(l => l.Id).ToListAsync(cancellationToken);
        return await ResumirAsync(db, LugarPrincipal.Elegir(lugares), hoy, cancellationToken);
    }

    public async Task<ResumenDashboard?> ObtenerDeLugarAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await ConDatos(db).FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);
        return await ResumirAsync(db, lugar, hoy, cancellationToken);
    }

    private static IQueryable<Lugar> ConDatos(VoltiaDbContext db) => db.Lugares
        .AsNoTracking()
        .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
        .Include(l => l.Periodos)
        .Include(l => l.Lecturas)
        .AsSplitQuery();

    private async Task<ResumenDashboard?> ResumirAsync(VoltiaDbContext db, Lugar? lugar, DateOnly hoy, CancellationToken cancellationToken)
    {
        if (lugar?.Tarifa is null)
        {
            return null;
        }

        var presupuestoMax = await db.Objetivos
            .OrderBy(o => o.Id)
            .Select(o => o.PresupuestoMax)
            .FirstOrDefaultAsync(cancellationToken);

        return Calcular(lugar, lugar.Tarifa, presupuestoMax, hoy);
    }

    public async Task<IReadOnlyList<ResumenPropiedad>> ObtenerPropiedadesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var propiedades = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Periodos)
            .Where(l => l.Tipo == TipoLugar.Propiedad && !l.EsDemo)
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);

        return propiedades
            .Select(l => (l.Nombre, Ultimo: l.Periodos.MaxBy(p => p.Fin)))
            .Select(p => new ResumenPropiedad(p.Nombre, p.Ultimo?.Fin, p.Ultimo?.KwhTotales, p.Ultimo?.Monto))
            .ToList();
    }

    private ResumenDashboard Calcular(Lugar lugar, Tarifa tarifa, decimal? presupuestoMax, DateOnly hoy)
    {
        var cerrados = lugar.Periodos.OrderBy(p => p.Inicio).ToList();
        var meses = lugar.MesesPorPeriodo;

        var ventana = PeriodoActual.Ventana(lugar, hoy);
        var (inicio, fin) = (ventana.Inicio, ventana.Fin);

        var diasDelPeriodo = ventana.Dias;
        var (kwhHastaHoy, diasMedidos) = PeriodoActual.Consumo(PeriodoActual.LecturasMedidas(lugar.Lecturas, ventana, hoy), ventana);
        var proyectado = kwhHastaHoy is decimal kwh
            ? proyeccion.Proyectar(kwh, diasMedidos, diasDelPeriodo, tarifa, meses)
            : null;

        var resumen = new ResumenDashboard
        {
            LugarId = lugar.Id,
            NombreLugar = lugar.Nombre,
            TipoLugar = lugar.Tipo,
            NombreTarifa = tarifa.Nombre,
            TarifaPorConfirmar = lugar.TarifaPorConfirmar,
            EsDemo = lugar.EsDemo,
            PreciosDeEjemplo = tarifa.EsDeEjemplo,
            InicioPeriodo = inicio,
            FinPeriodo = fin,
            MesesDelPeriodo = meses,
            DiaDelPeriodo = Math.Min(hoy.DayNumber - inicio.DayNumber + 1, diasDelPeriodo),
            DiasDelPeriodo = diasDelPeriodo,
            DiasParaCorte = fin.DayNumber - hoy.DayNumber,
            PeriodoTerminado = hoy >= fin,
            KwhHastaHoy = kwhHastaHoy,
            KwhProyectados = proyectado?.KwhProyectados,
            MontoProyectado = proyectado?.MontoProyectado,
            PromedioDiarioKwh = proyectado?.PromedioDiarioKwh,
            DesgloseProyectado = proyectado?.Calculo,
            Historial = Historial(cerrados, inicio, fin, proyectado),
            EstadoInicio = EstadoInicio.Evaluar(cerrados.Count, lugar.Lecturas.Count, inicio, kwhHastaHoy is not null, hoy),
            UltimoReciboEstimado = cerrados.Count > 0 && cerrados[^1].Estimado,
            FechaCorteUltimoRecibo = cerrados.Count > 0 ? cerrados[^1].Fin : null,
            // D6: el recibo anterior con la tarifa de hoy, para que la comparación dependa solo del consumo.
            MontoReciboAnterior = cerrados.Count > 0 ? calculadora.Calcular(cerrados[^1].KwhTotales, tarifa, meses).Total : null,
            VariacionVsPeriodoAnterior = cerrados.Count > 0 && cerrados[^1].KwhTotales > 0m
                ? proyectado?.KwhProyectados / cerrados[^1].KwhTotales - 1
                : null,
        };

        resumen = ConDac(resumen, cerrados, meses, tarifa);
        return presupuestoMax is decimal monto
            ? ConPresupuesto(resumen, monto, tarifa, diasMedidos)
            : resumen;
    }

    private static IReadOnlyList<PeriodoHistorial> Historial(
        List<Periodo> cerrados, DateOnly inicio, DateOnly fin, ResultadoProyeccion? proyectado)
    {
        var historial = cerrados
            .TakeLast(PeriodosCerradosEnHistorial)
            .Select(p => new PeriodoHistorial(p.Inicio, p.Fin, p.KwhTotales, p.Monto, EsActual: false))
            .ToList();

        if (proyectado is not null)
        {
            historial.Add(new PeriodoHistorial(inicio, fin, proyectado.KwhProyectados, proyectado.MontoProyectado, EsActual: true));
        }

        return historial;
    }

    // El DAC se evalúa con consumo mensual (HistorialMensualDac); el simulador usa el mismo historial.
    private ResumenDashboard ConDac(ResumenDashboard resumen, List<Periodo> cerrados, int meses, Tarifa tarifa)
    {
        if (tarifa.LimiteDacKwhMensual is not decimal limite || cerrados.Count == 0)
        {
            return resumen;
        }

        var mensuales = HistorialMensualDac.Desde(cerrados, meses);

        return resumen with
        {
            Dac = dac.Evaluar(mensuales, limite),
            LimiteDacKwhMensual = limite,
            MesesEnPromedioDac = mensuales.Count,
        };
    }

    private ResumenDashboard ConPresupuesto(ResumenDashboard resumen, decimal monto, Tarifa tarifa, int diasMedidos)
    {
        var resultado = presupuesto.Calcular(
            monto,
            tarifa,
            resumen.DiasDelPeriodo,
            resumen.MesesDelPeriodo,
            kwhConsumidos: resumen.KwhHastaHoy ?? 0m,
            diasTranscurridos: diasMedidos);

        var permitidoPorDia = resultado.KwhMaximosPeriodo / resumen.DiasDelPeriodo;
        decimal? variacionRitmo = null;
        if (resumen.PromedioDiarioKwh is decimal promedio && resultado.KwhMaximosPeriodo > 0m)
        {
            variacionRitmo = promedio / permitidoPorDia - 1;
        }

        return resumen with
        {
            PresupuestoMax = monto,
            KwhPorDiaRestante = resultado.KwhPorDiaRestante,
            KwhMaximosPeriodo = resultado.KwhMaximosPeriodo,
            KwhRestantes = resultado.KwhRestantes,
            KwhPorDiaPermitido = permitidoPorDia,
            PresupuestoCubreCargoFijo = resultado.CubreCargoFijo,
            DiferenciaContraPresupuesto = resumen.MontoProyectado - monto,
            VariacionRitmoPresupuesto = variacionRitmo,
        };
    }
}
