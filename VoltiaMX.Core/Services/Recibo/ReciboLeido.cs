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
    CampoFecha FechaCorte);
