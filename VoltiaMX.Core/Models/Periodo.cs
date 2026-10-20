namespace VoltiaMX.Core.Models;

/// <summary>
/// Periodo de facturación cerrado de un lugar (lo que dice el recibo).
/// </summary>
public class Periodo
{
    public int Id { get; set; }

    public int LugarId { get; set; }

    public Lugar? Lugar { get; set; }

    public DateOnly Inicio { get; set; }

    public DateOnly Fin { get; set; }

    public decimal KwhTotales { get; set; }

    /// <summary>
    /// Calculado con la tarifa (sin IVA ni DAP).
    /// </summary>
    public decimal Monto { get; set; }

    /// <summary>
    /// La lectura de corte se interpoló o el periodo se midió desde una lectura posterior a su inicio.
    /// </summary>
    public bool Estimado { get; set; }

    /// <summary>
    /// Monto real del recibo, si la persona lo escribió. Solo se guarda; no entra en los cálculos.
    /// </summary>
    public decimal? MontoRecibo { get; set; }
}
