using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Una plantilla con su nombre visible (y los nombres que tuvo antes, para reconocer aparatos guardados con ellos).
/// </summary>
public sealed record PlantillaConNombre(PlantillaAparato Plantilla, string Nombre, IReadOnlyList<string> NombresAnteriores);

/// <summary>
/// Un aparato típico en "Elegir mis aparatos": su plantilla, su nombre y el aparato que ya existe (o null).
/// </summary>
public sealed record AparatoTipico(PlantillaAparato Plantilla, string Nombre, Aparato? Existente);

/// <summary>
/// Un aparato marcado con lo que dijo la persona: horas al día y cuántos tiene. <see cref="Id"/> es el del aparato
/// que ya existe (se actualiza) o null (es nuevo).
/// </summary>
public sealed record AparatoElegido(PlantillaAparato Plantilla, string Nombre, decimal HorasPorDia, int Cantidad, int? Id);

/// <summary>
/// Errores por clave de plantilla; sin errores, se guardó.
/// </summary>
public sealed record ResultadoGuardarElegidos(IReadOnlyDictionary<string, IReadOnlyList<CodigoErrorAparato>> Errores)
{
    public bool Guardado => Errores.Count == 0;
}

/// <summary>
/// "Elegir mis aparatos" (Fase 9d, D1): qué aparatos típicos ya tiene la casa y cómo queda cada uno al guardar.
/// </summary>
public static class AparatosTipicos
{
    /// <summary>
    /// Para cada plantilla, el aparato guardado con su nombre (o uno anterior), sin importar mayúsculas ni espacios.
    /// Cada aparato se empareja una sola vez; los que tienen un nombre propio no aparecen.
    /// </summary>
    public static IReadOnlyList<AparatoTipico> Emparejar(IEnumerable<PlantillaConNombre> plantillas, IEnumerable<Aparato> aparatos)
    {
        ArgumentNullException.ThrowIfNull(plantillas);
        ArgumentNullException.ThrowIfNull(aparatos);
        var libres = aparatos.OrderBy(a => a.Id).ToList();
        var resultado = new List<AparatoTipico>();
        foreach (var plantilla in plantillas)
        {
            var nombres = plantilla.NombresAnteriores.Prepend(plantilla.Nombre).Select(Normalizar).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
            var existente = libres.FirstOrDefault(a => nombres.Contains(Normalizar(a.Nombre)));
            if (existente is not null)
            {
                libres.Remove(existente);
            }

            resultado.Add(new AparatoTipico(plantilla.Plantilla, plantilla.Nombre, existente));
        }

        return resultado;
    }

    /// <summary>
    /// Cómo queda un aparato elegido: nuevo, con los valores típicos de su plantilla; existente, con lo que ya tenía
    /// salvo las horas y la cantidad.
    /// </summary>
    public static AparatoEditable Editable(AparatoElegido elegido, int lugarId, Aparato? existente)
    {
        ArgumentNullException.ThrowIfNull(elegido);
        return existente is null
            ? new AparatoEditable(
                null, lugarId, elegido.Nombre, elegido.Plantilla.Categoria, elegido.Cantidad, elegido.Plantilla.Watts,
                elegido.HorasPorDia, elegido.Plantilla.DiasPorSemana, elegido.Plantilla.CicloDeTrabajo)
            : new AparatoEditable(
                existente.Id, lugarId, existente.Nombre, existente.Categoria, elegido.Cantidad, existente.Watts,
                elegido.HorasPorDia, existente.DiasPorSemana, existente.CicloDeTrabajo);
    }

    private static string Normalizar(string nombre) => string.Join(' ', (nombre ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
