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
