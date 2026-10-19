namespace VoltiaMX.Core.Services;

/// <summary>
/// Consumo y monto estimados al cierre del periodo si se mantiene el ritmo actual.
/// </summary>
public sealed record ResultadoProyeccion(
    decimal PromedioDiarioKwh,
    decimal KwhProyectados,
    ResultadoCalculo Calculo)
{
    public decimal MontoProyectado => Calculo.Total;
}
