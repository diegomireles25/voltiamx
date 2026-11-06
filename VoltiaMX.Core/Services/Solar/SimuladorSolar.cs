using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Supuestos de la estimación. Todos son editables; los valores por defecto son de ejemplo.
/// <see cref="SuperficiePanelM2"/> y <see cref="EspacioPasoPorciento"/> (Fase 9e) solo sirven para el espacio que
/// ocupan los paneles.
/// </summary>
public sealed record ParametrosSolar(
    decimal? ConsumoMensualKwh,
    decimal PorcentajeCubrir,
    decimal HorasSol,
    decimal Eficiencia,
    decimal WattsPanel,
    decimal? CostoPorKwp,
    decimal SuperficiePanelM2 = ValoresSolarPorDefecto.SuperficiePanelM2,
    decimal EspacioPasoPorciento = ValoresSolarPorDefecto.EspacioPasoPorciento);

/// <summary>
/// Valores con los que arranca el simulador cada vez. NO son datos de México ni de CFE: la persona los cambia
/// por los de su ciudad y su instalador.
/// </summary>
public static class ValoresSolarPorDefecto
{
    public const decimal PorcentajeCubrir = 100m; // EJEMPLO
    public const decimal HorasSol = 5m; // EJEMPLO: horas de sol pico por día
    public const decimal Eficiencia = 0.80m; // EJEMPLO: pérdidas por temperatura, inversor y cableado
    public const decimal WattsPanel = 550m; // EJEMPLO
    public const decimal SuperficiePanelM2 = 2.6m; // EJEMPLO: superficie de un panel de unos 550 W
    public const decimal EspacioPasoPorciento = 10m; // EJEMPLO: espacio para pasar entre filas
}

public enum CodigoErrorSolar
{
    ConsumoRequerido,
    ConsumoFueraDeRango,
    PorcentajeFueraDeRango,
    HorasFueraDeRango,
    EficienciaFueraDeRango,
    WattsFueraDeRango,
    SuperficieFueraDeRango,
    PasoFueraDeRango,
    CostoFueraDeRango,
}

public static class SimuladorSolarValidador
{
    public const decimal ConsumoMaximo = 100_000m;
    public const decimal PorcentajeMinimo = 10m;
    public const decimal PorcentajeMaximo = 100m;
    public const decimal HorasMinimas = 1m;
    public const decimal HorasMaximas = 9m;
    public const decimal EficienciaMinima = 0.50m;
    public const decimal EficienciaMaxima = 0.95m;
    public const decimal WattsMinimos = 100m;
    public const decimal WattsMaximos = 800m;
    public const decimal CostoMaximo = 1_000_000m;
    public const decimal SuperficieMinima = 1m;
    public const decimal SuperficieMaxima = 4m;
    public const decimal PasoMinimo = 0m;
    public const decimal PasoMaximo = 30m;

    /// <summary>
    /// Errores en el orden de los campos de la pantalla.
    /// </summary>
    public static IReadOnlyList<CodigoErrorSolar> Validar(ParametrosSolar p)
    {
        ArgumentNullException.ThrowIfNull(p);
        var errores = new List<CodigoErrorSolar>();
        if (p.ConsumoMensualKwh is not decimal consumo)
        {
            errores.Add(CodigoErrorSolar.ConsumoRequerido);
        }
        else if (consumo <= 0m || consumo > ConsumoMaximo)
        {
            errores.Add(CodigoErrorSolar.ConsumoFueraDeRango);
        }

        if (p.PorcentajeCubrir is < PorcentajeMinimo or > PorcentajeMaximo)
        {
            errores.Add(CodigoErrorSolar.PorcentajeFueraDeRango);
        }

        if (p.HorasSol is < HorasMinimas or > HorasMaximas)
        {
            errores.Add(CodigoErrorSolar.HorasFueraDeRango);
        }

        if (p.Eficiencia is < EficienciaMinima or > EficienciaMaxima)
        {
            errores.Add(CodigoErrorSolar.EficienciaFueraDeRango);
        }

        if (p.WattsPanel is < WattsMinimos or > WattsMaximos)
        {
            errores.Add(CodigoErrorSolar.WattsFueraDeRango);
        }

        if (p.SuperficiePanelM2 is < SuperficieMinima or > SuperficieMaxima)
        {
            errores.Add(CodigoErrorSolar.SuperficieFueraDeRango);
        }

        if (p.EspacioPasoPorciento is < PasoMinimo or > PasoMaximo)
        {
            errores.Add(CodigoErrorSolar.PasoFueraDeRango);
        }

        if (p.CostoPorKwp is < 0m or > CostoMaximo)
        {
            errores.Add(CodigoErrorSolar.CostoFueraDeRango);
        }

        return errores;
    }
}

/// <summary>
/// Lo que el lugar aporta a la estimación: consumo medido, tarifa y el historial con el que se evalúa el DAC.
/// <see cref="ConsumoAproximado"/> (Fase 9e, D2): el consumo sale de un recibo por monto, sin lecturas.
/// </summary>
public sealed record DatosLugarSolar(
    string NombreLugar,
    bool EsDemo,
    decimal? KwhMedidoPorMes,
    Tarifa? Tarifa,
    int MesesPorPeriodo,
    IReadOnlyList<decimal> HistorialMensual,
    bool ConsumoAproximado = false)
{
    public static DatosLugarSolar SinLugar { get; } = new(string.Empty, false, null, null, 1, []);
}

