using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum TipoEscenario
{
    /// <summary>
    /// Usar el aparato menos horas al día o menos días a la semana.
    /// </summary>
    MenosHoras,

    /// <summary>
    /// Cambiarlo por un equipo de menos watts (y, si se sabe, otro ciclo de trabajo), con su precio.
    /// </summary>
    EquipoEficiente,
}

/// <summary>
/// Qué cambia en el aparato. Lo que no se indica queda como está; la cantidad nunca cambia.
/// </summary>
public sealed record EscenarioAhorro(
    TipoEscenario Tipo,
    decimal? HorasPorDia = null,
    int? DiasPorSemana = null,
    decimal? Watts = null,
    decimal? CicloDeTrabajo = null,
    decimal? PrecioEquipo = null)
{
    public static EscenarioAhorro MenosHoras(decimal horasPorDia, int diasPorSemana)
        => new(TipoEscenario.MenosHoras, HorasPorDia: horasPorDia, DiasPorSemana: diasPorSemana);

    /// <param name="cicloDeTrabajo">Null: el mismo ciclo del aparato actual.</param>
    /// <param name="precioEquipo">Null: no se sabe el precio y no se calcula la recuperación.</param>
    public static EscenarioAhorro EquipoEficiente(decimal watts, decimal? cicloDeTrabajo = null, decimal? precioEquipo = null)
        => new(TipoEscenario.EquipoEficiente, Watts: watts, CicloDeTrabajo: cicloDeTrabajo, PrecioEquipo: precioEquipo);

    /// <summary>
    /// El aparato con el cambio aplicado.
    /// </summary>
    public AparatoEditable Aplicar(Aparato aparato)
    {
        ArgumentNullException.ThrowIfNull(aparato);
        var menosHoras = Tipo == TipoEscenario.MenosHoras;
        return new AparatoEditable(
            aparato.Id,
            aparato.LugarId,
            aparato.Nombre,
            aparato.Categoria,
            aparato.Cantidad,
            menosHoras ? aparato.Watts : Watts ?? aparato.Watts,
            menosHoras ? HorasPorDia ?? aparato.HorasPorDia : aparato.HorasPorDia,
            menosHoras ? DiasPorSemana ?? aparato.DiasPorSemana : aparato.DiasPorSemana,
            menosHoras ? aparato.CicloDeTrabajo : CicloDeTrabajo ?? aparato.CicloDeTrabajo);
    }
}

public enum CodigoErrorSimulacion
{
    HorasFueraDeRango,
    DiasFueraDeRango,
    WattsFueraDeRango,
    CicloFueraDeRango,
    PrecioNegativo,
}

/// <summary>
/// Los valores del escenario siguen las mismas reglas que un aparato (AparatoValidador).
/// </summary>
public static class SimuladorValidador
{
    public static IReadOnlyList<CodigoErrorSimulacion> Validar(Aparato aparato, EscenarioAhorro escenario)
    {
        ArgumentNullException.ThrowIfNull(aparato);
        ArgumentNullException.ThrowIfNull(escenario);

        var errores = AparatoValidador.Validar(escenario.Aplicar(aparato))
            .Select(codigo => codigo switch
            {
                CodigoErrorAparato.HorasFueraDeRango => CodigoErrorSimulacion.HorasFueraDeRango,
                CodigoErrorAparato.DiasFueraDeRango => CodigoErrorSimulacion.DiasFueraDeRango,
                CodigoErrorAparato.WattsFueraDeRango => CodigoErrorSimulacion.WattsFueraDeRango,
                CodigoErrorAparato.CicloFueraDeRango => CodigoErrorSimulacion.CicloFueraDeRango,
                _ => (CodigoErrorSimulacion?)null, // nombre, categoría y cantidad son los del aparato guardado
            })
            .OfType<CodigoErrorSimulacion>()
            .ToList();

        if (escenario.PrecioEquipo < 0m)
        {
            errores.Add(CodigoErrorSimulacion.PrecioNegativo);
        }

        return errores;
    }
}
