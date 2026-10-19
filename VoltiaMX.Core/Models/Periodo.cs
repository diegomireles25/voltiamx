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

    public decimal Monto { get; set; }
}
