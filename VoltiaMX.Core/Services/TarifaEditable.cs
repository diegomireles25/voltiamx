using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Rango como lo captura la persona: solo el tope (vacío en el último = sin límite). El "desde" se
/// calcula con el tope del rango anterior.
/// </summary>
public sealed record RangoEditable(string Nombre, decimal? KwhHasta, decimal PrecioPorKwh);

/// <summary>
/// Tarifa en edición. <paramref name="Id"/> es null para una tarifa nueva. Montos sin IVA ni DAP.
/// </summary>
public sealed record TarifaEditable(
    int? Id,
    string Nombre,
    TipoTarifa Tipo,
    decimal CargoFijoMensual,
    decimal? LimiteDacKwhMensual,
    DateOnly? Vigencia,
    IReadOnlyList<RangoEditable> Rangos)
{
    /// <summary>
    /// Rangos con su "desde" calculado y Orden 1..N. Tras un rango sin límite el "desde" queda en el
    /// del rango sin límite (el validador lo marca como error).
    /// </summary>
    public IReadOnlyList<TarifaRango> RangosConDesde()
    {
        var resultado = new List<TarifaRango>();
        var desde = 0m;
        for (var i = 0; i < Rangos.Count; i++)
        {
            var rango = Rangos[i];
            resultado.Add(new TarifaRango
            {
                Nombre = rango.Nombre.Trim(),
                KwhDesde = desde,
                KwhHasta = rango.KwhHasta,
                PrecioPorKwh = rango.PrecioPorKwh,
                Orden = i + 1,
            });
            desde = rango.KwhHasta ?? desde;
        }

        return resultado;
    }

    /// <summary>
    /// Tarifa en memoria con estos datos (para la vista previa con TarifaCalculator).
    /// </summary>
    public Tarifa ComoTarifa() => new()
    {
        Id = Id ?? 0,
        Nombre = Nombre.Trim(),
        Tipo = Tipo,
        CargoFijoMensual = CargoFijoMensual,
        LimiteDacKwhMensual = LimiteDacKwhMensual,
        Vigencia = Vigencia ?? default,
        Rangos = [.. RangosConDesde()],
    };
}

/// <summary>
/// Tarifa en la lista de "Tus tarifas": cuántos rangos tiene y qué lugares la usan.
/// </summary>
public sealed record TarifaConUso(int Id, string Nombre, TipoTarifa Tipo, int NumeroDeRangos, IReadOnlyList<string> LugaresQueLaUsan)
{
    public bool EnUso => LugaresQueLaUsan.Count > 0;
}

public sealed record ResultadoGuardarTarifa(int? Id, IReadOnlyList<ErrorTarifa> Errores)
{
    public bool Guardada => Errores.Count == 0;
}

public enum MotivoNoEliminarTarifa
{
    NoExiste,
    EnUso,
    EsLaUltima,
}

public sealed record ResultadoEliminarTarifa(MotivoNoEliminarTarifa? Motivo, IReadOnlyList<string> LugaresQueLaUsan)
{
    public bool Eliminada => Motivo is null;
}
