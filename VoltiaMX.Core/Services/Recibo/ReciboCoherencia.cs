namespace VoltiaMX.Core.Services;

public enum CodigoAvisoRecibo
{
    /// <summary>
    /// Lectura actual menos anterior no da el consumo del recibo.
    /// </summary>
    LecturasNoCuadranConConsumo,

    /// <summary>
    /// Los kWh de los renglones no suman el consumo.
    /// </summary>
    RangosNoSumanConsumo,

    /// <summary>
    /// Los importes de los renglones no suman el importe de energía.
    /// </summary>
    ImportesNoSumanEnergia,

    /// <summary>
    /// La tarifa del recibo no está entre tus tarifas.
    /// </summary>
    TarifaNoRegistrada,

    /// <summary>
    /// Fase 9k: en un tramo, los kWh por el precio no dan su subtotal.
    /// </summary>
    SubtotalNoCuadra,

    /// <summary>
    /// Fase 9k: la energía más el IVA no da el importe del recibo.
    /// </summary>
    EnergiaMasIvaNoCuadra,

    /// <summary>
    /// Fase 9k: el total a pagar incluye saldos anteriores o redondeo; se usa el importe del recibo.
    /// </summary>
    TotalIncluyeSaldos,

    /// <summary>
    /// Fase 9k: no se leyó el desglose y el importe salió del total a pagar, que puede incluir saldos anteriores.
    /// </summary>
    TotalSinDesglose,
}

/// <summary>
/// Un dato del recibo que no cuadra: <see cref="Calculado"/> sale de los demás datos y
/// <see cref="Leido"/> es el que dice el recibo.
/// </summary>
public sealed record AvisoCoherencia(CodigoAvisoRecibo Codigo, decimal? Calculado = null, decimal? Leido = null);

/// <summary>
/// Comprobaciones entre los datos del recibo. Solo comparan lo que existe: un dato faltante no genera aviso.
/// </summary>
public static class ReciboCoherencia
{
    public const decimal ToleranciaKwh = 1m;
    public const decimal ToleranciaPesos = 1m;

    /// <summary>
    /// Fase 9k: kWh por precio contra el subtotal de cada tramo.
    /// </summary>
    public const decimal ToleranciaSubtotal = 0.50m;

    public static IReadOnlyList<AvisoCoherencia> Comprobar(ReciboLeido recibo, IEnumerable<string>? tarifasRegistradas = null)
        => Comprobar(ReciboAConfirmar.Desde(recibo), tarifasRegistradas);

    public static IReadOnlyList<AvisoCoherencia> Comprobar(ReciboAConfirmar recibo, IEnumerable<string>? tarifasRegistradas = null)
    {
        ArgumentNullException.ThrowIfNull(recibo);
        var avisos = new List<AvisoCoherencia>();

        // (actual − anterior) × multiplicador = consumo.
        var multiplicador = recibo.Multiplicador is > 0m and decimal m ? m : 1m;
        if (recibo.LecturaActual is decimal actual && recibo.LecturaAnterior is decimal anterior && recibo.ConsumoKwh is decimal consumo
            && Math.Abs((actual - anterior) * multiplicador - consumo) > ToleranciaKwh)
        {
            avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.LecturasNoCuadranConConsumo, (actual - anterior) * multiplicador, consumo));
        }

        if (recibo.Rangos.Count > 0 && recibo.Rangos.All(r => r.Kwh is not null) && recibo.ConsumoKwh is decimal leido)
        {
            var suma = recibo.Rangos.Sum(r => r.Kwh!.Value);
            if (Math.Abs(suma - leido) > ToleranciaKwh)
            {
                avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.RangosNoSumanConsumo, suma, leido));
            }
        }

        if (recibo.Rangos.Count > 0 && recibo.Rangos.All(r => r.Importe is not null) && recibo.ImporteEnergia is decimal energia)
        {
            var suma = recibo.Rangos.Sum(r => r.Importe!.Value);
            if (Math.Abs(suma - energia) > ToleranciaPesos)
            {
                avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.ImportesNoSumanEnergia, suma, energia));
            }
        }

        foreach (var rango in recibo.Rangos)
        {
            if (rango.Kwh is decimal kwh && rango.Precio is decimal precio && rango.Importe is decimal subtotal
                && Math.Abs(kwh * precio - subtotal) > ToleranciaSubtotal)
            {
                avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.SubtotalNoCuadra, kwh * precio, subtotal));
            }
        }

        if (recibo.ImporteEsFacturacion && recibo.ImporteEnergia is decimal deEnergia && recibo.Iva is decimal iva
            && recibo.ImporteRecibo is decimal importe && Math.Abs(deEnergia + iva - importe) > ToleranciaPesos)
        {
            avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.EnergiaMasIvaNoCuadra, deEnergia + iva, importe));
        }

        if (recibo.ImporteDesdeTotal)
        {
            avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.TotalSinDesglose));
        }
        else if (recibo.Total is decimal total && recibo.ImporteRecibo is decimal delRecibo
            && Math.Abs(total - delRecibo) > ReciboLeido.DiferenciaTotalAvisar)
        {
            avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.TotalIncluyeSaldos, delRecibo, total));
        }

        if (tarifasRegistradas is not null && !string.IsNullOrWhiteSpace(recibo.Tarifa)
            && !tarifasRegistradas.Any(t => string.Equals(t?.Trim(), recibo.Tarifa.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.TarifaNoRegistrada));
        }

        return avisos;
    }
}
