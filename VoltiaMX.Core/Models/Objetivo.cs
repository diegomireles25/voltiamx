namespace VoltiaMX.Core.Models;

/// <summary>
/// Objetivo del perfil y, opcionalmente, el monto máximo que quiere pagar por periodo.
/// </summary>
public class Objetivo
{
    public int Id { get; set; }

    public int PerfilId { get; set; }

    public PerfilUsuario? Perfil { get; set; }

    public TipoObjetivo Tipo { get; set; }

    // SIN USO EN LA INTERFAZ desde Fase 7a
    public decimal? PresupuestoMax { get; set; }
}