public enum EstadoPesosSolar
{
    Normal,

    /// <summary>
    /// El lugar no tiene tarifa: solo kWh.
    /// </summary>
    SinTarifa,

    /// <summary>
    /// No hay consumo medido (el consumo se escribió a mano): solo kWh.
    /// </summary>
    SinConsumoMedido,
}

public sealed record ResultadoSolar(
    decimal GeneracionPorKwpMes,
    decimal KwpNecesarios,
    int Paneles,
    decimal KwpInstalados,
    decimal GeneracionMensualKwh,
    decimal KwhAprovechadosMes,
    decimal ExcedenteKwhMes,
    EstadoPesosSolar EstadoPesos,
    decimal? AhorroPeriodo,
    decimal? AhorroMes,
    decimal? AhorroAnio,
    decimal? Inversion,
    decimal? RecuperacionMeses,
    bool RecuperacionExcedeMaximo,
    EvaluacionDac? DacAntes,
    EvaluacionDac? DacDespues,
    decimal EspacioM2 = 0m);

/// <summary>
/// Estimación de un sistema solar: paneles, generación, ahorro con la tarifa real (sin IVA ni DAP),
/// recuperación y efecto en DAC. Es una estimación con supuestos editables, no una cotización. No supone
/// aumentos de tarifa, degradación de los paneles, vida útil, inflación, subsidios ni financiamiento.
/// </summary>
public static class SimuladorSolar
{
    /// <summary>
    /// Días promedio de un mes (365 / 12 = 30.4167). En las cuentas se multiplica por 365 y se divide entre 12
    /// al final, para que el resultado se pueda reproducir a mano sin redondeos intermedios.
    /// </summary>
    public const decimal DiasPorMes = 365m / 12m;

    private const decimal DiasPorAnio = 365m;
    private const decimal MesesPorAnio = 12m;

    /// <summary>
    /// Más de 20 años de recuperación se muestra como "más de 20 años".
    /// </summary>
    public const int MesesMaximosRecuperacion = 240;

    public static ResultadoSolar Calcular(ParametrosSolar p, DatosLugarSolar datos, ITarifaCalculator calculadora, IDacService dac)
    {
        ArgumentNullException.ThrowIfNull(p);
        ArgumentNullException.ThrowIfNull(datos);
        if (SimuladorSolarValidador.Validar(p).Count > 0)
        {
            throw new ArgumentException("Los parámetros no son válidos.", nameof(p));
        }

        var consumo = p.ConsumoMensualKwh!.Value;
        var horasPorEficiencia = p.HorasSol * p.Eficiencia;

        // Generación de 1 kWp al mes = horas × eficiencia × 365 / 12.
        var generacionPorKwp = horasPorEficiencia * DiasPorAnio / MesesPorAnio;
        var aCubrir = consumo * p.PorcentajeCubrir / 100m;
        var kwpNecesarios = aCubrir * MesesPorAnio / (horasPorEficiencia * DiasPorAnio);

        // Paneles = techo(kWp necesarios × 1000 / watts), al menos 1.
        var paneles = Math.Max(1, (int)Math.Ceiling(aCubrir * MesesPorAnio * 1000m / (horasPorEficiencia * DiasPorAnio * p.WattsPanel)));
        var kwpInstalados = paneles * p.WattsPanel / 1000m;
        var generacion = kwpInstalados * horasPorEficiencia * DiasPorAnio / MesesPorAnio;

        // Lo que excede el consumo no se valora en pesos.
        var aprovechados = Math.Min(generacion, consumo);
        var excedente = generacion - aprovechados;

        var estado = datos.Tarifa is null ? EstadoPesosSolar.SinTarifa
            : datos.KwhMedidoPorMes is not > 0m ? EstadoPesosSolar.SinConsumoMedido
            : EstadoPesosSolar.Normal;

        var meses = Math.Max(datos.MesesPorPeriodo, 1);
        decimal? ahorroPeriodo = null;
        if (estado == EstadoPesosSolar.Normal)
        {
            ahorroPeriodo = AhorroConTarifa.Pesos(calculadora, datos.Tarifa!, meses, consumo, aprovechados).PesosPeriodo;
        }

        var ahorroMes = ahorroPeriodo / meses;
        decimal? inversion = p.CostoPorKwp is > 0m and decimal costo ? kwpInstalados * costo : null;
        decimal? recuperacion = inversion > 0m && ahorroMes > 0m ? inversion / ahorroMes : null;

        var (dacAntes, dacDespues) = datos.Tarifa is null
            ? (null, null)
            : AhorroConTarifa.EfectoDac(dac, datos.Tarifa, datos.HistorialMensual, aprovechados);

        return new ResultadoSolar(
            generacionPorKwp,
            kwpNecesarios,
            paneles,
            kwpInstalados,
            generacion,
            aprovechados,
            excedente,
            estado,
            ahorroPeriodo,
            ahorroMes,
            ahorroMes * MesesPorAnio,
            inversion,
            recuperacion,
            recuperacion > MesesMaximosRecuperacion,
            dacAntes,
            dacDespues,
            EspacioSolar.Calcular(paneles, p.SuperficiePanelM2, p.EspacioPasoPorciento));
    }
}
