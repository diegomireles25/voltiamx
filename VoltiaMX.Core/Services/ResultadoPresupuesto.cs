namespace VoltiaMX.Core.Services;

/// <summary>
/// Cuánto se puede consumir sin pasar de un monto máximo. Los kWh del periodo son enteros
/// y los valores por día se truncan a 2 decimales para no rebasar el presupuesto.
/// </summary>
/// <param name="CubreCargoFijo">False si el monto ni siquiera alcanza para el cargo fijo.</param>
/// <param name="KwhRestantes">kWh que aún caben en el presupuesto descontando lo ya consumido.</param>
/// <param name="KwhPorDiaRestante">KwhRestantes repartidos en los días que faltan del periodo.</param>
public sealed record ResultadoPresupuesto(
    decimal KwhMaximosPeriodo,
    decimal KwhMaximosPorDia,
    bool CubreCargoFijo,
    decimal KwhRestantes,
    decimal KwhPorDiaRestante);
