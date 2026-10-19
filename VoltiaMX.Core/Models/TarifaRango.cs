namespace VoltiaMX.Core.Models;

/// <summary>
/// Bloque de consumo mensual de una tarifa (básico, intermedio, excedente...).
/// Cubre los kWh en el intervalo (KwhDesde, KwhHasta]: con Básico 0–75 e Intermedio 75–140,
/// un consumo de 76 kWh cobra 75 en básico y 1 en intermedio.
/// </summary>
public class TarifaRango
{
    public int Id { get; set; }

    public int TarifaId { get; set; }

    public Tarifa? Tarifa { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// kWh mensuales ya cubiertos por los rangos anteriores (0 en el primero).
    /// </summary>
    public decimal KwhDesde { get; set; }

    /// <summary>
    /// Tope mensual incluido del rango. Null en el último rango (sin límite).
    /// </summary>
    public decimal? KwhHasta { get; set; }

    public decimal PrecioPorKwh { get; set; }

    public int Orden { get; set; }
}
