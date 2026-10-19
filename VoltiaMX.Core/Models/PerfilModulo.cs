namespace VoltiaMX.Core.Models;

/// <summary>
/// Estado de un módulo para el perfil: hay una fila por cada <see cref="ModuloApp"/>.
/// </summary>
public class PerfilModulo
{
    public int Id { get; set; }

    public int PerfilId { get; set; }

    public PerfilUsuario? Perfil { get; set; }

    public ModuloApp Modulo { get; set; }

    public bool Activo { get; set; }
}
