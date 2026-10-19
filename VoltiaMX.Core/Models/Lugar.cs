namespace VoltiaMX.Core.Models;

/// <summary>
/// Casa, negocio o propiedad en renta con su propio medidor y tarifa.
/// </summary>
public class Lugar
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public TipoLugar Tipo { get; set; }

    public int TarifaId { get; set; }

    public Tarifa? Tarifa { get; set; }

    /// <summary>
    /// Día del mes en que CFE cierra el periodo de facturación.
    /// </summary>
    public int DiaCorte { get; set; }

    public string? Estado { get; set; }

    public string? Municipio { get; set; }

    /// <summary>
    /// La persona eligió "No sé mi tarifa": se usa la tarifa 1 hasta que la confirme en su recibo.
    /// </summary>
    public bool TarifaPorConfirmar { get; set; }

    /// <summary>
    /// Lugar con datos de ejemplo ("Cargar datos de demo"). Se borra completo, sin tocar los datos reales.
    /// </summary>
    public bool EsDemo { get; set; }

    public List<Lectura> Lecturas { get; set; } = [];

    public List<Periodo> Periodos { get; set; } = [];
}
