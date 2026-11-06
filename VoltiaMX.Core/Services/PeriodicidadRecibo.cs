using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// "¿Cada cuánto te llega el recibo?" (Fase 9c, D3): la opción que se preselecciona en el primer recibo. Lo que se
/// leyó del recibo (inicio y fin del periodo) manda; sin eso, una tarifa comercial es mensual y lo demás bimestral.
/// La persona siempre puede cambiarla.
/// </summary>
public static class PeriodicidadRecibo
{
    /// <summary>
    /// Hasta este número de días, el periodo leído se toma como mensual; más, como bimestral.
    /// </summary>
    public const int DiasMaximosMensual = 45;

    public static int Sugerir(DateOnly? inicio, DateOnly? fin, TipoTarifa? tipoTarifa)
    {
        if (inicio is DateOnly i && fin is DateOnly f && f > i)
        {
            return f.DayNumber - i.DayNumber + 1 <= DiasMaximosMensual ? 1 : 2;
        }

        return tipoTarifa == TipoTarifa.Comercial ? 1 : 2;
    }
}
