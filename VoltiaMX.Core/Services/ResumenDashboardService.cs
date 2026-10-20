using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class ResumenDashboardService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    IProyeccionService proyeccion,
    IDacService dac,
    IPresupuestoInversoService presupuesto) : IResumenDashboardService
{
    private const int PeriodosCerradosEnHistorial = 5;
    private const int MesesDelHistorialDac = 12;

    public async Task<ResumenDashboard?> ObtenerAsync(DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugares = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Periodos)
            .Include(l => l.Lecturas)
            .AsSplitQuery()
            .OrderBy(l => l.Id)
            .ToListAsync(cancellationToken);

        var lugar = LugarPrincipal.Elegir(lugares);
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

        // El periodo actual no avanza solo: se cierra al registrar la lectura en o después del corte.
        DateOnly? ultimoCorte = cerrados.Count > 0 ? cerrados.Max(p => p.Fin) : null;
        var primeraLectura = lugar.Lecturas.Count > 0 ? lugar.Lecturas.Min(l => l.Fecha) : hoy;
        var ventana = VentanaPeriodo.Calcular(Math.Clamp(lugar.DiaCorte, 1, 31), meses, ultimoCorte, primeraLectura);
        var (inicio, fin) = (ventana.Inicio, ventana.Fin);

        var diasDelPeriodo = ventana.Dias;
        var (kwhHastaHoy, diasMedidos) = ConsumoDelPeriodo(lugar.Lecturas, ventana, hoy);
        var proyectado = kwhHastaHoy is decimal kwh
            ? proyeccion.Proyectar(kwh, diasMedidos, diasDelPeriodo, tarifa, meses)
            : null;

        var resumen = new ResumenDashboard
        {
            NombreLugar = lugar.Nombre,
            TipoLugar = lugar.Tipo,
            NombreTarifa = tarifa.Nombre,
            TarifaPorConfirmar = lugar.TarifaPorConfirmar,
            EsDemo = lugar.EsDemo,
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
            Historial = Historial(cerrados, inicio, fin, proyectado),
            VariacionVsPeriodoAnterior = cerrados.Count > 0 && cerrados[^1].KwhTotales > 0m
                ? proyectado?.KwhProyectados / cerrados[^1].KwhTotales - 1
                : null,
        };

        resumen = ConDac(resumen, cerrados, meses, tarifa);
        return presupuestoMax is decimal monto
            ? ConPresupuesto(resumen, monto, tarifa, diasMedidos)
            : resumen;
    }

    /// <summary>
    /// kWh desde la lectura base (la de corte del periodo anterior o, si no hay, la primera del
    /// periodo) hasta la última lectura del periodo de hoy o antes. Null si no hay dos lecturas que lo midan.
    /// </summary>
    private static (decimal? Kwh, int DiasMedidos) ConsumoDelPeriodo(IEnumerable<Lectura> lecturas, Ventana ventana, DateOnly hoy)
    {
        var corteAnterior = ventana.Inicio.AddDays(-1);
        var delPeriodo = lecturas
            .Where(l => l.Fecha >= corteAnterior && l.Fecha <= ventana.Fin && l.Fecha <= hoy)
            .OrderBy(l => l.Fecha)
            .ToList();
        var lecturaBase = delPeriodo.FirstOrDefault();
        var ultima = delPeriodo.LastOrDefault();
        if (lecturaBase is null || ultima is null || ultima.Fecha <= lecturaBase.Fecha)
        {
            return (null, 0);
        }

        var dias = Math.Min(ultima.Fecha.DayNumber - lecturaBase.Fecha.DayNumber, ventana.Dias);
        return (ultima.ValorKwh - lecturaBase.ValorKwh, dias);
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

    // El DAC se evalúa con consumo mensual: cada periodo se reparte en partes iguales entre sus
    // meses y cuentan los últimos 12.
    private ResumenDashboard ConDac(ResumenDashboard resumen, List<Periodo> cerrados, int meses, Tarifa tarifa)
    {
        if (tarifa.LimiteDacKwhMensual is not decimal limite || cerrados.Count == 0)
        {
            return resumen;
        }

        var mensuales = cerrados
            .SelectMany(p => Enumerable.Repeat(p.KwhTotales / meses, meses))
            .TakeLast(MesesDelHistorialDac)
            .ToList();

        return resumen with { Dac = dac.Evaluar(mensuales, limite), LimiteDacKwhMensual = limite };
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

        decimal? variacionRitmo = null;
        if (resumen.PromedioDiarioKwh is decimal promedio && resultado.KwhMaximosPeriodo > 0m)
        {
            variacionRitmo = promedio / (resultado.KwhMaximosPeriodo / resumen.DiasDelPeriodo) - 1;
        }

        return resumen with
        {
            PresupuestoMax = monto,
            KwhPorDiaRestante = resultado.KwhPorDiaRestante,
            DiferenciaContraPresupuesto = resumen.MontoProyectado - monto,
            VariacionRitmoPresupuesto = variacionRitmo,
        };
    }
}
