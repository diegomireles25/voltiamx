using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum VeredictoRecibo
{
    Coincide,
    Cercano,
    NoCoincide,
}

/// <summary>
/// Energía calculada con la tarifa contra el importe de energía del recibo (sin IVA ni DAP ni cargo
/// fijo). <paramref name="Diferencia"/> = calculado − recibo; <paramref name="DiferenciaPorcentaje"/>
/// es relativa al recibo (null si el recibo es 0).
/// </summary>
public sealed record ComparacionContraTarifa(
    ResultadoCalculo Calculo,
    decimal SubtotalEnergia,
    decimal Diferencia,
    decimal? DiferenciaPorcentaje,
    VeredictoRecibo Veredicto);

public interface IValidadorRecibo
{
    ComparacionContraTarifa Comparar(Tarifa tarifa, int mesesDelPeriodo, decimal kwh, decimal montoEnergiaRecibo);
}

/// <summary>
/// Compara el recibo de CFE con la tarifa capturada para saber si sus precios y rangos son correctos.
/// </summary>
public sealed class ValidadorRecibo(ITarifaCalculator calculadora) : IValidadorRecibo
{
    /// <summary>Diferencia en pesos que todavía se considera igual (redondeos del recibo).</summary>
    public const decimal ToleranciaPesos = 1m;

    /// <summary>Diferencia relativa que todavía se considera igual: 0.5 %.</summary>
    public const decimal ToleranciaCoincide = 0.005m;

    /// <summary>Diferencia relativa hasta la que la tarifa está cerca pero no exacta: 3 %.</summary>
    public const decimal ToleranciaCercano = 0.03m;

    public ComparacionContraTarifa Comparar(Tarifa tarifa, int mesesDelPeriodo, decimal kwh, decimal montoEnergiaRecibo)
    {
        ArgumentNullException.ThrowIfNull(tarifa);
        ArgumentOutOfRangeException.ThrowIfNegative(montoEnergiaRecibo);

        var calculo = calculadora.Calcular(kwh, tarifa, mesesDelPeriodo);
        var diferencia = calculo.SubtotalEnergia - montoEnergiaRecibo;
        decimal? porcentaje = montoEnergiaRecibo > 0m ? diferencia / montoEnergiaRecibo : null;

        var absoluta = Math.Abs(diferencia);
        var relativa = porcentaje is decimal p ? Math.Abs(p) : (decimal?)null;
        var veredicto = absoluta <= ToleranciaPesos || relativa <= ToleranciaCoincide ? VeredictoRecibo.Coincide
            : relativa <= ToleranciaCercano ? VeredictoRecibo.Cercano
            : VeredictoRecibo.NoCoincide;

        return new ComparacionContraTarifa(calculo, calculo.SubtotalEnergia, diferencia, porcentaje, veredicto);
    }
}
