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

    /// <summary>
    /// Lo que marca el medidor (acumulado, la "lectura actual" del recibo). El consumo es la
    /// diferencia entre dos lecturas.
    /// </summary>
    public decimal ValorKwh { get; set; }

    public OrigenLectura Origen { get; set; }

    /// <summary>
    /// Lectura de corte calculada por interpolación al cerrar un periodo; nadie la capturó.
    /// </summary>
    public bool EsEstimada { get; set; }
}
