using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

/// <summary>
/// Una plantilla con su nombre visible (y los nombres que tuvo antes, para reconocer aparatos guardados con ellos).
/// </summary>
public sealed record PlantillaConNombre(PlantillaAparato Plantilla, string Nombre, IReadOnlyList<string> NombresAnteriores);

/// <summary>
/// Un aparato típico en "Elegir mis aparatos": su plantilla, su nombre y los aparatos guardados que le corresponden
/// (Fase 9g): uno con su cantidad ("mismas horas") o varias unidades llamadas "{nombre} · {apodo}".
/// </summary>
public sealed record AparatoTipico(PlantillaAparato Plantilla, string Nombre, IReadOnlyList<Aparato> Registros)
{
    /// <summary>
    /// El primer registro (o null): el aparato de la 9d, que conserva su potencia al guardar.
    /// </summary>
    public Aparato? Existente => Registros.Count > 0 ? Registros[0] : null;
}

/// <summary>
/// Una unidad de un aparato típico que se usa distinto (Fase 9g): su apodo (opcional) y sus horas al día.
/// <see cref="Id"/> es el del aparato que ya existe (se actualiza) o null (es nuevo).
/// </summary>
public sealed record UnidadElegida(int? Id, string Apodo, decimal HorasPorDia);

/// <summary>
/// Un aparato marcado con lo que dijo la persona: horas al día y cuántos tiene. <see cref="Id"/> es el del aparato
/// que ya existe (se actualiza) o null (es nuevo). Fase 9g: <see cref="Otros"/> son los demás registros guardados de
/// la misma plantilla y <see cref="Unidades"/>, si se usan distinto, una por unidad (null = mismas horas).
/// </summary>
public sealed record AparatoElegido(
    PlantillaAparato Plantilla,
    string Nombre,
    decimal HorasPorDia,
    int Cantidad,
    int? Id,
    IReadOnlyList<int>? Otros = null,
    IReadOnlyList<UnidadElegida>? Unidades = null);

public enum TipoCambioAparato
{
    /// <summary>
    /// Varias unidades pasan a ser un aparato con las mismas horas (las del primero).
    /// </summary>
    AUnoConMismasHoras,

    /// <summary>
    /// Un aparato con su cantidad pasa a ser una unidad por aparato, cada una con sus horas.
    /// </summary>
    AUnidades,

    /// <summary>
    /// Se quitan unidades (la cantidad bajó).
    /// </summary>
    SoloQuitar,
}

