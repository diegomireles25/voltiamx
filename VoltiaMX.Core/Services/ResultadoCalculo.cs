namespace VoltiaMX.Core.Services;

/// <summary>
/// kWh cobrados en un rango de la tarifa y su importe.
/// </summary>
public sealed record DesgloseRango(string Nombre, decimal Kwh, decimal PrecioPorKwh, decimal Subtotal);

/// <summary>
/// Importe de un consumo, sin IVA ni DAP y sin redondear.
/// </summary>
public sealed record ResultadoCalculo(
    IReadOnlyList<DesgloseRango> Desglose,
    decimal SubtotalEnergia,
    decimal CargoFijo,
    decimal Total);
