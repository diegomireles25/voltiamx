using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Historial por periodo de un lugar con las mismas reglas que el Dashboard: periodo actual de
/// <see cref="PeriodoActual"/>, proyección de <see cref="IProyeccionService"/> y tope de kWh de
/// <see cref="IPresupuestoInversoService"/>.
/// </summary>
public sealed class HistorialService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    IProyeccionService proyeccion,
    IPresupuestoInversoService presupuesto) : IHistorialService
{
    public async Task<HistorialLugar?> ObtenerAsync(int lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Periodos)
            .Include(l => l.Lecturas)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);
        if (lugar?.Tarifa is not { } tarifa)
        {
            return null;
        }

        var presupuestoMax = await db.Objetivos
            .OrderBy(o => o.Id)
            .Select(o => o.PresupuestoMax)
            .FirstOrDefaultAsync(cancellationToken);

        var periodos = lugar.Periodos
            .OrderBy(p => p.Inicio)
            .Select(p => new PeriodoDeHistorial(
                p.Inicio, p.Fin, p.KwhTotales, p.Monto, p.Estimado, p.MontoRecibo,
                p.KwhTotales / (p.Fin.DayNumber - p.Inicio.DayNumber + 1),
                EsActual: false))
            .ToList();

        var ventana = PeriodoActual.Ventana(lugar, hoy);
        var medidas = PeriodoActual.LecturasMedidas(lugar.Lecturas, ventana, hoy);
        var (kwhMedidos, diasMedidos) = PeriodoActual.Consumo(medidas, ventana);
        if (kwhMedidos is decimal kwh)
        {
            var proyectado = proyeccion.Proyectar(kwh, diasMedidos, ventana.Dias, tarifa, lugar.MesesPorPeriodo);
            periodos.Add(new PeriodoDeHistorial(
                ventana.Inicio, ventana.Fin, proyectado.KwhProyectados, proyectado.MontoProyectado,
                Estimado: false, MontoRecibo: null, proyectado.PromedioDiarioKwh, EsActual: true));
        }

        var acumulado = medidas.Select(l => new PuntoAcumulado(l.Fecha, l.ValorKwh - medidas[0].ValorKwh)).ToList();

        decimal? tope = presupuestoMax is decimal monto
            ? presupuesto.Calcular(monto, tarifa, ventana.Dias, lugar.MesesPorPeriodo, kwhMedidos ?? 0m, diasMedidos).KwhMaximosPeriodo
            : null;

        return new HistorialLugar(periodos, ventana, acumulado, tope);
    }
}
