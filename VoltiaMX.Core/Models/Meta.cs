namespace VoltiaMX.Core.Models;

/// <summary>
/// Meta de ahorro de un lugar: reducir el consumo <see cref="PorcentajeObjetivo"/> % contra el periodo
/// anterior, en cada periodo desde <see cref="FechaInicio"/>. Una por lugar; el progreso no se guarda.
/// </summary>
public class Meta
{
    public int Id { get; set; }

    public int LugarId { get; set; }

    public Lugar? Lugar { get; set; }

    public int PorcentajeObjetivo { get; set; }

    /// <summary>
    /// Fecha en que se creó o se reactivó: los periodos que empiezan antes no cuentan en el historial.
    /// </summary>
    public DateOnly FechaInicio { get; set; }

    public bool Activa { get; set; }
}
