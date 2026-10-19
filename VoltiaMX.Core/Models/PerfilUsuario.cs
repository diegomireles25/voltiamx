namespace VoltiaMX.Core.Models;

/// <summary>
/// Perfil único de la persona que usa la app en este equipo.
/// </summary>
public class PerfilUsuario
{
    public int Id { get; set; }

    public string? Nombre { get; set; }

    public bool OnboardingCompletado { get; set; }

    public bool ModoAvanzado { get; set; }

    /// <summary>
    /// Preferencia guardada en el onboarding; los avisos llegan en una fase posterior.
    /// </summary>
    public bool NotificacionesActivas { get; set; }

    public List<PerfilUso> Usos { get; set; } = [];

    public List<PerfilModulo> Modulos { get; set; } = [];

    public Objetivo? Objetivo { get; set; }
}
