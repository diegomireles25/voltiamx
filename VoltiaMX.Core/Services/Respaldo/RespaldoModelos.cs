namespace VoltiaMX.Core.Services;

/// <summary>
/// Dónde vive la base y dónde se guardan las copias automáticas. Llegan de la app como parámetros.
/// </summary>
public sealed record OpcionesRespaldo(string RutaBase, string CarpetaRespaldos, string VersionApp);

/// <summary>
/// Cuántos registros trae el respaldo. Solo conteos: nunca nombres, direcciones ni consumos.
/// </summary>
public sealed record ConteosRespaldo(int Lugares, int Lecturas, int Periodos, int Tarifas, int Aparatos);

/// <summary>
/// manifiesto.json del archivo .voltiabak.
/// </summary>
public sealed record ManifiestoRespaldo(
    int Formato,
    string VersionApp,
    string UltimaMigracion,
    DateTime CreadoUtc,
    ConteosRespaldo Conteos,
    string Sha256);

public enum MotivoRechazo
{
    /// <summary>
    /// No existe o no se pudo abrir el archivo.
    /// </summary>
    NoSePudoLeer,
    NoEsZip,
    FaltaManifiesto,
    FaltaBase,
    ManifiestoIlegible,
    FormatoNoCompatible,
    HashNoCoincide,
    BaseDanada,
    SinHistorialDeMigraciones,

    /// <summary>
    /// Tiene migraciones que esta versión de la app no conoce: no se restaura.
    /// </summary>
    ProcedeDeVersionMasNueva,
}

public sealed record ResultadoValidacion(MotivoRechazo? Motivo, ManifiestoRespaldo? Manifiesto)
{
    public bool EsValido => Motivo is null;
}

public enum ErrorRespaldo
{
    SinPermiso,
    EnUso,
    DiscoLleno,
    DestinoNoExiste,

    /// <summary>
    /// La copia no pasó PRAGMA integrity_check.
    /// </summary>
    IntegridadFallo,

    /// <summary>
    /// No se pudo crear la copia de seguridad previa: no se restauró nada.
    /// </summary>
    CopiaPreviaFallo,

    /// <summary>
    /// Falló al preparar la base restaurada (migraciones o revisión final).
    /// </summary>
    MigracionFallo,
    Desconocido,
}

public sealed record ResultadoRespaldo(string? Ruta, ErrorRespaldo? Error);

/// <summary>
/// Cómo terminó una restauración. <see cref="Revertido"/> indica que algo falló después del reemplazo
/// y se volvió a la copia previa.
/// </summary>
public sealed record ResultadoRestauracion(MotivoRechazo? Rechazo, ErrorRespaldo? Error, string? CopiaPrevia, bool Revertido)
{
    public bool Restaurado => Rechazo is null && Error is null;
}

public enum TipoCopia
{
    AntesDeMigrar,
    AntesDeRestaurar,
}

/// <summary>
/// Un archivo de la carpeta Respaldos. <see cref="Fecha"/> es la hora local del nombre.
/// </summary>
public sealed record CopiaAutomatica(string Ruta, TipoCopia Tipo, DateTime Fecha, long Bytes);

public enum EstadoCopiaPrevia
{
    NoHaceFalta,
    Creada,
    Fallo,
}

public sealed record ResultadoCopiaPrevia(EstadoCopiaPrevia Estado, string? Ruta, ErrorRespaldo? Error);
