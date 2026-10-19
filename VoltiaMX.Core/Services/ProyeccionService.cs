using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class ProyeccionService(ITarifaCalculator calculadora) : IProyeccionService
{
    public ResultadoProyeccion Proyectar(
        decimal kwhAcumulados, int diasTranscurridos, int diasDelPeriodo, Tarifa tarifa, int mesesDelPeriodo = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(kwhAcumulados);
        ArgumentOutOfRangeException.ThrowIfLessThan(diasDelPeriodo, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(diasTranscurridos);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(diasTranscurridos, diasDelPeriodo);

        var sinDatos = diasTranscurridos == 0;
        var promedioDiario = sinDatos ? 0m : kwhAcumulados / diasTranscurridos;
        // Se multiplica antes de dividir para no arrastrar el redondeo del promedio.
        var kwhProyectados = sinDatos ? kwhAcumulados : kwhAcumulados * diasDelPeriodo / diasTranscurridos;

        return new ResultadoProyeccion(
            promedioDiario,
            kwhProyectados,
            calculadora.Calcular(kwhProyectados, tarifa, mesesDelPeriodo));
    }
}
