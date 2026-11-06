using System.Globalization;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Aviso por enviar, sin texto: la app arma el título y el cuerpo con sus recursos.
/// </summary>
public sealed record AvisoPendiente(
    TipoAviso Tipo,
    string Clave,
    DateOnly FinPeriodo,
    int? DiasParaCorte = null,
    bool PeriodoTerminado = false,
    NivelRiesgoDac? NivelDac = null,
    EstadoMeta? EstadoMeta = null);

/// <summary>
/// Clave enviada que hay que olvidar para poder avisar de nuevo (el DAC bajó de nivel).
/// </summary>
public sealed record ClaveAviso(TipoAviso Tipo, string Clave);

public sealed record ResultadoAvisos(IReadOnlyList<AvisoPendiente> Pendientes, IReadOnlyList<ClaveAviso> ARestablecer)
{
    public static readonly ResultadoAvisos Ninguno = new([], []);
}

/// <summary>
/// Qué avisos tocan para el lugar principal, con lo ya calculado del Resumen y de la meta. Función pura:
/// no lee la base ni arma textos. Cada clave se avisa una sola vez.
/// </summary>
public static class EvaluadorAvisos
{
    /// <summary>
    /// Se avisa de la lectura cuando faltan estos días o menos para el corte.
    /// </summary>
    public const int DiasAntesDelCorte = 3;

    private static readonly NivelRiesgoDac[] NivelesDeAviso = [NivelRiesgoDac.Medio, NivelRiesgoDac.Alto];

    /// <param name="clavesEnviadas">Claves que ya se avisaron en este lugar.</param>
    /// <param name="notificacionesActivas">Preferencia del perfil; apagada no avisa ni olvida nada.</param>
    /// <param name="metasActivas">El módulo Metas está activo (modo avanzado): sin él no hay avisos de meta.</param>
    public static ResultadoAvisos Evaluar(
        ResumenDashboard? resumen,
        ProgresoMeta? meta,
        DateOnly hoy,
        IReadOnlySet<string> clavesEnviadas,
        bool notificacionesActivas,
        bool metasActivas)
    {
        ArgumentNullException.ThrowIfNull(clavesEnviadas);
        if (!notificacionesActivas || resumen is null || resumen.EsDemo)
        {
            return ResultadoAvisos.Ninguno;
        }

        var fin = resumen.FinPeriodo;
        var finTexto = fin.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var candidatos = new List<AvisoPendiente>();

        // Lectura: el periodo ya terminó o el corte está cerca.
        if (resumen.PeriodoTerminado)
        {
            candidatos.Add(new AvisoPendiente(TipoAviso.Lectura, $"Lectura:{finTexto}:terminado", fin, resumen.DiasParaCorte, PeriodoTerminado: true));
        }
        else if (resumen.DiasParaCorte <= DiasAntesDelCorte)
        {
            candidatos.Add(new AvisoPendiente(TipoAviso.Lectura, $"Lectura:{finTexto}:faltan", fin, resumen.DiasParaCorte));
        }

        // DAC: se avisa al llegar a Medio o Alto; los niveles de arriba se olvidan para volver a avisar si sube.
        var restablecer = new List<ClaveAviso>();
        if (resumen.Dac is { } dac)
        {
            if (dac.Nivel is NivelRiesgoDac.Medio or NivelRiesgoDac.Alto)
            {
                candidatos.Add(new AvisoPendiente(TipoAviso.Dac, ClaveDac(dac.Nivel), fin, NivelDac: dac.Nivel));
            }

            restablecer.AddRange(NivelesDeAviso.Where(n => n > dac.Nivel).Select(n => new ClaveAviso(TipoAviso.Dac, ClaveDac(n))));
        }

        // Meta: en riesgo o superada en este periodo.
        if (metasActivas && meta?.Estado is EstadoMeta.EnRiesgo or EstadoMeta.Superada)
        {
            candidatos.Add(new AvisoPendiente(TipoAviso.Meta, $"Meta:{meta.Estado}:{finTexto}", fin, EstadoMeta: meta.Estado));
        }

        // Desde la Fase 7a ya no se avisa del presupuesto: TipoAviso.Presupuesto se conserva solo para las filas
        // viejas de AvisosEnviados.

        return new ResultadoAvisos(candidatos.Where(a => !clavesEnviadas.Contains(a.Clave)).ToList(), restablecer);
    }

    private static string ClaveDac(NivelRiesgoDac nivel) => $"Dac:{nivel}";
}
