using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Aparato tal como lo captura la persona. Sin <see cref="Id"/> es un alta.
/// </summary>
public sealed record AparatoEditable(
    int? Id,
    int LugarId,
    string Nombre,
    CategoriaAparato Categoria,
    int Cantidad,
    decimal Watts,
    decimal HorasPorDia,
    int DiasPorSemana,
    decimal CicloDeTrabajo);

/// <summary>
/// Con errores no se guardó nada y <see cref="Id"/> es el que traía el aparato.
/// </summary>
public sealed record ResultadoGuardarAparato(int? Id, IReadOnlyList<CodigoErrorAparato> Errores);

/// <summary>
/// Valores típicos de referencia para empezar a capturar un aparato. El nombre visible sale de
/// los recursos de la app con <see cref="Clave"/>.
/// </summary>
public sealed record PlantillaAparato(
    string Clave,
    CategoriaAparato Categoria,
    decimal Watts,
    decimal HorasPorDia,
    int DiasPorSemana,
    decimal CicloDeTrabajo);
