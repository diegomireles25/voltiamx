using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Core.Services;

public interface ISimuladorSolarService
{
    /// <summary>
    /// Consumo medido, tarifa, meses del periodo e historial DAC del lugar (los mismos que usan el Dashboard
    /// y el simulador de ahorro). Sin lugar, datos vacíos.
    /// </summary>
    Task<DatosLugarSolar> ObtenerDatosAsync(int? lugarId, DateOnly hoy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calcula con <see cref="SimuladorSolar"/>; los parámetros deben ser válidos.
    /// </summary>
    ResultadoSolar Calcular(ParametrosSolar parametros, DatosLugarSolar datos);
}

public sealed class SimuladorSolarService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    ITarifaCalculator calculadora,
    IDacService dac) : ISimuladorSolarService
{
    public async Task<DatosLugarSolar> ObtenerDatosAsync(int? lugarId, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        if (lugarId is not int id)
        {
            return DatosLugarSolar.SinLugar;
        }

        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Lecturas)
            .Include(l => l.Periodos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lugar is null)
        {
            return DatosLugarSolar.SinLugar;
        }

        var meses = Math.Max(lugar.MesesPorPeriodo, 1);
        return new DatosLugarSolar(
            lugar.Nombre,
            lugar.EsDemo,
            ConsumoMedido.KwhPorMes(lugar, hoy),
            lugar.Tarifa,
            meses,
            HistorialMensualDac.Desde(lugar.Periodos, meses),
            ConsumoMedido.EsAproximado(lugar, hoy));
    }

    public ResultadoSolar Calcular(ParametrosSolar parametros, DatosLugarSolar datos)
        => SimuladorSolar.Calcular(parametros, datos, calculadora, dac);
}
