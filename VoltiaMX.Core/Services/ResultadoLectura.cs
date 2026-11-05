namespace VoltiaMX.Core.Services;

public enum MotivoRechazoLectura
{
    LugarNoExiste,
    ValorNegativo,
    FechaFutura,
    MenorQueAnterior,
    MayorQueSiguiente,
    PeriodoCerrado,
}

public enum ResultadoEliminacion
{
    Eliminada,
    SinLecturas,
    EsCorteDePeriodoCerrado,
}

/// <summary>
/// Periodo que se cierra (o se cerraría) con la lectura. <paramref name="Monto"/> sale de la tarifa,
/// sin IVA ni DAP. <paramref name="Estimado"/>: la lectura de corte se interpoló o el periodo se midió
/// desde una lectura posterior a su inicio.
/// </summary>
public sealed record CierrePeriodo(DateOnly Inicio, DateOnly Fin, decimal Kwh, decimal Monto, bool Estimado);

/// <summary>
/// Lectura para la lista de la pantalla. <paramref name="ConsumoDesdeAnterior"/> es null en la primera
/// del lugar; <paramref name="EsCorteDePeriodo"/> marca la lectura en la fecha de corte de un periodo cerrado.
/// </summary>
public sealed record LecturaListada(
    DateOnly Fecha, decimal ValorKwh, decimal? ConsumoDesdeAnterior, bool EsEstimada, bool EsCorteDePeriodo);

/// <summary>
/// Lugar donde se capturan las lecturas: el mismo que muestra el Dashboard. <paramref name="PreciosDeEjemplo"/>
/// indica que su tarifa todavía trae los precios sembrados (Tarifa.EsDeEjemplo).
/// </summary>
public sealed record LugarDeLecturas(int Id, string Nombre, bool EsDemo, bool PreciosDeEjemplo);

/// <summary>
/// Qué pasó (o pasaría, en la previsualización) al registrar una lectura.
/// </summary>
public sealed record ResultadoLectura
{
    public MotivoRechazoLectura? Rechazo { get; init; }

    public bool Aceptada => Rechazo is null;

    /// <summary>
    /// Ya había una lectura en esa fecha y se reemplaza (una por día).
    /// </summary>
    public bool Reemplaza { get; init; }

    /// <summary>
    /// Primera lectura del lugar: solo fija el punto de partida, todavía no hay consumo.
    /// </summary>
    public bool EsPuntoDePartida { get; init; }

    /// <summary>
    /// La lectura pasa del corte pero no hay una lectura anterior al corte que sirva de base,
    /// así que el periodo no se puede cerrar.
    /// </summary>
    public bool SinLecturaBase { get; init; }

    public IReadOnlyList<CierrePeriodo> Cierres { get; init; } = [];

    public static ResultadoLectura Rechazada(MotivoRechazoLectura motivo) => new() { Rechazo = motivo };
}