/// <summary>
/// Lo que se reemplaza al guardar un aparato típico (para la confirmación, Fase 9g): el tipo, cuántos registros había
/// y cuántos quedan, las horas y la cantidad del que queda y las unidades que se quitan.
/// </summary>
public sealed record CambioAparato(
    TipoCambioAparato Tipo,
    string Nombre,
    int RegistrosAntes,
    int RegistrosDespues,
    decimal HorasPorDia,
    int Cantidad,
    IReadOnlyList<string> Quitadas);

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
    /// Separa el nombre del aparato típico del apodo de una unidad: "Aire acondicionado · Sala" (Fase 9g).
    /// </summary>
    public const string SeparadorUnidad = " · ";

    /// <summary>
    /// Para cada plantilla, los aparatos guardados con su nombre (o uno anterior) o que empiezan con
    /// "{nombre} · " (sus unidades), sin importar mayúsculas ni espacios. Cada aparato se empareja una sola vez; los
    /// que tienen un nombre propio no aparecen.
    /// </summary>
    public static IReadOnlyList<AparatoTipico> Emparejar(IEnumerable<PlantillaConNombre> plantillas, IEnumerable<Aparato> aparatos)
    {
        ArgumentNullException.ThrowIfNull(plantillas);
        ArgumentNullException.ThrowIfNull(aparatos);
        var libres = aparatos.OrderBy(a => a.Id).ToList();
        var resultado = new List<AparatoTipico>();
        foreach (var plantilla in plantillas)
        {
            var nombres = plantilla.NombresAnteriores.Prepend(plantilla.Nombre).Select(Normalizar).ToList();
            var registros = libres.Where(a => nombres.Any(n => EsDe(a.Nombre, n))).ToList();
            libres.RemoveAll(registros.Contains);
            resultado.Add(new AparatoTipico(plantilla.Plantilla, plantilla.Nombre, registros));
        }

        return resultado;
    }

    /// <summary>
    /// El nombre de una unidad: "{nombre} · {apodo}" o, sin apodo, "{nombre} · {número}". El apodo se recorta para que
    /// el nombre quepa en <see cref="AparatoValidador.LargoMaximoNombre"/>.
    /// </summary>
    public static string NombreUnidad(string nombre, string? apodo, int numero)
    {
        var limpio = Normalizar(apodo ?? string.Empty);
        var sufijo = limpio.Length > 0 ? limpio : numero.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var cabe = AparatoValidador.LargoMaximoNombre - nombre.Length - SeparadorUnidad.Length;
        if (cabe > 0 && sufijo.Length > cabe)
        {
            sufijo = sufijo[..cabe].TrimEnd();
        }

        return nombre + SeparadorUnidad + sufijo;
    }

    /// <summary>
    /// El apodo de una unidad (lo que va después de "{nombre} · "), o vacío si no es una unidad.
    /// </summary>
    public static string ApodoDe(string nombreRegistro, string nombre)
    {
        var limpio = Normalizar(nombreRegistro);
        var prefijo = Normalizar(nombre) + SeparadorUnidad;
        return limpio.StartsWith(prefijo, StringComparison.CurrentCultureIgnoreCase) ? limpio[prefijo.Length..].Trim() : string.Empty;
    }

    /// <summary>
    /// Al volver de "cada uno distinto" a "mismas horas" (precisión 1 de la Fase 9g): las horas del PRIMER registro y
    /// la suma de las cantidades.
    /// </summary>
    public static (decimal HorasPorDia, int Cantidad) MismasHoras(IReadOnlyList<Aparato> registros)
    {
        ArgumentNullException.ThrowIfNull(registros);
        return registros.Count == 0 ? (0m, 0) : (registros[0].HorasPorDia, registros.Sum(r => r.Cantidad));
    }

    /// <summary>
    /// Lo que se reemplaza al guardar, para pedir una sola confirmación; null si no hay nada que confirmar.
    /// <paramref name="registros"/> son los aparatos guardados de la plantilla.
    /// </summary>
    public static CambioAparato? Cambios(AparatoElegido elegido, IReadOnlyList<Aparato> registros)
    {
        ArgumentNullException.ThrowIfNull(elegido);
        ArgumentNullException.ThrowIfNull(registros);
        if (elegido.Unidades is not { } unidades)
        {
            return registros.Count > 1
                ? new CambioAparato(TipoCambioAparato.AUnoConMismasHoras, elegido.Nombre, registros.Count, 1, elegido.HorasPorDia, elegido.Cantidad, [])
                : null;
        }

        var usados = unidades.Where(u => u.Id is not null).Select(u => u.Id!.Value).ToHashSet();
        var quitadas = registros.Where(r => !usados.Contains(r.Id)).Select(r => r.Nombre).ToList();
        if (registros.Count == 1 && ApodoDe(registros[0].Nombre, elegido.Nombre).Length == 0)
        {
            return new CambioAparato(TipoCambioAparato.AUnidades, elegido.Nombre, 1, unidades.Count, elegido.HorasPorDia, unidades.Count, quitadas);
        }

        return quitadas.Count > 0
            ? new CambioAparato(TipoCambioAparato.SoloQuitar, elegido.Nombre, registros.Count, unidades.Count, elegido.HorasPorDia, unidades.Count, quitadas)
            : null;
    }

    /// <summary>
    /// Cómo quedan los aparatos de un elegido: con las mismas horas, uno solo (el primero que ya existía, con su
    /// potencia); cada uno distinto, uno por unidad. Las unidades nuevas toman la potencia, los días y el ciclo del
    /// primer registro que ya existía (o los de la plantilla). <paramref name="registros"/> son los guardados de la
    /// plantilla; los que no quedan se devuelven en <c>Quitar</c>.
    /// </summary>
    public static (IReadOnlyList<(Aparato? Existente, AparatoEditable Editable)> Quedan, IReadOnlyList<Aparato> Quitar) Plan(
        AparatoElegido elegido, int lugarId, IReadOnlyList<Aparato> registros)
    {
        ArgumentNullException.ThrowIfNull(elegido);
        ArgumentNullException.ThrowIfNull(registros);
        var porId = registros.ToDictionary(r => r.Id);
        var primero = elegido.Id is int idPrimero && porId.TryGetValue(idPrimero, out var conId) ? conId : registros.FirstOrDefault();
        var quedan = new List<(Aparato?, AparatoEditable)>();
        if (elegido.Unidades is not { } unidades)
        {
            var editable = Editable(elegido, lugarId, primero);
            quedan.Add((primero, editable));
        }
        else
        {
            for (var i = 0; i < unidades.Count; i++)
            {
                var unidad = unidades[i];
                var existente = unidad.Id is int id ? porId.GetValueOrDefault(id) : null;
                var base_ = existente ?? primero;
                var nombre = NombreUnidad(elegido.Nombre, unidad.Apodo, i + 1);
                quedan.Add((existente, base_ is null
                    ? new AparatoEditable(
                        null, lugarId, nombre, elegido.Plantilla.Categoria, 1, elegido.Plantilla.Watts, unidad.HorasPorDia,
                        elegido.Plantilla.DiasPorSemana, elegido.Plantilla.CicloDeTrabajo)
                    : new AparatoEditable(
                        existente?.Id, lugarId, nombre, base_.Categoria, 1, base_.Watts, unidad.HorasPorDia, base_.DiasPorSemana,
                        base_.CicloDeTrabajo)));
            }
        }

        var siguen = quedan.Where(q => q.Item1 is not null).Select(q => q.Item1!.Id).ToHashSet();
        return (quedan, registros.Where(r => !siguen.Contains(r.Id)).ToList());
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
                existente.Id, lugarId, ApodoDe(existente.Nombre, elegido.Nombre).Length > 0 ? elegido.Nombre : existente.Nombre,
                existente.Categoria, elegido.Cantidad, existente.Watts, elegido.HorasPorDia, existente.DiasPorSemana,
                existente.CicloDeTrabajo);
    }

    // El nombre exacto (sin importar mayúsculas ni espacios) o una unidad: "{nombre} · …".
    private static bool EsDe(string nombreRegistro, string nombre)
        => string.Equals(Normalizar(nombreRegistro), nombre, StringComparison.CurrentCultureIgnoreCase)
            || Normalizar(nombreRegistro).StartsWith(nombre + SeparadorUnidad, StringComparison.CurrentCultureIgnoreCase);

    private static string Normalizar(string nombre) => string.Join(' ', (nombre ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
