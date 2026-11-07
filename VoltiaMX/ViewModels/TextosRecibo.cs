using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// El mensaje de error de un recibo por monto (Fase 9i): los que chocan con otro recibo dicen con cuál, con sus fechas.
/// </summary>
public static class TextosRecibo
{
    public static string Error(ITextos textos, ResultadoReciboPorMonto resultado)
    {
        ArgumentNullException.ThrowIfNull(textos);
        ArgumentNullException.ThrowIfNull(resultado);
        var clave = $"ReciboMonto_Error_{resultado.Error}";
        return resultado switch
        {
            { Error: ErrorReciboPorMonto.CorteRepetido or ErrorReciboPorMonto.CorteMuyCerca, ChoqueFin: DateOnly fin }
                => textos.Formato(clave, fin),
            { Error: ErrorReciboPorMonto.DentroDeLecturas, ChoqueInicio: DateOnly inicio, ChoqueFin: DateOnly fin }
                => textos.Formato(clave, inicio, fin),
            _ => textos[clave],
        };
    }
}
