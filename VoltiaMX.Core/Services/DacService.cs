namespace VoltiaMX.Core.Services;

public sealed class DacService : IDacService
{
    private const int MesesDelPromedio = 12;

    private readonly UmbralesDac _umbrales;

    public DacService()
        : this(new UmbralesDac())
    {
    }

    public DacService(UmbralesDac umbrales)
    {
        ArgumentNullException.ThrowIfNull(umbrales);
        if (umbrales.Medio <= 0m || umbrales.Medio >= umbrales.Alto)
        {
            throw new ArgumentException("El umbral Medio debe ser positivo y menor que el Alto.", nameof(umbrales));
        }

        _umbrales = umbrales;
    }

    public EvaluacionDac Evaluar(IReadOnlyList<decimal> consumosMensualesKwh, decimal limiteKwhMensual)
    {
        ArgumentNullException.ThrowIfNull(consumosMensualesKwh);
        if (consumosMensualesKwh.Count == 0)
        {
            throw new ArgumentException("Se necesita al menos un mes de consumo.", nameof(consumosMensualesKwh));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limiteKwhMensual);

        var promedio = consumosMensualesKwh.TakeLast(MesesDelPromedio).Average();
        var proporcion = promedio / limiteKwhMensual;
        var nivel = proporcion >= _umbrales.Alto ? NivelRiesgoDac.Alto
            : proporcion >= _umbrales.Medio ? NivelRiesgoDac.Medio
            : NivelRiesgoDac.Bajo;

        return new EvaluacionDac(nivel, promedio, limiteKwhMensual - promedio, promedio > limiteKwhMensual);
    }
}
