namespace VoltiaMX.Core.Models;

/// <summary>
/// Inquilino (o unidad) de un lugar con su propio submedidor. Solo se guarda un nombre o unidad:
/// son datos de terceros y viajan en los respaldos y en los PDF.
/// </summary>
public class Inquilino
{
    public int Id { get; set; }

    public int LugarId { get; set; }

    public Lugar? Lugar { get; set; }

    /// <summary>
    /// Nombre o unidad ("Depto 2"); único por lugar sin distinguir mayúsculas.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// False = dado de baja: ya no entra en repartos nuevos, pero sigue en el historial.
    /// </summary>
    public bool Activo { get; set; } = true;

    public DateOnly FechaAlta { get; set; }
}
