using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public interface IProyeccionService
{
    /// <summary>
    /// Proyecta de forma lineal el consumo al cierre del periodo a partir de lo acumulado
    /// en <paramref name="diasTranscurridos"/>, y calcula su monto con la tarifa.
    /// Con 0 días transcurridos no hay ritmo que extrapolar y se devuelve el acumulado.
    /// </summary>
    ResultadoProyeccion Proyectar(
        decimal kwhAcumulados, int diasTranscurridos, int diasDelPeriodo, Tarifa tarifa, int mesesDelPeriodo = 1);
}
