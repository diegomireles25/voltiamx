namespace VoltiaMX.Core.Services;

/// <summary>
/// Fila del recibo explicado, con kWh, precio y subtotal a 2 decimales.
/// </summary>
public sealed record RangoRedondeado(string Nombre, decimal Kwh, decimal PrecioPorKwh, decimal Subtotal);

/// <summary>
/// Desglose listo para mostrar: <see cref="Total"/> es la suma exacta de los subtotales y el cargo fijo
/// ya redondeados, para que las filas sumen lo que dice la fila del total.
/// </summary>
public sealed record DesgloseRedondeado(IReadOnlyList<RangoRedondeado> Rangos, decimal CargoFijo, decimal Total)
{
    public static DesgloseRedondeado Desde(ResultadoCalculo calculo)
    {
        ArgumentNullException.ThrowIfNull(calculo);
        var rangos = calculo.Desglose
            .Select(r => new RangoRedondeado(r.Nombre, Centavos(r.Kwh), Centavos(r.PrecioPorKwh), Centavos(r.Subtotal)))
            .ToList();
        var cargoFijo = Centavos(calculo.CargoFijo);
        return new DesgloseRedondeado(rangos, cargoFijo, rangos.Sum(r => r.Subtotal) + cargoFijo);
    }

    private static decimal Centavos(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
