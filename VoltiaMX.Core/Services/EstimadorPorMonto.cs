using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum ErrorReciboPorMonto
{
    /// <summary>
    /// El total debe ser mayor a 0 y no pasar de <see cref="EstimadorPorMonto.TotalMaximo"/>.
    /// </summary>
    TotalFueraDeRango,

    /// <summary>
    /// Los impuestos y cargos van de 0 a <see cref="EstimadorPorMonto.CargosMaximos"/> %.
    /// </summary>
    CargosFueraDeRango,

    /// <summary>
    /// El monto no alcanza ni para lo que cobra la tarifa sin consumir (cargo fijo): no hay kWh.
    /// </summary>
    MontoNoAlcanza,

    FechaFutura,

    /// <summary>
    /// Las fechas del recibo se cruzan con un recibo que ya está registrado.
    /// </summary>
    Traslape,

    LugarNoExiste,
}

/// <summary>
/// kWh que caben en un recibo por su monto. <see cref="Monto"/> es Total(<see cref="Kwh"/>) con la tarifa, antes de
/// impuestos; <see cref="MontoSinImpuestos"/>, el total pagado sin los impuestos y cargos; <see cref="Meses"/>, los
/// meses del recibo con los que se calculó.
/// </summary>
public sealed record EstimacionPorMonto(ErrorReciboPorMonto? Error, decimal Kwh, decimal Monto, decimal MontoSinImpuestos, int Meses = 1);

/// <summary>
/// "Solo sé cuánto pagué" (D9): el consumo aproximado de un recibo a partir de su total. Usa el inverso del
/// cálculo por tramos (<see cref="IPresupuestoInversoService"/>); no repite el cálculo.
/// </summary>
public static class EstimadorPorMonto
{
    /// <summary>
    /// Impuestos y otros cargos del recibo que VoltiaMX no calcula, en %. La persona lo puede cambiar.
    /// </summary>
    public const decimal CargosPorDefecto = 16m; // EJEMPLO: verifica los cargos de tu recibo

    public const decimal CargosMaximos = 40m;

    public const decimal TotalMaximo = 1_000_000m;

    public static ErrorReciboPorMonto? Validar(decimal total, decimal porcentaje)
        => total is <= 0m or > TotalMaximo ? ErrorReciboPorMonto.TotalFueraDeRango
            : porcentaje is < 0m or > CargosMaximos ? ErrorReciboPorMonto.CargosFueraDeRango
            : null;

    /// <summary>
    /// El total y la fecha de corte de un recibo por monto, antes de estimar (el onboarding lo usa en el paso
    /// "Tu recibo"). El total se revisa primero.
    /// </summary>
    public static ErrorReciboPorMonto? ValidarRecibo(decimal total, DateOnly fechaCorte, DateOnly hoy)
        => Validar(total, CargosPorDefecto)
            ?? (ValidadorFechaCorte.Validar(fechaCorte, hoy) is null ? null : ErrorReciboPorMonto.FechaFutura);

    /// <param name="dias">Días del recibo (solo los pide el inverso para su reparto diario).</param>
    public static EstimacionPorMonto Estimar(
        IPresupuestoInversoService inverso, ITarifaCalculator calculadora, Tarifa tarifa, int meses, int dias, decimal total, decimal porcentaje)
    {
        ArgumentNullException.ThrowIfNull(inverso);
        ArgumentNullException.ThrowIfNull(calculadora);
        ArgumentNullException.ThrowIfNull(tarifa);
        if (Validar(total, porcentaje) is { } error)
        {
            return new EstimacionPorMonto(error, 0m, 0m, 0m);
        }

        meses = Math.Max(meses, 1);
        var sinImpuestos = total / (1m + porcentaje / 100m);
        var resultado = inverso.Calcular(sinImpuestos, tarifa, Math.Max(dias, 1), meses);
        if (!resultado.CubreCargoFijo || resultado.KwhMaximosPeriodo <= 0m)
        {
            return new EstimacionPorMonto(ErrorReciboPorMonto.MontoNoAlcanza, 0m, 0m, sinImpuestos);
        }

        var kwh = resultado.KwhMaximosPeriodo;
        return new EstimacionPorMonto(null, kwh, calculadora.Calcular(kwh, tarifa, meses).Total, sinImpuestos, meses);
    }
}
