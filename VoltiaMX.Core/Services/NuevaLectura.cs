using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Lectura que la persona quiere registrar. <paramref name="ValorKwh"/> es lo que marca el medidor;
/// <paramref name="MontoRecibo"/> es el total de su recibo, si lo tiene a la mano.
/// </summary>
public sealed record NuevaLectura(
    int LugarId,
    DateOnly Fecha,
    decimal ValorKwh,
    OrigenLectura Origen = OrigenLectura.Manual,
    decimal? MontoRecibo = null);
