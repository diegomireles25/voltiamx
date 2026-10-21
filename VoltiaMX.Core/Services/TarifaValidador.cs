using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum CodigoErrorTarifa
{
    NombreObligatorio,
    NombreMuyLargo,
    NombreDuplicado,
    SinRangos,
    NombreDeRangoObligatorio,
    PrimerRangoNoEmpiezaEnCero,
    HuecoEntreRangos,
    HastaNoMayorQueDesde,
    RangoDespuesDeUnoSinLimite,
    UltimoRangoConLimite,
    PrecioNoPositivo,
    PrecioConMasDeCuatroDecimales,
    CargoFijoNegativo,
    LimiteDacSoloDomestica,
    LimiteDacNoPositivo,
    VigenciaObligatoria,
}

/// <summary>
/// Error de validación. <paramref name="Rango"/> es el número del rango (1..N) cuando aplica.
/// </summary>
public sealed record ErrorTarifa(CodigoErrorTarifa Codigo, int? Rango = null);

/// <summary>
/// Reglas de una tarifa editable. La continuidad de los rangos es la misma de
/// <see cref="RangosDeTarifa"/>, que la usa para los cálculos.
/// </summary>
public static class TarifaValidador
{
    public const int LargoMaximoNombre = 40;
    public const int DecimalesDePrecio = 4;

    /// <param name="otrosNombres">Nombres de las demás tarifas (sin la que se edita).</param>
    public static IReadOnlyList<ErrorTarifa> Validar(TarifaEditable tarifa, IEnumerable<string> otrosNombres)
    {
        ArgumentNullException.ThrowIfNull(tarifa);
        ArgumentNullException.ThrowIfNull(otrosNombres);
        var errores = new List<ErrorTarifa>();

        var nombre = tarifa.Nombre?.Trim() ?? string.Empty;
        if (nombre.Length == 0)
        {
            errores.Add(new(CodigoErrorTarifa.NombreObligatorio));
        }
        else
        {
            if (nombre.Length > LargoMaximoNombre)
            {
                errores.Add(new(CodigoErrorTarifa.NombreMuyLargo));
            }

            if (otrosNombres.Any(o => string.Equals(o?.Trim(), nombre, StringComparison.OrdinalIgnoreCase)))
            {
                errores.Add(new(CodigoErrorTarifa.NombreDuplicado));
            }
        }

        if (tarifa.Rangos.Count == 0)
        {
            errores.Add(new(CodigoErrorTarifa.SinRangos));
        }
        else
        {
            for (var i = 0; i < tarifa.Rangos.Count; i++)
            {
                var rango = tarifa.Rangos[i];
                if (string.IsNullOrWhiteSpace(rango.Nombre))
                {
                    errores.Add(new(CodigoErrorTarifa.NombreDeRangoObligatorio, i + 1));
                }

                if (rango.PrecioPorKwh <= 0m)
                {
                    errores.Add(new(CodigoErrorTarifa.PrecioNoPositivo, i + 1));
                }
                else if (decimal.Round(rango.PrecioPorKwh, DecimalesDePrecio) != rango.PrecioPorKwh)
                {
                    errores.Add(new(CodigoErrorTarifa.PrecioConMasDeCuatroDecimales, i + 1));
                }
            }

            errores.AddRange(RevisarRangos(tarifa.RangosConDesde()));
        }

        if (tarifa.CargoFijoMensual < 0m)
        {
            errores.Add(new(CodigoErrorTarifa.CargoFijoNegativo));
        }

        if (tarifa.LimiteDacKwhMensual is decimal limite)
        {
            if (tarifa.Tipo != TipoTarifa.Domestica)
            {
                errores.Add(new(CodigoErrorTarifa.LimiteDacSoloDomestica));
            }
            else if (limite <= 0m)
            {
                errores.Add(new(CodigoErrorTarifa.LimiteDacNoPositivo));
            }
        }

        if (tarifa.Vigencia is null)
        {
            errores.Add(new(CodigoErrorTarifa.VigenciaObligatoria));
        }

        return errores;
    }

    /// <summary>
    /// Continuidad de rangos ya armados (con su "desde"): el primero empieza en 0, cada uno donde
    /// termina el anterior, cada tope es mayor que su inicio y solo el último queda sin límite.
    /// </summary>
    public static IReadOnlyList<ErrorTarifa> RevisarRangos(IReadOnlyList<TarifaRango> rangosOrdenados)
    {
        ArgumentNullException.ThrowIfNull(rangosOrdenados);
        var errores = new List<ErrorTarifa>();
        for (var i = 0; i < rangosOrdenados.Count; i++)
        {
            var rango = rangosOrdenados[i];
            var numero = i + 1;
            if (i == 0)
            {
                if (rango.KwhDesde != 0m)
                {
                    errores.Add(new(CodigoErrorTarifa.PrimerRangoNoEmpiezaEnCero, numero));
                }
            }
            else if (rangosOrdenados[i - 1].KwhHasta is not decimal finAnterior)
            {
                errores.Add(new(CodigoErrorTarifa.RangoDespuesDeUnoSinLimite, numero));
                continue;
            }
            else if (rango.KwhDesde != finAnterior)
            {
                errores.Add(new(CodigoErrorTarifa.HuecoEntreRangos, numero));
            }

            if (rango.KwhHasta is decimal hasta && hasta <= rango.KwhDesde)
            {
                errores.Add(new(CodigoErrorTarifa.HastaNoMayorQueDesde, numero));
            }
        }

        if (rangosOrdenados.Count > 0 && rangosOrdenados[^1].KwhHasta is not null)
        {
            errores.Add(new(CodigoErrorTarifa.UltimoRangoConLimite, rangosOrdenados.Count));
        }

        return errores;
    }
}
