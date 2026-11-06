using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class RangoAConfirmar
{
    public string Nombre { get; set; } = string.Empty;

    public decimal? Kwh { get; set; }

    public decimal? Precio { get; set; }

    public decimal? Importe { get; set; }
}

public enum ErrorConfirmacionRecibo
{
    FaltaFecha,
    FaltaLecturaActual,
    LecturaNegativa,

    /// <summary>
    /// La fecha de corte (o la de la lectura) es posterior a hoy (Fase 9b, D5).
    /// </summary>
    FechaCorteFutura,
}

public sealed record ResultadoConfirmacionRecibo(NuevaLectura? Lectura, IReadOnlyList<ErrorConfirmacionRecibo> Errores);

/// <summary>
/// Datos del recibo tal como la persona los confirma (corregidos o no). Nada se guarda hasta
/// convertirlo en una <see cref="NuevaLectura"/>.
/// </summary>
public sealed class ReciboAConfirmar
{
    public string? Tarifa { get; set; }

    public DateOnly? PeriodoInicio { get; set; }

    public DateOnly? PeriodoFin { get; set; }

    public decimal? Dias { get; set; }

    public decimal? LecturaAnterior { get; set; }

    public decimal? LecturaActual { get; set; }

    public decimal? ConsumoKwh { get; set; }

    public List<RangoAConfirmar> Rangos { get; set; } = [];

    public decimal? ImporteEnergia { get; set; }

    /// <summary>
    /// Total a pagar del recibo (con IVA y DAP): se guarda como monto del recibo del periodo.
    /// </summary>
    public decimal? Total { get; set; }

    public DateOnly? FechaCorte { get; set; }

    /// <summary>
    /// Fecha de la lectura actual: el fin del periodo facturado, que es cuando se tomó.
    /// </summary>
    public DateOnly? FechaLectura { get; set; }

    public static ReciboAConfirmar Desde(ReciboLeido leido)
    {
        ArgumentNullException.ThrowIfNull(leido);
        return new ReciboAConfirmar
        {
            Tarifa = leido.Tarifa.Valor,
            PeriodoInicio = leido.PeriodoInicio.Valor,
            PeriodoFin = leido.PeriodoFin.Valor,
            Dias = leido.Dias.Valor,
            LecturaAnterior = leido.LecturaAnterior.Valor,
            LecturaActual = leido.LecturaActual.Valor,
            ConsumoKwh = leido.ConsumoKwh.Valor,
            Rangos = leido.Rangos
                .Select(r => new RangoAConfirmar { Nombre = r.Nombre, Kwh = r.Kwh.Valor, Precio = r.Precio.Valor, Importe = r.Importe.Valor })
                .ToList(),
            ImporteEnergia = leido.ImporteEnergia.Valor,
            Total = leido.Total.Valor,
            FechaCorte = leido.FechaCorte.Valor,
            FechaLectura = leido.PeriodoFin.Valor,
        };
    }

    /// <summary>
    /// La lectura actual como lectura del medidor, con origen OCR y el total del recibo.
    /// </summary>
    public ResultadoConfirmacionRecibo ANuevaLectura(int lugarId, DateOnly hoy)
    {
        var errores = new List<ErrorConfirmacionRecibo>();
        if (FechaLectura is null)
        {
            errores.Add(ErrorConfirmacionRecibo.FaltaFecha);
        }

        if ((FechaCorte is DateOnly corte && ValidadorFechaCorte.Validar(corte, hoy) is not null)
            || (FechaLectura is DateOnly fecha && ValidadorFechaCorte.Validar(fecha, hoy) is not null))
        {
            errores.Add(ErrorConfirmacionRecibo.FechaCorteFutura);
        }

        if (LecturaActual is null)
        {
            errores.Add(ErrorConfirmacionRecibo.FaltaLecturaActual);
        }
        else if (LecturaActual < 0m)
        {
            errores.Add(ErrorConfirmacionRecibo.LecturaNegativa);
        }

        return errores.Count > 0
            ? new ResultadoConfirmacionRecibo(null, errores)
            : new ResultadoConfirmacionRecibo(
                new NuevaLectura(lugarId, FechaLectura!.Value, LecturaActual!.Value, OrigenLectura.Ocr, Total),
                []);
    }
}
