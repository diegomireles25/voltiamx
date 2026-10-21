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

public static class SeccionesConfiguracion
{
    public const string Tarifas = "Tarifas";
}
