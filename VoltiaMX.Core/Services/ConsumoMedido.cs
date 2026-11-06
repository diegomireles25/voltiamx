using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Consumo mensual que mide el medidor del lugar: la referencia contra la que se comparan los aparatos.
/// </summary>
public static class ConsumoMedido
{
    /// <summary>
    /// kWh del periodo actual proyectados a todo el periodo (igual que el Dashboard) entre los meses
    /// del periodo; si el periodo actual no tiene lectura base, el último periodo cerrado entre sus
    /// meses; null si no hay ninguno de los dos. Espera el lugar con lecturas y periodos cargados.
    /// </summary>
    public static decimal? KwhPorMes(Lugar lugar, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(lugar);
        var meses = Math.Max(lugar.MesesPorPeriodo, 1);

        var ventana = PeriodoActual.Ventana(lugar, hoy);
        var (kwh, diasMedidos) = PeriodoActual.Consumo(PeriodoActual.LecturasMedidas(lugar.Lecturas, ventana, hoy), ventana);
        if (kwh is decimal medidos && diasMedidos > 0)
        {
            // Misma proyección que ProyeccionService: se multiplica antes de dividir.
            return medidos * ventana.Dias / diasMedidos / meses;
        }

        return lugar.Periodos.MaxBy(p => p.Fin) is { } ultimo ? ultimo.KwhTotales / meses : null;
    }

    /// <summary>
    /// El consumo de <see cref="KwhPorMes"/> es aproximado: no hay lecturas que midan el recibo actual y sale del
    /// último recibo, que se calculó con lo que se pagó (recibo por monto, <see cref="Periodo.Estimado"/>).
    /// </summary>
    public static bool EsAproximado(Lugar lugar, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(lugar);
        var ventana = PeriodoActual.Ventana(lugar, hoy);
        var (kwh, diasMedidos) = PeriodoActual.Consumo(PeriodoActual.LecturasMedidas(lugar.Lecturas, ventana, hoy), ventana);
        if (kwh is not null && diasMedidos > 0)
        {
            return false;
        }

        return lugar.Periodos.MaxBy(p => p.Fin)?.Estimado == true;
    }
}
