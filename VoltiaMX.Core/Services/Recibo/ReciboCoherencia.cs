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

    public static IReadOnlyList<AvisoCoherencia> Comprobar(ReciboLeido recibo, IEnumerable<string>? tarifasRegistradas = null)
        => Comprobar(ReciboAConfirmar.Desde(recibo), tarifasRegistradas);

    public static IReadOnlyList<AvisoCoherencia> Comprobar(ReciboAConfirmar recibo, IEnumerable<string>? tarifasRegistradas = null)
    {
        ArgumentNullException.ThrowIfNull(recibo);
        var avisos = new List<AvisoCoherencia>();

        if (recibo.LecturaActual is decimal actual && recibo.LecturaAnterior is decimal anterior && recibo.ConsumoKwh is decimal consumo
            && Math.Abs(actual - anterior - consumo) > ToleranciaKwh)
        {
            avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.LecturasNoCuadranConConsumo, actual - anterior, consumo));
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

        if (tarifasRegistradas is not null && !string.IsNullOrWhiteSpace(recibo.Tarifa)
            && !tarifasRegistradas.Any(t => string.Equals(t?.Trim(), recibo.Tarifa.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            avisos.Add(new AvisoCoherencia(CodigoAvisoRecibo.TarifaNoRegistrada));
        }

        return avisos;
    }
}
