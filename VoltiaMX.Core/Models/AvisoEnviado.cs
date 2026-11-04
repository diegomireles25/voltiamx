namespace VoltiaMX.Core.Models;

public enum TipoAviso
{
    Lectura,
    Dac,
    Meta,
    Presupuesto,
}

/// <summary>
/// Un aviso que ya se mostró como notificación: cada clave se avisa una sola vez por lugar.
/// </summary>
public class AvisoEnviado
{
    public int Id { get; set; }

    public int LugarId { get; set; }

    public Lugar? Lugar { get; set; }

    public TipoAviso Tipo { get; set; }

    /// <summary>
    /// Qué se avisó y de qué periodo (p. ej. "Lectura:2026-10-22:faltan" o "Dac:Alto").
    /// </summary>
    public string Clave { get; set; } = string.Empty;

    public DateTime FechaEnvio { get; set; }
}
