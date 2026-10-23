using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Consumo mensual estimado de un aparato a partir de su potencia y su uso.
/// </summary>
public static class ConsumoAparato
{
    /// <summary>
    /// Semanas en un mes promedio: 52 semanas del año entre 12 meses (≈ 4.33). Con 4 semanas
    /// exactas el consumo mensual quedaría corto por unos dos días.
    /// </summary>
    public const decimal SemanasPorMes = 52m / 12m;

    /// <summary>
    /// kWh al mes = cantidad × watts / 1000 × horas al día × días a la semana × semanas por mes × ciclo.
    /// </summary>
    public static decimal KwhPorMes(int cantidad, decimal watts, decimal horasPorDia, int diasPorSemana, decimal cicloDeTrabajo)
        => cantidad * watts / 1000m * horasPorDia * diasPorSemana * SemanasPorMes * cicloDeTrabajo;

    public static decimal KwhPorMes(Aparato aparato)
    {
        ArgumentNullException.ThrowIfNull(aparato);
        return KwhPorMes(aparato.Cantidad, aparato.Watts, aparato.HorasPorDia, aparato.DiasPorSemana, aparato.CicloDeTrabajo);
    }

    public static decimal KwhPorMes(AparatoEditable aparato)
    {
        ArgumentNullException.ThrowIfNull(aparato);
        return KwhPorMes(aparato.Cantidad, aparato.Watts, aparato.HorasPorDia, aparato.DiasPorSemana, aparato.CicloDeTrabajo);
    }
}
