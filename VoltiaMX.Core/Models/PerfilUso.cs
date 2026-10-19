namespace VoltiaMX.Core.Models;

/// <summary>
/// Uso elegido en el onboarding. Se guarda aparte de los módulos porque el modo avanzado
/// activa módulos extra y los usos no se podrían deducir de ellos.
/// </summary>
public class PerfilUso
{
    public int Id { get; set; }

    public int PerfilId { get; set; }

    public PerfilUsuario? Perfil { get; set; }

    public UsoApp Uso { get; set; }
}
