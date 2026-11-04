using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;

namespace VoltiaMX.Core.Services;

public interface IRespaldoService
{
    string CarpetaRespaldos { get; }

    /// <summary>
    /// "VoltiaMX-respaldo-AAAA-MM-DD.voltiabak" con la fecha de hoy.
    /// </summary>
    string NombreSugerido();

    /// <summary>
    /// Respaldo completo en <paramref name="destino"/>. Nunca deja un archivo a medias en el destino.
    /// </summary>
    Task<ResultadoRespaldo> CrearAsync(string destino, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revisa el archivo sin tocar la base actual.
    /// </summary>
    Task<ResultadoValidacion> ValidarAsync(string origen, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida, crea la copia previa, reemplaza la base y la migra; si algo falla después del reemplazo,
    /// vuelve a la copia previa.
    /// </summary>
    Task<ResultadoRestauracion> RestaurarAsync(string origen, CancellationToken cancellationToken = default);

    /// <summary>
    /// Al arrancar, antes de migrar: copia la base si tiene datos y migraciones pendientes. Nunca lanza.
    /// </summary>
    Task<ResultadoCopiaPrevia> CrearCopiaAntesDeMigrarAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Copias automáticas de la carpeta Respaldos, de la más nueva a la más antigua.
    /// </summary>
    IReadOnlyList<CopiaAutomatica> ListarCopias();
}

/// <summary>
/// Respaldo en un archivo .voltiabak: un zip con "voltia.db" (copia hecha con la API de respaldo de SQLite)
/// y "manifiesto.json" (formato, versión, migración, fecha, conteos y SHA-256 de la base).
/// </summary>
public sealed partial class RespaldoService(OpcionesRespaldo opciones, TimeProvider? reloj = null) : IRespaldoService
{
    public const string Extension = ".voltiabak";
    public const int FormatoActual = 1;
    public const int CopiasConservadas = 5;

    private const string EntradaBase = "voltia.db";
    private const string EntradaManifiesto = "manifiesto.json";
    private const string TablaHistorial = "__EFMigrationsHistory";
    private const string FormatoFecha = "yyyyMMdd-HHmmss";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly Lazy<HashSet<string>> MigracionesConocidas = new(() =>
    {
        using var db = new VoltiaDbContext(new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite("Data Source=:memory:").Options);
        return db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
    });

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public string CarpetaRespaldos => opciones.CarpetaRespaldos;

    /// <summary>
    /// Solo para pruebas: se ejecuta justo después de reemplazar la base, para simular un fallo.
    /// </summary>
    internal Func<Task>? DespuesDeReemplazar { get; set; }

    public string NombreSugerido() => $"VoltiaMX-respaldo-{_reloj.GetLocalNow():yyyy-MM-dd}{Extension}";

    public Task<ResultadoRespaldo> CrearAsync(string destino, CancellationToken cancellationToken = default)
        => Task.Run(() =>
        {
            try
            {
                Crear(destino);
                return new ResultadoRespaldo(destino, null);
            }
            catch (Exception ex)
            {
                return new ResultadoRespaldo(null, Clasificar(ex));
            }
        }, cancellationToken);

    public async Task<ResultadoValidacion> ValidarAsync(string origen, CancellationToken cancellationToken = default)
    {
        using var extraida = await Task.Run(() => Validar(origen), cancellationToken);
        return extraida.Resultado;
    }

    public async Task<ResultadoRestauracion> RestaurarAsync(string origen, CancellationToken cancellationToken = default)
    {
        // (1) Validar; si falla, no se toca nada.
        using var extraida = await Task.Run(() => Validar(origen), cancellationToken);
        if (extraida.Resultado.Motivo is { } motivo)
        {
            return new ResultadoRestauracion(motivo, null, null, false);
        }

        // (2) Copia previa de la base actual; sin ella no se continúa.
        string? copiaPrevia = null;
        if (File.Exists(opciones.RutaBase))
        {
            var copia = await Task.Run(() => CrearCopiaAutomatica(TipoCopia.AntesDeRestaurar), cancellationToken);
            if (copia.Error is not null)
            {
                return new ResultadoRestauracion(null, ErrorRespaldo.CopiaPreviaFallo, null, false);
            }

            copiaPrevia = copia.Ruta;
        }

        // (3) Reemplazo atómico; si falla aquí, la base actual sigue intacta.
        try
        {
            await Task.Run(() => Reemplazar(extraida.RutaBase!), cancellationToken);
        }
        catch (Exception ex)
        {
            return new ResultadoRestauracion(null, Clasificar(ex), copiaPrevia, false);
        }

        // (4) Migraciones pendientes y revisión final; (5) si algo falla, vuelve la copia previa.
        try
        {
            if (DespuesDeReemplazar is { } fallo)
            {
                await fallo();
            }

            await using (var db = Contexto(opciones.RutaBase))
            {
                await db.Database.MigrateAsync(cancellationToken);
            }

            if (!PasaIntegridad(opciones.RutaBase))
            {
                throw new ErrorDeRespaldoException(ErrorRespaldo.IntegridadFallo);
            }

            return new ResultadoRestauracion(null, null, copiaPrevia, false);
        }
        catch (Exception ex)
        {
            var error = ex is ErrorDeRespaldoException propio ? propio.Error : ErrorRespaldo.MigracionFallo;
            var revertido = copiaPrevia is not null && await Task.Run(() => Revertir(copiaPrevia));
            return new ResultadoRestauracion(null, error, copiaPrevia, revertido);
        }
    }

    public async Task<ResultadoCopiaPrevia> CrearCopiaAntesDeMigrarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(opciones.RutaBase) || !TieneDatos(opciones.RutaBase))
            {
                return new ResultadoCopiaPrevia(EstadoCopiaPrevia.NoHaceFalta, null, null);
            }

            await using (var db = Contexto(opciones.RutaBase))
            {
                if (!(await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                {
                    return new ResultadoCopiaPrevia(EstadoCopiaPrevia.NoHaceFalta, null, null);
                }
            }

            var copia = await Task.Run(() => CrearCopiaAutomatica(TipoCopia.AntesDeMigrar), cancellationToken);
            return copia.Error is { } error
                ? new ResultadoCopiaPrevia(EstadoCopiaPrevia.Fallo, null, error)
                : new ResultadoCopiaPrevia(EstadoCopiaPrevia.Creada, copia.Ruta, null);
        }
        catch (Exception ex)
        {
            return new ResultadoCopiaPrevia(EstadoCopiaPrevia.Fallo, null, Clasificar(ex));
        }
    }

    public IReadOnlyList<CopiaAutomatica> ListarCopias()
    {
        if (!Directory.Exists(opciones.CarpetaRespaldos))
        {
            return [];
        }

        var copias = new List<(CopiaAutomatica Copia, int Sufijo)>();
        foreach (var archivo in new DirectoryInfo(opciones.CarpetaRespaldos).EnumerateFiles("antes-de-*" + Extension))
        {
            var partes = NombreDeCopia().Match(archivo.Name);
            if (!partes.Success
                || !DateTime.TryParseExact(partes.Groups["fecha"].Value, FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            {
                continue;
            }

            var tipo = partes.Groups["tipo"].Value == "migrar" ? TipoCopia.AntesDeMigrar : TipoCopia.AntesDeRestaurar;
            var sufijo = partes.Groups["n"].Success ? int.Parse(partes.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
            copias.Add((new CopiaAutomatica(archivo.FullName, tipo, fecha, archivo.Length), sufijo));
        }

        return copias
            .OrderByDescending(c => c.Copia.Fecha)
            .ThenByDescending(c => c.Sufijo)
            .Select(c => c.Copia)
            .ToList();
    }

    [GeneratedRegex(@"^antes-de-(?<tipo>migrar|restaurar)-(?<fecha>\d{8}-\d{6})(-(?<n>\d+))?\.voltiabak$", RegexOptions.CultureInvariant)]
    private static partial Regex NombreDeCopia();

    // ---- Crear ----

    private void Crear(string destino)
    {
        var temporal = Directory.CreateTempSubdirectory("voltiamx-respaldo-").FullName;
        try
        {
            var copia = Path.Combine(temporal, EntradaBase);
            CopiarBase(opciones.RutaBase, copia);
            if (!PasaIntegridad(copia))
            {
                throw new ErrorDeRespaldoException(ErrorRespaldo.IntegridadFallo);
            }

            var manifiesto = new ManifiestoRespaldo(
                FormatoActual,
                opciones.VersionApp,
                Migraciones(copia).LastOrDefault() ?? string.Empty,
                _reloj.GetUtcNow().UtcDateTime,
                Contar(copia),
                Hash(copia));

            // Se arma junto al destino y se mueve al final: nunca queda un respaldo a medias con su nombre.
            var parcial = $"{destino}.tmp-{Guid.NewGuid():N}";
            try
            {
                using (var zip = ZipFile.Open(parcial, ZipArchiveMode.Create))
                {
                    zip.CreateEntryFromFile(copia, EntradaBase, CompressionLevel.Optimal);
                    using var flujo = zip.CreateEntry(EntradaManifiesto).Open();
                    JsonSerializer.Serialize(flujo, manifiesto, Json);
                }

                File.Move(parcial, destino, overwrite: true);
            }
            finally
            {
                BorrarSinFallar(parcial);
            }
        }
        finally
        {
            BorrarCarpetaSinFallar(temporal);
        }
    }

    private ResultadoRespaldo CrearCopiaAutomatica(TipoCopia tipo)
    {
        try
        {
            Directory.CreateDirectory(opciones.CarpetaRespaldos);
            var prefijo = $"antes-de-{(tipo == TipoCopia.AntesDeMigrar ? "migrar" : "restaurar")}-{_reloj.GetLocalNow().DateTime.ToString(FormatoFecha, CultureInfo.InvariantCulture)}";
            var ruta = Path.Combine(opciones.CarpetaRespaldos, prefijo + Extension);
            for (var n = 2; File.Exists(ruta); n++)
            {
                ruta = Path.Combine(opciones.CarpetaRespaldos, $"{prefijo}-{n}{Extension}");
            }

            Crear(ruta);
            Rotar();
            return new ResultadoRespaldo(ruta, null);
        }
        catch (Exception ex)
        {
            return new ResultadoRespaldo(null, Clasificar(ex));
        }
    }

    private void Rotar()
    {
        foreach (var vieja in ListarCopias().Skip(CopiasConservadas))
        {
            BorrarSinFallar(vieja.Ruta);
        }
    }

    // ---- Validar ----

    private ArchivoExtraido Validar(string origen)
    {
        var temporal = Directory.CreateTempSubdirectory("voltiamx-validar-").FullName;
        var extraida = new ArchivoExtraido(temporal);
        try
        {
            extraida.Resultado = Revisar(origen, Path.Combine(temporal, EntradaBase));
            if (extraida.Resultado.EsValido)
            {
                extraida.RutaBase = Path.Combine(temporal, EntradaBase);
            }
        }
        catch (Exception)
        {
            extraida.Resultado = new ResultadoValidacion(MotivoRechazo.NoSePudoLeer, null);
        }

        return extraida;
    }

    private static ResultadoValidacion Revisar(string origen, string baseExtraida)
    {
        static ResultadoValidacion Rechazo(MotivoRechazo motivo) => new(motivo, null);

        if (!File.Exists(origen))
        {
            return Rechazo(MotivoRechazo.NoSePudoLeer);
        }

        ManifiestoRespaldo? manifiesto;
        try
        {
            using var zip = ZipFile.OpenRead(origen);
            var entradaManifiesto = zip.GetEntry(EntradaManifiesto);
            var entradaBase = zip.GetEntry(EntradaBase);
            if (entradaManifiesto is null)
            {
                return Rechazo(MotivoRechazo.FaltaManifiesto);
            }

            if (entradaBase is null)
            {
                return Rechazo(MotivoRechazo.FaltaBase);
            }

            try
            {
                using var flujo = entradaManifiesto.Open();
                manifiesto = JsonSerializer.Deserialize<ManifiestoRespaldo>(flujo, Json);
            }
            catch (JsonException)
            {
                return Rechazo(MotivoRechazo.ManifiestoIlegible);
            }

            if (manifiesto is null || manifiesto.Conteos is null || string.IsNullOrEmpty(manifiesto.Sha256))
            {
                return Rechazo(MotivoRechazo.ManifiestoIlegible);
            }

            if (manifiesto.Formato != FormatoActual)
            {
                return Rechazo(MotivoRechazo.FormatoNoCompatible);
            }

            entradaBase.ExtractToFile(baseExtraida, overwrite: true);
        }
        catch (InvalidDataException)
        {
            return Rechazo(MotivoRechazo.NoEsZip);
        }

        if (!string.Equals(Hash(baseExtraida), manifiesto.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            return Rechazo(MotivoRechazo.HashNoCoincide);
        }

        if (!PasaIntegridad(baseExtraida))
        {
            return Rechazo(MotivoRechazo.BaseDanada);
        }

        if (!TablaExiste(baseExtraida, TablaHistorial))
        {
            return Rechazo(MotivoRechazo.SinHistorialDeMigraciones);
        }

        // Cualquier migración que esta versión no conoce: el respaldo viene de una versión más nueva.
        if (Migraciones(baseExtraida).Any(m => !MigracionesConocidas.Value.Contains(m)))
        {
            return Rechazo(MotivoRechazo.ProcedeDeVersionMasNueva);
        }

        return new ResultadoValidacion(null, manifiesto);
    }

    // ---- Restaurar ----

    private void Reemplazar(string baseNueva)
    {
        SqliteConnection.ClearAllPools();
        var junto = opciones.RutaBase + ".restaurando";
        try
        {
            // Junto a la base (mismo disco) para que el reemplazo sea atómico.
            File.Copy(baseNueva, junto, overwrite: true);
            if (File.Exists(opciones.RutaBase))
            {
                File.Replace(junto, opciones.RutaBase, destinationBackupFileName: null);
            }
            else
            {
                File.Move(junto, opciones.RutaBase);
            }

            // Los -wal y -shm de la base anterior no corresponden a la nueva.
            BorrarSinFallar(opciones.RutaBase + "-wal");
            BorrarSinFallar(opciones.RutaBase + "-shm");
        }
        finally
        {
            BorrarSinFallar(junto);
        }
    }

    private bool Revertir(string copiaPrevia)
    {
        try
        {
            using var extraida = Validar(copiaPrevia);
            if (extraida.RutaBase is null)
            {
                return false;
            }

            Reemplazar(extraida.RutaBase);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // ---- SQLite ----

    private static string Cadena(string ruta, bool soloLectura = false) => new SqliteConnectionStringBuilder
    {
        DataSource = ruta,
        Mode = soloLectura ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
        Pooling = false,
    }.ToString();

    private static VoltiaDbContext Contexto(string ruta)
        => new(new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(Cadena(ruta)).Options);

    /// <summary>
    /// Copia consistente con la API de respaldo de SQLite (sirve con la base abierta o en modo WAL).
    /// </summary>
    private static void CopiarBase(string origen, string destino)
    {
        if (!File.Exists(origen))
        {
            throw new FileNotFoundException("No existe la base de datos.");
        }

        using var fuente = new SqliteConnection(Cadena(origen, soloLectura: true));
        using var copia = new SqliteConnection(Cadena(destino));
        fuente.Open();
        copia.Open();
        fuente.BackupDatabase(copia);
    }

    private static bool PasaIntegridad(string ruta)
    {
        try
        {
            using var conexion = new SqliteConnection(Cadena(ruta, soloLectura: true));
            conexion.Open();
            using var comando = conexion.CreateCommand();
            comando.CommandText = "PRAGMA integrity_check";
            return comando.ExecuteScalar() as string == "ok";
        }
        catch (SqliteException)
        {
            return false;
        }
    }

    private static bool TablaExiste(SqliteConnection conexion, string tabla)
    {
        using var comando = conexion.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $tabla";
        comando.Parameters.AddWithValue("$tabla", tabla);
        return (long)comando.ExecuteScalar()! > 0;
    }

    private static bool TablaExiste(string ruta, string tabla)
    {
        using var conexion = new SqliteConnection(Cadena(ruta, soloLectura: true));
        conexion.Open();
        return TablaExiste(conexion, tabla);
    }

    private static List<string> Migraciones(string ruta)
    {
        using var conexion = new SqliteConnection(Cadena(ruta, soloLectura: true));
        conexion.Open();
        var migraciones = new List<string>();
        if (!TablaExiste(conexion, TablaHistorial))
        {
            return migraciones;
        }

        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT MigrationId FROM {TablaHistorial} ORDER BY MigrationId";
        using var lector = comando.ExecuteReader();
        while (lector.Read())
        {
            migraciones.Add(lector.GetString(0));
        }

        return migraciones;
    }

    /// <summary>
    /// Conteos por SQL directo: en un respaldo antiguo puede faltar una tabla (cuenta 0).
    /// </summary>
    private static ConteosRespaldo Contar(string ruta)
    {
        using var conexion = new SqliteConnection(Cadena(ruta, soloLectura: true));
        conexion.Open();
        return new ConteosRespaldo(
            Contar(conexion, "Lugares"),
            Contar(conexion, "Lecturas"),
            Contar(conexion, "Periodos"),
            Contar(conexion, "Tarifas"),
            Contar(conexion, "Aparatos"));
    }

    private static int Contar(SqliteConnection conexion, string tabla)
    {
        if (!TablaExiste(conexion, tabla))
        {
            return 0;
        }

        using var comando = conexion.CreateCommand();
        comando.CommandText = $"SELECT COUNT(*) FROM \"{tabla}\"";
        return (int)(long)comando.ExecuteScalar()!;
    }

    /// <summary>
    /// Hay algo que proteger: lugares, tarifas o el perfil.
    /// </summary>
    private static bool TieneDatos(string ruta)
    {
        using var conexion = new SqliteConnection(Cadena(ruta, soloLectura: true));
        conexion.Open();
        return Contar(conexion, "Lugares") + Contar(conexion, "Tarifas") + Contar(conexion, "Perfiles") > 0;
    }

    private static string Hash(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        return Convert.ToHexStringLower(SHA256.HashData(flujo));
    }

    // ---- Utilidades ----

    /// <summary>
    /// El error en términos de la persona, por tipo de excepción o código de Windows.
    /// </summary>
    private static ErrorRespaldo Clasificar(Exception ex) => ex switch
    {
        ErrorDeRespaldoException propio => propio.Error,
        UnauthorizedAccessException => ErrorRespaldo.SinPermiso,
        DirectoryNotFoundException or FileNotFoundException => ErrorRespaldo.DestinoNoExiste,
        _ => (uint)ex.HResult switch
        {
            0x80070005 => ErrorRespaldo.SinPermiso,
            0x80070020 or 0x80070021 or 0x80070497 => ErrorRespaldo.EnUso,
            0x80070070 or 0x80070027 => ErrorRespaldo.DiscoLleno,
            0x80070002 or 0x80070003 or 0x8007007B => ErrorRespaldo.DestinoNoExiste,
            _ => ErrorRespaldo.Desconocido,
        },
    };

    private static void BorrarSinFallar(string ruta)
    {
        try
        {
            File.Delete(ruta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Un temporal que no se pudo borrar no detiene el respaldo.
        }
    }

    private static void BorrarCarpetaSinFallar(string ruta)
    {
        try
        {
            Directory.Delete(ruta, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Queda en %TEMP%.
        }
    }

    /// <summary>
    /// Base extraída a una carpeta temporal que se borra al terminar.
    /// </summary>
    private sealed class ArchivoExtraido(string carpeta) : IDisposable
    {
        public ResultadoValidacion Resultado { get; set; } = new(MotivoRechazo.NoSePudoLeer, null);

        public string? RutaBase { get; set; }

        public void Dispose() => BorrarCarpetaSinFallar(carpeta);
    }

    private sealed class ErrorDeRespaldoException(ErrorRespaldo error) : Exception(error.ToString())
    {
        public ErrorRespaldo Error { get; } = error;
    }
}
