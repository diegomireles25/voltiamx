using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum CodigoErrorAparato
{
    NombreVacio,
    NombreMuyLargo,
    CategoriaInvalida,
    CantidadFueraDeRango,
    WattsFueraDeRango,
    HorasFueraDeRango,
    DiasFueraDeRango,
    CicloFueraDeRango,
}

/// <summary>
/// Reglas de un aparato. Devuelve los errores en el orden de los campos del formulario.
/// </summary>
public static class AparatoValidador
{
    public const int LargoMaximoNombre = 40;
    public const int CantidadMaxima = 99;
    public const decimal WattsMaximos = 20_000m;
    public const decimal HorasMaximas = 24m;
    public const int DiasMaximos = 7;
    public const decimal CicloMinimo = 0.05m;
    public const decimal CicloMaximo = 1m;

    public static IReadOnlyList<CodigoErrorAparato> Validar(AparatoEditable aparato)
    {
        ArgumentNullException.ThrowIfNull(aparato);
        var errores = new List<CodigoErrorAparato>();

        var nombre = aparato.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            errores.Add(CodigoErrorAparato.NombreVacio);
        }
        else if (nombre.Length > LargoMaximoNombre)
        {
            errores.Add(CodigoErrorAparato.NombreMuyLargo);
        }

        if (!Enum.IsDefined(aparato.Categoria))
        {
            errores.Add(CodigoErrorAparato.CategoriaInvalida);
        }

        if (aparato.Cantidad is < 1 or > CantidadMaxima)
        {
            errores.Add(CodigoErrorAparato.CantidadFueraDeRango);
        }

        if (aparato.Watts <= 0m || aparato.Watts > WattsMaximos)
        {
            errores.Add(CodigoErrorAparato.WattsFueraDeRango);
        }

        if (aparato.HorasPorDia <= 0m || aparato.HorasPorDia > HorasMaximas)
        {
            errores.Add(CodigoErrorAparato.HorasFueraDeRango);
        }

        if (aparato.DiasPorSemana is < 1 or > DiasMaximos)
        {
            errores.Add(CodigoErrorAparato.DiasFueraDeRango);
        }

        if (aparato.CicloDeTrabajo < CicloMinimo || aparato.CicloDeTrabajo > CicloMaximo)
        {
            errores.Add(CodigoErrorAparato.CicloFueraDeRango);
        }

        return errores;
    }
}
