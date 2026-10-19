namespace VoltiaMX.Core.Models;

/// <summary>
/// Tarifa de CFE editable por el usuario. Sus rangos y cargos son mensuales;
/// los servicios los escalan según los meses del periodo de facturación.
/// </summary>
public class Tarifa
{
    public int Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public TipoTarifa Tipo { get; set; }

    /// <summary>
    /// Consumo promedio mensual (últimos 12 meses) a partir del cual se pasa a DAC.
    /// Null cuando la tarifa no tiene límite DAC.
    /// </summary>
    public decimal? LimiteDacKwhMensual { get; set; }

    /// <summary>
    /// Cargo fijo que se cobra una vez por cada mes del periodo, sin importar el consumo.
    /// </summary>
    public decimal CargoFijoMensual { get; set; }

    /// <summary>
    /// Fecha desde la que aplican estos precios.
    /// </summary>
    public DateOnly Vigencia { get; set; }

    public List<TarifaRango> Rangos { get; set; } = [];
}
