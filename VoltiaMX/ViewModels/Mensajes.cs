using VoltiaMX.Core.Models;

namespace VoltiaMX.ViewModels;

/// <summary>
/// El onboarding se guardó: la app cambia a la ventana principal.
/// </summary>
public sealed record OnboardingTerminadoMensaje;

/// <summary>
/// La persona pidió repetir el onboarding desde Configuración.
/// </summary>
public sealed record RepetirOnboardingMensaje;

/// <summary>
/// Cambió el modo simple/avanzado: el menú del Shell se regenera.
/// </summary>
public sealed record ModulosCambiadosMensaje;

/// <summary>
/// Abrir un módulo desde otra pantalla (p. ej. "Capturar lectura" del Dashboard). El Shell selecciona
/// su elemento del menú; <paramref name="Seccion"/> indica dónde abrir la página, si aplica.
/// </summary>
public sealed record IrAModuloMensaje(ModuloApp Modulo, string? Seccion = null);

/// <summary>
/// Abrir Configuración desde otra pantalla; con <paramref name="Seccion"/> = Tarifas abre las tarifas
/// (dependen de Configuración, así el menú marca Configuración).
/// </summary>
public sealed record IrAConfiguracionMensaje(string? Seccion = null);

/// <summary>
/// Volver al Dashboard desde fuera del Shell (p. ej. al tocar una notificación de un módulo inactivo).
/// </summary>
public sealed record IrADashboardMensaje;

/// <summary>
/// Cambiaron datos que afectan los avisos (Dashboard cargado, lectura guardada, tarifa editada): se
/// vuelven a evaluar las notificaciones.
/// </summary>
public sealed record EvaluarAvisosMensaje;

/// <summary>
/// El Shell terminó de armar su menú: ya puede recibir mensajes de navegación.
/// </summary>
public sealed record ShellListoMensaje;

public static class SeccionesConfiguracion
{
    public const string Tarifas = "Tarifas";

    /// <summary>
    /// Reportes y exportación (también desde el módulo Reportes).
    /// </summary>
    public const string Reportes = "Reportes";

    /// <summary>
    /// Respaldo y restauración.
    /// </summary>
    public const string Respaldo = "Respaldo";

    /// <summary>
    /// Ayuda: qué significa cada palabra.
    /// </summary>
    public const string Ayuda = "Ayuda";
}

/// <summary>
/// Secciones del módulo Aparatos. El simulador no es un módulo del menú: se abre bajo Aparatos.
/// </summary>
public static class SeccionesAparatos
{
    public const string Simulador = "Simulador";

    private const string PrefijoSimulador = Simulador + ":";

    /// <summary>
    /// El simulador con el aparato ya elegido.
    /// </summary>
    public static string SimuladorDe(int aparatoId) => PrefijoSimulador + aparatoId;

    public static bool EsSimulador(string? seccion)
        => seccion == Simulador || seccion?.StartsWith(PrefijoSimulador, StringComparison.Ordinal) == true;

    public static int? AparatoDe(string? seccion)
        => seccion?.StartsWith(PrefijoSimulador, StringComparison.Ordinal) == true
            && int.TryParse(seccion[PrefijoSimulador.Length..], out var id)
                ? id
                : null;

    // "Elegir mis aparatos" (Fase 9d, D1): de dónde se abrió, para volver ahí al guardar o cancelar.
    public const string ElegirDesdeAparatos = "Elegir:Aparatos";

    public const string ElegirDesdeInicio = "Elegir:Inicio";

    public static bool EsElegir(string? seccion) => seccion is ElegirDesdeAparatos or ElegirDesdeInicio;

    public static bool ElegirVuelveAInicio(string? seccion) => seccion == ElegirDesdeInicio;
}

/// <summary>
/// Secciones del módulo Inquilinos: el reparto (nuevo o uno guardado, de solo lectura) se abre bajo Inquilinos.
/// </summary>
public static class SeccionesInquilinos
{
    public const string NuevoReparto = "Reparto";

    private const string PrefijoReparto = NuevoReparto + ":";

    public static string VerReparto(int repartoId) => PrefijoReparto + repartoId;

    public static bool EsReparto(string? seccion)
        => seccion == NuevoReparto || seccion?.StartsWith(PrefijoReparto, StringComparison.Ordinal) == true;

    public static int? RepartoDe(string? seccion)
        => seccion?.StartsWith(PrefijoReparto, StringComparison.Ordinal) == true
            && int.TryParse(seccion[PrefijoReparto.Length..], out var id)
                ? id
                : null;
}
