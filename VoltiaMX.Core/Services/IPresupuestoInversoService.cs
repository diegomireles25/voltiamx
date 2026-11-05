using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

// Lo usa el recibo por monto (EstimadorPorMonto, Fase 8) para saber cuántos kWh caben en un monto.
public interface IPresupuestoInversoService
{
    /// <summary>
    /// Inverso del cálculo por rangos: los kWh máximos del periodo cuyo importe no pasa de
    /// <paramref name="montoMaximo"/>. Con <paramref name="kwhConsumidos"/> y
    /// <paramref name="diasTranscurridos"/> (0 por defecto, inicio del periodo) calcula además
    /// lo que queda por consumir y su reparto diario.
    /// </summary>
    ResultadoPresupuesto Calcular(
        decimal montoMaximo,
        Tarifa tarifa,
        int diasDelPeriodo,
        int mesesDelPeriodo = 1,
        decimal kwhConsumidos = 0m,
        int diasTranscurridos = 0);
}
