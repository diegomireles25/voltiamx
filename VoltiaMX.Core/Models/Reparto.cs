namespace VoltiaMX.Core.Models;

/// <summary>
/// De dónde salió el total que se repartió.
/// </summary>
public enum OrigenTotal
{
    /// <summary>
    /// El monto real del recibo que escribió la persona (Periodo.MontoRecibo).
    /// </summary>
    MontoRecibo,

    /// <summary>
    /// El monto que calcula VoltiaMX con la tarifa (sin IVA ni DAP).
    /// </summary>
    MontoCalculado,

    /// <summary>
    /// La persona escribió otro total.
    /// </summary>
    Capturado,
}

public enum ModoReparto
{
    /// <summary>
    /// Cada inquilino paga lo que mide su submedidor; lo que no miden queda a cargo del propietario.
    /// </summary>
    SoloMedido,

    /// <summary>
    /// Se reparte todo el total en proporción a lo que mide cada submedidor.
    /// </summary>
    ProporcionalAlMedido,
}

/// <summary>
/// Reparto guardado de un periodo cerrado: una copia fija de lo calculado, que no cambia aunque después
/// cambien las tarifas o se recalculen los periodos.
/// </summary>
public class Reparto
{
    public int Id { get; set; }

    public int LugarId { get; set; }

    public Lugar? Lugar { get; set; }

    public DateOnly PeriodoInicio { get; set; }

    public DateOnly PeriodoFin { get; set; }

    public decimal TotalRepartido { get; set; }

    public OrigenTotal OrigenTotal { get; set; }

    public ModoReparto Modo { get; set; }

    /// <summary>
    /// kWh del medidor principal en el periodo (Periodo.KwhTotales al guardar).
    /// </summary>
    public decimal KwhPrincipal { get; set; }

    public DateTime FechaCreacion { get; set; }

    public List<RepartoInquilino> Inquilinos { get; set; } = [];
}

/// <summary>
/// Lo que le toca a un inquilino en un reparto, con su nombre copiado al guardar (renombrar no cambia
/// los recibos ya emitidos).
/// </summary>
public class RepartoInquilino
{
    public int Id { get; set; }

    public int RepartoId { get; set; }

    public Reparto? Reparto { get; set; }

    /// <summary>
    /// Null si el inquilino ya no existe.
    /// </summary>
    public int? InquilinoId { get; set; }

    public Inquilino? Inquilino { get; set; }

    public string NombreInquilino { get; set; } = string.Empty;

    public decimal LecturaAnterior { get; set; }

    public decimal LecturaActual { get; set; }

    public decimal Kwh { get; set; }

    public decimal Cuota { get; set; }
}
