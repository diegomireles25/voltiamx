using VoltiaMX.Core.Models;

namespace VoltiaMX.Tests;

/// <summary>
/// Tarifas con precios redondos para que el resultado esperado de cada prueba sea evidente.
/// </summary>
internal static class TarifasDePrueba
{
    /// <summary>
    /// Básico 0–75 a $1, Intermedio 75–140 a $2 y Excedente 140+ a $3 por kWh; sin cargo fijo.
    /// </summary>
    public static Tarifa Escalonada() => new()
    {
        Nombre = "Escalonada",
        Tipo = TipoTarifa.Domestica,
        Rangos =
        [
            new() { Nombre = "Básico", KwhDesde = 0m, KwhHasta = 75m, PrecioPorKwh = 1m, Orden = 1 },
            new() { Nombre = "Intermedio", KwhDesde = 75m, KwhHasta = 140m, PrecioPorKwh = 2m, Orden = 2 },
            new() { Nombre = "Excedente", KwhDesde = 140m, KwhHasta = null, PrecioPorKwh = 3m, Orden = 3 },
        ]
    };

    /// <summary>
    /// Un solo rango sin límite a $2 por kWh, con el cargo fijo mensual indicado.
    /// </summary>
    public static Tarifa RangoUnico(decimal cargoFijoMensual = 0m) => new()
    {
        Nombre = "Única",
        Tipo = TipoTarifa.Comercial,
        CargoFijoMensual = cargoFijoMensual,
        Rangos =
        [
            new() { Nombre = "Único", KwhDesde = 0m, KwhHasta = null, PrecioPorKwh = 2m, Orden = 1 },
        ]
    };

    /// <summary>
    /// Tres tramos para los costos y las sugerencias (Fase 8): 0–100 a $1, 100–200 a $2 y 200+ a $4 por kWh;
    /// sin cargo fijo. Con 250 kWh al mes: 100 + 200 + 200 = $500.
    /// </summary>
    public static Tarifa TresTramos() => new()
    {
        Nombre = "Tres tramos",
        Tipo = TipoTarifa.Domestica,
        Rangos =
        [
            new() { Nombre = "Básico", KwhDesde = 0m, KwhHasta = 100m, PrecioPorKwh = 1m, Orden = 1 },
            new() { Nombre = "Intermedio", KwhDesde = 100m, KwhHasta = 200m, PrecioPorKwh = 2m, Orden = 2 },
            new() { Nombre = "Excedente", KwhDesde = 200m, KwhHasta = null, PrecioPorKwh = 4m, Orden = 3 },
        ]
    };
}
