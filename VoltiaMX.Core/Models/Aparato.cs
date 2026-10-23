namespace VoltiaMX.Core.Models;

/// <summary>
/// Aparato de un lugar con su uso típico, para estimar cuánto consume al mes.
/// </summary>
public class Aparato
{
    public int Id { get; set; }

    public int LugarId { get; set; }

    public Lugar? Lugar { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public CategoriaAparato Categoria { get; set; }

    /// <summary>
    /// Cuántos aparatos iguales hay (p. ej. 12 focos).
    /// </summary>
    public int Cantidad { get; set; } = 1;

    /// <summary>
    /// Potencia mientras funciona, en watts.
    /// </summary>
    public decimal Watts { get; set; }

    public decimal HorasPorDia { get; set; }

    public int DiasPorSemana { get; set; } = 7;

    /// <summary>
    /// Fracción del tiempo encendido en que consume su potencia (0.05 a 1). Los refrigeradores y
    /// los aires acondicionados ciclan: el compresor arranca y se apaga.
    /// </summary>
    public decimal CicloDeTrabajo { get; set; } = 1m;
}
