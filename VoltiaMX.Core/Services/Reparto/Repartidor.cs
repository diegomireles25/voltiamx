using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public enum AvisoReparto
{
    /// <summary>
    /// Los submedidores no midieron nada: no se calcula.
    /// </summary>
    SinConsumoSubmedido,

    /// <summary>
    /// Los submedidores suman más de 1 kWh por encima del medidor principal (solo aviso).
    /// </summary>
    SubmedidoresExcedenPrincipal,

    /// <summary>
    /// Alguna lectura actual es menor que la anterior.
    /// </summary>
    LecturaMenorQueAnterior,

    /// <summary>
    /// Alguna lectura es negativa.
    /// </summary>
    LecturaNegativa,

    /// <summary>
    /// El total es 0, negativo o mayor que el máximo.
    /// </summary>
    TotalInvalido,
}

/// <summary>
/// Lecturas de un submedidor en el periodo; los kWh son actual - anterior.
/// </summary>
public sealed record LecturasSubmedidor(decimal Anterior, decimal Actual)
{
    public decimal Kwh => Actual - Anterior;
}

/// <summary>
/// Resultado de un reparto. Las cuotas siguen el orden de los submedidores.
/// </summary>
public sealed record ResultadoReparto(
    IReadOnlyList<decimal> Kwh,
    IReadOnlyList<decimal> Cuotas,
    decimal TotalCobrado,
    decimal AbsorbePropietario,
    decimal KwhRepartidos,
    decimal? PrecioPromedioKwh,
    IReadOnlyList<AvisoReparto> Avisos)
{
    public bool PuedeGuardar => Avisos.All(a => !Repartidor.Bloquea(a));
}

/// <summary>
/// Reparte el total de un periodo entre submedidores. Función pura: no aplica los rangos de la tarifa
/// por inquilino, reparte el costo total del recibo (así nadie "gasta" el rango básico completo).
/// </summary>
public static class Repartidor
{
    public const decimal TotalMaximo = 10_000_000m;

    /// <summary>
    /// Diferencia tolerada entre la suma de submedidores y el principal antes de avisar.
    /// </summary>
    public const decimal ToleranciaKwh = 1m;

    public static bool Bloquea(AvisoReparto aviso) => aviso != AvisoReparto.SubmedidoresExcedenPrincipal;

    public static ResultadoReparto Calcular(
        decimal total,
        ModoReparto modo,
        decimal kwhPrincipal,
        IReadOnlyList<LecturasSubmedidor> submedidores)
    {
        ArgumentNullException.ThrowIfNull(submedidores);
        var kwh = submedidores.Select(s => s.Kwh).ToList();
        var ceros = submedidores.Select(_ => 0m).ToList();

        var avisos = new List<AvisoReparto>();
        if (total <= 0 || total > TotalMaximo)
        {
            avisos.Add(AvisoReparto.TotalInvalido);
        }

        if (submedidores.Any(s => s.Anterior < 0 || s.Actual < 0))
        {
            avisos.Add(AvisoReparto.LecturaNegativa);
        }

        if (submedidores.Any(s => s.Actual < s.Anterior))
        {
            avisos.Add(AvisoReparto.LecturaMenorQueAnterior);
        }

        if (avisos.Count > 0)
        {
            return new ResultadoReparto(kwh, ceros, 0m, 0m, 0m, null, avisos);
        }

        var suma = kwh.Sum();
        if (suma == 0)
        {
            return new ResultadoReparto(kwh, ceros, 0m, 0m, 0m, null, [AvisoReparto.SinConsumoSubmedido]);
        }

        if (suma > kwhPrincipal + ToleranciaKwh)
        {
            avisos.Add(AvisoReparto.SubmedidoresExcedenPrincipal);
        }

        // SoloMedido: lo que no miden los submedidores queda a cargo del propietario.
        var divisor = modo == ModoReparto.SoloMedido ? Math.Max(kwhPrincipal, suma) : suma;
        var objetivo = modo == ModoReparto.SoloMedido
            ? Math.Round(total * suma / divisor, 2, MidpointRounding.AwayFromZero)
            : total;

        var cuotas = MayorResto(kwh.Select(k => total * k / divisor).ToList(), objetivo);
        var cobrado = cuotas.Sum();
        return new ResultadoReparto(kwh, cuotas, cobrado, total - cobrado, suma, Math.Round(cobrado / suma, 4), avisos);
    }

    /// <summary>
    /// Redondea a centavos hacia abajo y reparte los centavos que faltan entre los mayores restos (en empate,
    /// el primero), para que la suma sea exactamente <paramref name="objetivo"/>.
    /// </summary>
    private static List<decimal> MayorResto(List<decimal> exactas, decimal objetivo)
    {
        var centavos = exactas.Select(e => Math.Floor(e * 100m)).ToList();
        var faltan = (int)(objetivo * 100m - centavos.Sum());
        var orden = exactas
            .Select((e, i) => (Resto: e * 100m - centavos[i], Indice: i))
            .OrderByDescending(x => x.Resto)
            .ThenBy(x => x.Indice)
            .ToList();

        for (var n = 0; n < faltan; n++)
        {
            centavos[orden[n % orden.Count].Indice] += 1m;
        }

        return centavos.Select(c => c / 100m).ToList();
    }
}
