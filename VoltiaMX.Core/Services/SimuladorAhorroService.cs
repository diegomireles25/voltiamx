using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Core.Services;

public sealed class SimuladorAhorroService(
    IDbContextFactory<VoltiaDbContext> fabrica,
    ITarifaCalculator calculadora,
    IDacService dac) : ISimuladorAhorroService
{
    public const int MesesPorAnio = 12;

    /// <summary>
    /// Más de 10 años de recuperación se muestra como "más de 10 años".
    /// </summary>
    public const int MesesMaximosRecuperacion = 120;

    public ResultadoSimulacion Simular(EntradaSimulacion entrada)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        if (SimuladorValidador.Validar(entrada.Aparato, entrada.Escenario).Count > 0)
        {
            throw new ArgumentException("El escenario no es válido.", nameof(entrada));
        }

        var meses = Math.Max(entrada.MesesPorPeriodo, 1);
        var antes = ConsumoAparato.KwhPorMes(entrada.Aparato);
        var despues = ConsumoAparato.KwhPorMes(entrada.Escenario.Aplicar(entrada.Aparato));
        var ahorroMes = antes - despues;

        var estado = ahorroMes < 0m ? EstadoSimulacion.ConsumiriasMas
            : entrada.KwhMedidoPorMes is not > 0m ? EstadoSimulacion.SinConsumoMedido
            : EstadoSimulacion.Normal;

        // Pesos con la tarifa real: el ahorro vale lo que cuestan los kWh de arriba del consumo.
        decimal? pesosPeriodo = null;
        var ahorroAplicadoMes = ahorroMes;
        if (entrada.KwhMedidoPorMes is > 0m and decimal medido)
        {
            (pesosPeriodo, ahorroAplicadoMes, var excede) = AhorroConTarifa.Pesos(calculadora, entrada.Tarifa, meses, medido, ahorroMes);
            if (excede)
            {
                estado = EstadoSimulacion.AhorroExcedeConsumo;
            }
        }

        var pesosMes = pesosPeriodo / meses;
        var precio = entrada.Escenario.PrecioEquipo;
        decimal? recuperacion = precio > 0m && pesosMes > 0m && estado != EstadoSimulacion.ConsumiriasMas
            ? precio / pesosMes
            : null;

        var (dacAntes, dacDespues) = AhorroConTarifa.EfectoDac(dac, entrada.Tarifa, entrada.HistorialMensual, ahorroAplicadoMes);

        return new ResultadoSimulacion(
            antes,
            despues,
            ahorroMes,
            estado,
            pesosPeriodo,
            pesosMes,
            pesosMes * MesesPorAnio,
            recuperacion,
            recuperacion > MesesMaximosRecuperacion,
            dacAntes,
            dacDespues);
    }

    public async Task<ResultadoSimulacion?> SimularAsync(
        int lugarId, int aparatoId, EscenarioAhorro escenario, DateOnly hoy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(escenario);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var lugar = await db.Lugares
            .AsNoTracking()
            .Include(l => l.Tarifa).ThenInclude(t => t!.Rangos)
            .Include(l => l.Lecturas)
            .Include(l => l.Periodos)
            .Include(l => l.Aparatos)
            .AsSplitQuery()
            .FirstOrDefaultAsync(l => l.Id == lugarId, cancellationToken);

        if (lugar?.Tarifa is null || lugar.Aparatos.FirstOrDefault(a => a.Id == aparatoId) is not { } aparato)
        {
            return null;
        }

        return Simular(new EntradaSimulacion(
            aparato,
            escenario,
            lugar.Tarifa,
            lugar.MesesPorPeriodo,
            ConsumoMedido.KwhPorMes(lugar, hoy),
            HistorialMensualDac.Desde(lugar.Periodos, Math.Max(lugar.MesesPorPeriodo, 1))));
    }
}
