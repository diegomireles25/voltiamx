namespace VoltiaMX.Core.Models;

/// <summary>
/// Lectura del medidor tomada en una fecha, capturada a mano o por OCR.
/// </summary>
public class Lectura
{
    public int Id { get; set; }

    public int LugarId { get; set; }

    public Lugar? Lugar { get; set; }

    public DateOnly Fecha { get; set; }

    public decimal ValorKwh { get; set; }

    public OrigenLectura Origen { get; set; }
}
