namespace VoltiaMX.Core.Services;

public interface ISimuladorAhorroService
{
    /// <summary>
    /// Calcula el escenario con los datos que recibe (sin acceso a la base). El escenario debe ser
    /// válido según <see cref="SimuladorValidador"/>.
    /// </summary>
    ResultadoSimulacion Simular(EntradaSimulacion entrada);

    /// <summary>
    /// Junta la tarifa, el consumo medido y el historial del lugar y simula. Null si el aparato no
    /// es de ese lugar o el lugar no tiene tarifa.
    /// </summary>
    Task<ResultadoSimulacion?> SimularAsync(
        int lugarId, int aparatoId, EscenarioAhorro escenario, DateOnly hoy, CancellationToken cancellationToken = default);
}
