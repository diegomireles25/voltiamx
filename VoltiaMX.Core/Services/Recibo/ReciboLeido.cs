namespace VoltiaMX.Core.Services;

/// <summary>
/// Qué tan seguro es un dato leído del recibo.
/// </summary>
public enum Confianza
{
    /// <summary>
    /// Junto a su etiqueta y con formato válido.
    /// </summary>
    Alta,

    /// <summary>
    /// Deducido de otros datos o tomado de la línea siguiente a su etiqueta: hay que revisarlo.
    /// </summary>
    Baja,

    NoEncontrado,
}

public sealed record CampoTexto(string? Valor, Confianza Confianza)
{
    public static readonly CampoTexto NoEncontrado = new(null, Confianza.NoEncontrado);
}

public sealed record CampoNumero(decimal? Valor, Confianza Confianza)
{
    public static readonly CampoNumero NoEncontrado = new(null, Confianza.NoEncontrado);
}

public sealed record CampoFecha(DateOnly? Valor, Confianza Confianza)
{
    public static readonly CampoFecha NoEncontrado = new(null, Confianza.NoEncontrado);
}

/// <summary>
/// Renglón de energía del recibo (Básico, Intermedio, Excedente).
/// </summary>
public sealed record RangoLeido(string Nombre, CampoNumero Kwh, CampoNumero Precio, CampoNumero Importe);

/// <summary>
/// Lo que se pudo leer del texto del recibo, campo por campo. Nada se inventa: lo dudoso queda NoEncontrado.
/// Fase 9k: <see cref="FechaCorte"/> es el FIN del periodo facturado (nunca "corte a partir" ni el límite de pago) y
/// <see cref="Total"/> es el "TOTAL A PAGAR", que puede incluir saldos anteriores: el monto del recibo es
/// <see cref="ImporteRecibo"/>.
/// </summary>
public sealed record ReciboLeido(
    CampoTexto Tarifa,
    CampoFecha PeriodoInicio,
    CampoFecha PeriodoFin,
    CampoNumero Dias,
    CampoNumero LecturaAnterior,
    CampoNumero LecturaActual,
    CampoNumero ConsumoKwh,
    IReadOnlyList<RangoLeido> Rangos,
    CampoNumero ImporteEnergia,
    CampoNumero Total,
    CampoFecha FechaCorte)
{
    /// <summary>
    /// Multiplicador del medidor: consumo = (lectura actual − anterior) × multiplicador.
    /// </summary>
    public CampoNumero Multiplicador { get; init; } = CampoNumero.NoEncontrado;

    public CampoNumero Iva { get; init; } = CampoNumero.NoEncontrado;

    /// <summary>
    /// "Fac. del Periodo": lo que se facturó en este recibo (energía más IVA).
    /// </summary>
    public CampoNumero FacturacionPeriodo { get; init; } = CampoNumero.NoEncontrado;

    public CampoNumero AdeudoAnterior { get; init; } = CampoNumero.NoEncontrado;

    /// <summary>
    /// "Su Pago": lo que se pagó del recibo anterior (sin signo).
    /// </summary>
    public CampoNumero SuPago { get; init; } = CampoNumero.NoEncontrado;

    /// <summary>
    /// "Total" del desglose (la facturación más los saldos anteriores).
    /// </summary>
    public CampoNumero TotalDesglose { get; init; } = CampoNumero.NoEncontrado;

    /// <summary>
    /// El monto del recibo que usa la app (D4): la facturación del periodo; sin ella, energía más IVA (para revisar);
    /// sin ninguna, el total a pagar (para revisar).
    /// </summary>
    public CampoNumero ImporteRecibo { get; init; } = CampoNumero.NoEncontrado;

    /// <summary>
    /// El importe salió del "TOTAL A PAGAR": puede incluir saldos anteriores.
    /// </summary>
    public bool ImporteDesdeTotal { get; init; }

    /// <summary>
    /// Más de esto entre el total a pagar y el importe del recibo: el total incluye saldos anteriores o redondeo.
    /// </summary>
    public const decimal DiferenciaTotalAvisar = 2m;

    public bool TotalIncluyeSaldos
        => !ImporteDesdeTotal && Total.Valor is decimal total && ImporteRecibo.Valor is decimal importe
            && Math.Abs(total - importe) > DiferenciaTotalAvisar;
}
