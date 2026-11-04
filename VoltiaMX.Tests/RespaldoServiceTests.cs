using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Respaldo con archivos reales en una carpeta temporal por prueba (se borra al terminar).
/// Todas las conexiones de las pruebas van sin pool para soltar los archivos.
/// </summary>
public sealed class RespaldoServiceTests : IDisposable
{
    private static readonly string[] Tablas =
        ["Lugares", "Tarifas", "TarifaRangos", "Periodos", "Lecturas", "Aparatos", "Metas", "AvisosEnviados"];

    private const string PoblarExtra = """
        INSERT INTO Aparatos (LugarId, Nombre, Categoria, Cantidad, Watts, HorasPorDia, DiasPorSemana, CicloDeTrabajo) VALUES
            (1, 'Refrigerador', 'Refrigeracion', 1, '250.0', '24.0', 7, '0.5'),
            (1, '=1+1', 'Otro', 1, '10.0', '1.0', 7, '1.0');
        INSERT INTO Metas (LugarId, PorcentajeObjetivo, FechaInicio, Activa) VALUES (1, 10, '2026-05-01', 1);
        INSERT INTO AvisosEnviados (LugarId, Tipo, Clave, FechaEnvio) VALUES (1, 'Corte', '2026-07-01', '2026-06-28 09:00:00');
        """;

    // Otra base con datos distintos: las tarifas de ejemplo y un solo lugar con una lectura.
    private const string PoblarOtra = """
        INSERT INTO Lugares (Id, Nombre, Tipo, TarifaId, DiaCorte, MesesPorPeriodo, EsDemo, TarifaPorConfirmar) VALUES
            (1, 'Otro lugar', 'Casa', 1, 5, 2, 0, 0);
        INSERT INTO Lecturas (LugarId, Fecha, ValorKwh, Origen, EsEstimada) VALUES (1, '2026-10-01', '123.0', 'Manual', 0);
        """;

    // Esquema de la migración Perfil: Lugares sin MesesPorPeriodo y Lecturas sin EsEstimada.
    private const string PoblarEsquemaPerfil = """
        INSERT INTO Tarifas (Id, Nombre, Tipo, LimiteDacKwhMensual, CargoFijoMensual, Vigencia) VALUES
            (1, '1', 'Domestica', '250.0', '0.0', '2026-01-01');
        INSERT INTO TarifaRangos (TarifaId, Nombre, KwhDesde, KwhHasta, PrecioPorKwh, Orden) VALUES
            (1, 'Básico', '0.0', '75.0', '1.0', 1), (1, 'Excedente', '75.0', NULL, '3.5', 2);
        INSERT INTO Lugares (Id, Nombre, Tipo, TarifaId, DiaCorte, EsDemo, TarifaPorConfirmar) VALUES
            (1, 'Casa vieja', 'Casa', 1, 1, 0, 0);
        INSERT INTO Lecturas (LugarId, Fecha, ValorKwh, Origen) VALUES
            (1, '2026-05-01', '1000.0', 'Manual'), (1, '2026-06-01', '1150.0', 'Manual');
        INSERT INTO Periodos (LugarId, Inicio, Fin, KwhTotales, Monto) VALUES
            (1, '2026-03-02', '2026-05-01', '300.0', '400.0');
        """;

    private readonly string _carpeta = Directory.CreateTempSubdirectory("voltiamx-pruebas-").FullName;
    private readonly RelojDePrueba _reloj = new(new DateTimeOffset(2026, 11, 3, 10, 30, 0, TimeSpan.Zero));

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(_carpeta, recursive: true);
        }
        catch (IOException)
        {
            // Algún archivo quedó tomado: se queda en %TEMP%, no en el repositorio.
        }
    }

    private string Ruta(string nombre) => Path.Combine(_carpeta, nombre);

    private string CarpetaRespaldos => Ruta("Respaldos");

    private RespaldoService Servicio(string rutaBase, string? carpetaRespaldos = null)
        => new(new OpcionesRespaldo(rutaBase, carpetaRespaldos ?? CarpetaRespaldos, "0.8.0"), _reloj);

    private static string Cadena(string ruta) => new SqliteConnectionStringBuilder { DataSource = ruta, Pooling = false }.ToString();

    private static VoltiaDbContext Contexto(string ruta)
        => new(new DbContextOptionsBuilder<VoltiaDbContext>().UseSqlite(Cadena(ruta)).Options);

    private static async Task EjecutarAsync(string ruta, string sql)
    {
        await using var conexion = new SqliteConnection(Cadena(ruta));
        await conexion.OpenAsync();
        await using var comando = conexion.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Base con todas las migraciones y los datos de MigracionNombreTarifa40Tests más aparatos, meta y aviso.
    /// </summary>
    private async Task<string> BaseConDatosAsync(string nombre = "origen.db")
    {
        var ruta = Ruta(nombre);
        await using (var db = Contexto(ruta))
        {
            await db.Database.MigrateAsync();
        }

        await EjecutarAsync(ruta, MigracionNombreTarifa40Tests.Poblar);
        await EjecutarAsync(ruta, PoblarExtra);
        return ruta;
    }

    private async Task<string> OtraBaseAsync(string nombre = "voltia.db")
    {
        var ruta = Ruta(nombre);
        await using (var db = Contexto(ruta))
        {
            await new DbInicializador(db).InicializarAsync();
        }

        await EjecutarAsync(ruta, PoblarOtra);
        return ruta;
    }

    private static async Task MigrarHastaAsync(string ruta, string sufijo)
    {
        await using var db = Contexto(ruta);
        var migracion = db.Database.GetMigrations().Single(m => m.EndsWith(sufijo, StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(migracion);
    }

    private static async Task<Dictionary<string, long>> ConteosAsync(string ruta)
    {
        await using var conexion = new SqliteConnection(Cadena(ruta));
        await conexion.OpenAsync();
        var conteos = new Dictionary<string, long>();
        foreach (var tabla in Tablas)
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = $"SELECT COUNT(*) FROM {tabla}";
            conteos[tabla] = (long)(await comando.ExecuteScalarAsync())!;
        }

        return conteos;
    }

    private static string Hash(string ruta) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ruta)));

    private async Task<string> RespaldoValidoAsync(string nombre = "respaldo.voltiabak")
    {
        var origen = await BaseConDatosAsync();
        var destino = Ruta(nombre);
        var resultado = await Servicio(origen).CrearAsync(destino);
        Assert.Null(resultado.Error);
        return destino;
    }

    /// <summary>
    /// Reescribe el respaldo: cambia la base con <paramref name="cambiarBase"/> y, si se pide, recalcula el hash
    /// del manifiesto para que la validación llegue a revisar la base.
    /// </summary>
    private async Task<string> AlterarAsync(string respaldo, Func<string, Task> cambiarBase, bool recalcularHash)
    {
        var db = Ruta("alterada.db");
        using (var zip = ZipFile.OpenRead(respaldo))
        {
            zip.GetEntry("voltia.db")!.ExtractToFile(db, overwrite: true);
        }

        await cambiarBase(db);
        SqliteConnection.ClearAllPools();

        var alterado = Ruta("alterado.voltiabak");
        File.Copy(respaldo, alterado, overwrite: true);
        using (var zip = ZipFile.Open(alterado, ZipArchiveMode.Update))
        {
            JsonNode manifiesto;
            using (var lector = zip.GetEntry("manifiesto.json")!.Open())
            {
                manifiesto = JsonNode.Parse(lector)!;
            }

            if (recalcularHash)
            {
                manifiesto["sha256"] = Hash(db);
            }

            zip.GetEntry("voltia.db")!.Delete();
            zip.GetEntry("manifiesto.json")!.Delete();
            zip.CreateEntryFromFile(db, "voltia.db");
            using var escritor = new StreamWriter(zip.CreateEntry("manifiesto.json").Open());
            escritor.Write(manifiesto.ToJsonString());
        }

        return alterado;
    }

    /// <summary>
    /// El archivo se rechaza con su motivo al validarlo y al restaurarlo, y la base actual queda intacta.
    /// </summary>
    private async Task AsegurarRechazoAsync(string archivo, MotivoRechazo esperado)
    {
        var actual = await OtraBaseAsync();
        var hashAntes = Hash(actual);
        var servicio = Servicio(actual);

        var validacion = await servicio.ValidarAsync(archivo);
        var restauracion = await servicio.RestaurarAsync(archivo);

        Assert.False(validacion.EsValido);
        Assert.Equal(esperado, validacion.Motivo);
        Assert.Equal(esperado, restauracion.Rechazo);
        Assert.False(restauracion.Restaurado);
        Assert.Equal(hashAntes, Hash(actual));
        Assert.Empty(servicio.ListarCopias());
    }

    private static void CrearZip(string ruta, params (string Nombre, byte[] Contenido)[] entradas)
    {
        using var zip = ZipFile.Open(ruta, ZipArchiveMode.Create);
        foreach (var (nombre, contenido) in entradas)
        {
            using var flujo = zip.CreateEntry(nombre).Open();
            flujo.Write(contenido);
        }
    }

    [Fact]
    public async Task Crear_BaseConDatos_ZipConLaBaseYElManifiestoConSusConteos()
    {
        var origen = await BaseConDatosAsync();
        var destino = Ruta("respaldo.voltiabak");

        var resultado = await Servicio(origen).CrearAsync(destino);

        Assert.Null(resultado.Error);
        Assert.Equal(destino, resultado.Ruta);
        using var zip = ZipFile.OpenRead(destino);
        Assert.Equal(["manifiesto.json", "voltia.db"], zip.Entries.Select(e => e.FullName).Order());

        var validacion = await Servicio(origen).ValidarAsync(destino);
        Assert.True(validacion.EsValido);
        var manifiesto = validacion.Manifiesto!;
        var conteos = await ConteosAsync(origen);
        Assert.Equal(1, manifiesto.Formato);
        Assert.Equal("0.8.0", manifiesto.VersionApp);
        await using (var db = Contexto(origen))
        {
            Assert.Equal(db.Database.GetMigrations().Last(), manifiesto.UltimaMigracion);
        }

        Assert.Equal(new DateTime(2026, 11, 3, 10, 30, 0, DateTimeKind.Utc), manifiesto.CreadoUtc);
        Assert.Equal(
            (conteos["Lugares"], conteos["Lecturas"], conteos["Periodos"], conteos["Tarifas"], conteos["Aparatos"]),
            ((long)manifiesto.Conteos.Lugares, (long)manifiesto.Conteos.Lecturas, (long)manifiesto.Conteos.Periodos,
                (long)manifiesto.Conteos.Tarifas, (long)manifiesto.Conteos.Aparatos));
    }

    [Fact]
    public async Task Crear_ElManifiestoSoloLlevaConteos_SinNombresNiConsumos()
    {
        var destino = await RespaldoValidoAsync();

        using var zip = ZipFile.OpenRead(destino);
        using var lector = new StreamReader(zip.GetEntry("manifiesto.json")!.Open());
        var texto = await lector.ReadToEndAsync();

        Assert.DoesNotContain("Mi casa", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Refrigerador", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("1600", texto, StringComparison.Ordinal);
        Assert.Equal(
            ["conteos", "creadoUtc", "formato", "sha256", "ultimaMigracion", "versionApp"],
            JsonNode.Parse(texto)!.AsObject().Select(p => p.Key).Order());
    }

    [Fact]
    public async Task Crear_DestinoQueNoExiste_DevuelveElErrorSinDejarArchivos()
    {
        var origen = await BaseConDatosAsync();
        var destino = Path.Combine(_carpeta, "no-existe", "respaldo.voltiabak");

        var resultado = await Servicio(origen).CrearAsync(destino);

        Assert.Equal(ErrorRespaldo.DestinoNoExiste, resultado.Error);
        Assert.Null(resultado.Ruta);
        Assert.False(Directory.Exists(Path.GetDirectoryName(destino)));
        Assert.Empty(Directory.GetFiles(_carpeta, "*.tmp-*"));
    }

    [Fact]
    public async Task Crear_ReemplazaUnArchivoExistenteSinDejarTemporales()
    {
        var origen = await BaseConDatosAsync();
        var destino = Ruta("respaldo.voltiabak");
        await File.WriteAllTextAsync(destino, "viejo");

        var resultado = await Servicio(origen).CrearAsync(destino);

        Assert.Null(resultado.Error);
        Assert.True((await Servicio(origen).ValidarAsync(destino)).EsValido);
        // La base está en modo WAL (EF lo activa al crearla): sus -wal y -shm no cuentan como temporales.
        Assert.Equal(
            ["origen.db", "respaldo.voltiabak"],
            Directory.GetFiles(_carpeta).Select(Path.GetFileName).Where(n => !n!.EndsWith("-wal") && !n.EndsWith("-shm")).Order());
    }

    [Fact]
    public void NombreSugerido_UsaLaFechaDeHoy()
        => Assert.Equal("VoltiaMX-respaldo-2026-11-03.voltiabak", Servicio(Ruta("voltia.db")).NombreSugerido());

    [Fact]
    public async Task Restaurar_SobreOtraBase_DejaExactamenteLosDatosDelRespaldo()
    {
        var respaldo = await RespaldoValidoAsync();
        var conteosRespaldo = await ConteosAsync(Ruta("origen.db"));
        var actual = await OtraBaseAsync();
        Assert.NotEqual(conteosRespaldo, await ConteosAsync(actual));

        var resultado = await Servicio(actual).RestaurarAsync(respaldo);

        Assert.True(resultado.Restaurado);
        Assert.False(resultado.Revertido);
        Assert.Equal(conteosRespaldo, await ConteosAsync(actual));
        await using var db = Contexto(actual);
        Assert.Equal(["Casa de ejemplo", "Local", "Mi casa"], (await db.Lugares.Select(l => l.Nombre).ToListAsync()).Order());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.False(File.Exists(actual + "-wal"));
        Assert.False(File.Exists(actual + ".restaurando"));
    }

    [Fact]
    public async Task Restaurar_CreaLaCopiaPreviaConLosDatosAnterioresYSePuedeAbrir()
    {
        var respaldo = await RespaldoValidoAsync();
        var actual = await OtraBaseAsync();
        var conteosAntes = await ConteosAsync(actual);
        var servicio = Servicio(actual);

        var resultado = await servicio.RestaurarAsync(respaldo);

        var copia = Assert.Single(servicio.ListarCopias());
        Assert.Equal(TipoCopia.AntesDeRestaurar, copia.Tipo);
        Assert.Equal(copia.Ruta, resultado.CopiaPrevia);
        Assert.Equal("antes-de-restaurar-20261103-103000.voltiabak", Path.GetFileName(copia.Ruta));
        Assert.Equal(CarpetaRespaldos, Path.GetDirectoryName(copia.Ruta));
        Assert.True(copia.Bytes > 0);

        // La copia previa es un respaldo válido y al restaurarla vuelven los datos anteriores.
        Assert.True((await servicio.ValidarAsync(copia.Ruta)).EsValido);
        Assert.True((await Servicio(actual, Ruta("Otras")).RestaurarAsync(copia.Ruta)).Restaurado);
        Assert.Equal(conteosAntes, await ConteosAsync(actual));
    }

    [Fact]
    public async Task Restaurar_FalloDespuesDelReemplazo_RecuperaLaCopiaPrevia()
    {
        var respaldo = await RespaldoValidoAsync();
        var actual = await OtraBaseAsync();
        var conteosAntes = await ConteosAsync(actual);
        var servicio = Servicio(actual);
        servicio.DespuesDeReemplazar = () => throw new InvalidOperationException("fallo simulado");

        var resultado = await servicio.RestaurarAsync(respaldo);

        Assert.False(resultado.Restaurado);
        Assert.True(resultado.Revertido);
        Assert.Equal(ErrorRespaldo.MigracionFallo, resultado.Error);
        Assert.NotNull(resultado.CopiaPrevia);
        Assert.Equal(conteosAntes, await ConteosAsync(actual));
    }

    [Fact]
    public async Task Restaurar_RespaldoDeVersionAntigua_SeMigraYConservaSusFilas()
    {
        var vieja = Ruta("vieja.db");
        await MigrarHastaAsync(vieja, "_Perfil");
        await EjecutarAsync(vieja, PoblarEsquemaPerfil);
        var respaldo = Ruta("viejo.voltiabak");
        Assert.Null((await Servicio(vieja).CrearAsync(respaldo)).Error);
        var actual = await OtraBaseAsync();
        var servicio = Servicio(actual);

        var validacion = await servicio.ValidarAsync(respaldo);
        var resultado = await servicio.RestaurarAsync(respaldo);

        Assert.True(validacion.EsValido);
        Assert.EndsWith("_Perfil", validacion.Manifiesto!.UltimaMigracion, StringComparison.Ordinal);
        Assert.Equal(0, validacion.Manifiesto.Conteos.Aparatos);
        Assert.True(resultado.Restaurado);
        await using var db = Contexto(actual);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal("Casa vieja", (await db.Lugares.SingleAsync()).Nombre);
        Assert.Equal(2, await db.Lecturas.CountAsync());
        Assert.Equal(1, await db.Periodos.CountAsync());
        Assert.Equal(2, await db.TarifaRangos.CountAsync());
    }

    [Fact]
    public async Task Rechazo_ArchivoQueNoEsZip()
    {
        var archivo = Ruta("texto.voltiabak");
        await File.WriteAllTextAsync(archivo, "esto no es un respaldo");
        await AsegurarRechazoAsync(archivo, MotivoRechazo.NoEsZip);
    }

    [Fact]
    public async Task Rechazo_ArchivoQueNoExiste()
        => await AsegurarRechazoAsync(Ruta("no-existe.voltiabak"), MotivoRechazo.NoSePudoLeer);

    [Fact]
    public async Task Rechazo_ZipSinManifiesto()
    {
        var archivo = Ruta("sin-manifiesto.voltiabak");
        CrearZip(archivo, ("voltia.db", [1, 2, 3]));
        await AsegurarRechazoAsync(archivo, MotivoRechazo.FaltaManifiesto);
    }

    [Fact]
    public async Task Rechazo_ZipSinBase()
    {
        var archivo = Ruta("sin-base.voltiabak");
        CrearZip(archivo, ("manifiesto.json", Encoding.UTF8.GetBytes("{}")));
        await AsegurarRechazoAsync(archivo, MotivoRechazo.FaltaBase);
    }

    [Fact]
    public async Task Rechazo_ManifiestoCorrupto()
    {
        var archivo = Ruta("manifiesto-corrupto.voltiabak");
        CrearZip(archivo, ("voltia.db", [1, 2, 3]), ("manifiesto.json", Encoding.UTF8.GetBytes("{ \"formato\": 1, ")));
        await AsegurarRechazoAsync(archivo, MotivoRechazo.ManifiestoIlegible);
    }

    [Fact]
    public async Task Rechazo_FormatoDesconocido()
    {
        var respaldo = await RespaldoValidoAsync();
        var archivo = Ruta("formato-2.voltiabak");
        using (var zip = ZipFile.OpenRead(respaldo))
        using (var lector = zip.GetEntry("manifiesto.json")!.Open())
        {
            var manifiesto = JsonNode.Parse(lector)!;
            manifiesto["formato"] = 2;
            await using var db = new MemoryStream();
            await using (var flujo = zip.GetEntry("voltia.db")!.Open())
            {
                await flujo.CopyToAsync(db);
            }

            CrearZip(archivo, ("voltia.db", db.ToArray()), ("manifiesto.json", Encoding.UTF8.GetBytes(manifiesto.ToJsonString())));
        }

        await AsegurarRechazoAsync(archivo, MotivoRechazo.FormatoNoCompatible);
    }

    [Fact]
    public async Task Rechazo_UnByteModificado_ElHashNoCoincide()
    {
        var respaldo = await RespaldoValidoAsync();
        var alterado = await AlterarAsync(respaldo, async db =>
        {
            var bytes = await File.ReadAllBytesAsync(db);
            bytes[bytes.Length / 2] ^= 0xFF;
            await File.WriteAllBytesAsync(db, bytes);
        }, recalcularHash: false);

        await AsegurarRechazoAsync(alterado, MotivoRechazo.HashNoCoincide);
    }

    [Fact]
    public async Task Rechazo_BaseQueNoEsSqlite()
    {
        var respaldo = await RespaldoValidoAsync();
        var alterado = await AlterarAsync(
            respaldo, db => File.WriteAllTextAsync(db, new string('x', 8192)), recalcularHash: true);

        await AsegurarRechazoAsync(alterado, MotivoRechazo.BaseDanada);
    }

    [Fact]
    public async Task Rechazo_BaseSinHistorialDeMigraciones()
    {
        var respaldo = await RespaldoValidoAsync();
        var alterado = await AlterarAsync(
            respaldo, db => EjecutarAsync(db, "DROP TABLE __EFMigrationsHistory"), recalcularHash: true);

        await AsegurarRechazoAsync(alterado, MotivoRechazo.SinHistorialDeMigraciones);
    }

    [Fact]
    public async Task Rechazo_RespaldoDeUnaVersionMasNueva()
    {
        var respaldo = await RespaldoValidoAsync();
        var alterado = await AlterarAsync(
            respaldo,
            db => EjecutarAsync(db, "INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES ('29991231000000_Futura', '10.0.13')"),
            recalcularHash: true);

        await AsegurarRechazoAsync(alterado, MotivoRechazo.ProcedeDeVersionMasNueva);
    }

    [Fact]
    public async Task CopiasAutomaticas_SeConservanLasCincoMasNuevas()
    {
        var respaldo = await RespaldoValidoAsync();
        var actual = await OtraBaseAsync();
        var servicio = Servicio(actual);

        for (var i = 0; i < 7; i++)
        {
            _reloj.Avanzar(TimeSpan.FromMinutes(1));
            Assert.True((await servicio.RestaurarAsync(respaldo)).Restaurado);
        }

        var copias = servicio.ListarCopias();
        Assert.Equal(5, copias.Count);
        Assert.Equal(
            ["antes-de-restaurar-20261103-103700.voltiabak", "antes-de-restaurar-20261103-103600.voltiabak",
             "antes-de-restaurar-20261103-103500.voltiabak", "antes-de-restaurar-20261103-103400.voltiabak",
             "antes-de-restaurar-20261103-103300.voltiabak"],
            copias.Select(c => Path.GetFileName(c.Ruta)));
    }

    [Fact]
    public async Task CopiasAutomaticas_EnElMismoSegundo_NoSeSobrescriben()
    {
        var respaldo = await RespaldoValidoAsync();
        var actual = await OtraBaseAsync();
        var servicio = Servicio(actual);

        await servicio.RestaurarAsync(respaldo);
        await servicio.RestaurarAsync(respaldo);

        Assert.Equal(
            ["antes-de-restaurar-20261103-103000-2.voltiabak", "antes-de-restaurar-20261103-103000.voltiabak"],
            servicio.ListarCopias().Select(c => Path.GetFileName(c.Ruta)));
    }

    [Fact]
    public async Task CopiaAntesDeMigrar_BaseConDatosYMigracionesPendientes_LaCrea()
    {
        var actual = Ruta("voltia.db");
        await MigrarHastaAsync(actual, "_Metas");
        await EjecutarAsync(actual, MigracionNombreTarifa40Tests.Poblar);
        var servicio = Servicio(actual);

        var resultado = await servicio.CrearCopiaAntesDeMigrarAsync();

        Assert.Equal(EstadoCopiaPrevia.Creada, resultado.Estado);
        var copia = Assert.Single(servicio.ListarCopias());
        Assert.Equal(TipoCopia.AntesDeMigrar, copia.Tipo);
        Assert.Equal("antes-de-migrar-20261103-103000.voltiabak", Path.GetFileName(copia.Ruta));
        var validacion = await servicio.ValidarAsync(copia.Ruta);
        Assert.True(validacion.EsValido);
        Assert.EndsWith("_Metas", validacion.Manifiesto!.UltimaMigracion, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CopiaAntesDeMigrar_SinBase_NoCreaNada()
    {
        var servicio = Servicio(Ruta("voltia.db"));

        var resultado = await servicio.CrearCopiaAntesDeMigrarAsync();

        Assert.Equal(EstadoCopiaPrevia.NoHaceFalta, resultado.Estado);
        Assert.False(Directory.Exists(CarpetaRespaldos));
        Assert.False(File.Exists(Ruta("voltia.db")));
    }

    [Fact]
    public async Task CopiaAntesDeMigrar_SinMigracionesPendientes_NoCreaNada()
    {
        var actual = await OtraBaseAsync();

        var resultado = await Servicio(actual).CrearCopiaAntesDeMigrarAsync();

        Assert.Equal(EstadoCopiaPrevia.NoHaceFalta, resultado.Estado);
        Assert.False(Directory.Exists(CarpetaRespaldos));
    }

    [Fact]
    public async Task CopiaAntesDeMigrar_BaseSinDatos_NoCreaNada()
    {
        var actual = Ruta("voltia.db");
        await MigrarHastaAsync(actual, "_Metas");

        var resultado = await Servicio(actual).CrearCopiaAntesDeMigrarAsync();

        Assert.Equal(EstadoCopiaPrevia.NoHaceFalta, resultado.Estado);
        Assert.False(Directory.Exists(CarpetaRespaldos));
    }

    [Fact]
    public async Task CopiaAntesDeMigrar_SinPoderEscribir_DevuelveElFalloSinLanzar()
    {
        var actual = Ruta("voltia.db");
        await MigrarHastaAsync(actual, "_Metas");
        await EjecutarAsync(actual, MigracionNombreTarifa40Tests.Poblar);
        // Un archivo con el nombre de la carpeta impide crearla.
        var bloqueo = Ruta("Bloqueo");
        await File.WriteAllTextAsync(bloqueo, "x");

        var resultado = await Servicio(actual, bloqueo).CrearCopiaAntesDeMigrarAsync();

        Assert.Equal(EstadoCopiaPrevia.Fallo, resultado.Estado);
        Assert.NotNull(resultado.Error);
        Assert.Null(resultado.Ruta);
    }

    [Fact]
    public void ListarCopias_SinCarpeta_DevuelveVacioEIgnoraOtrosArchivos()
    {
        var servicio = Servicio(Ruta("voltia.db"));
        Assert.Empty(servicio.ListarCopias());

        Directory.CreateDirectory(CarpetaRespaldos);
        File.WriteAllText(Path.Combine(CarpetaRespaldos, "notas.txt"), "x");
        File.WriteAllText(Path.Combine(CarpetaRespaldos, "VoltiaMX-respaldo-2026-11-03.voltiabak"), "x");
        Assert.Empty(servicio.ListarCopias());
    }

    [Fact]
    public async Task IdaYVuelta_ConInquilinosYRepartos_RestauraLasTablasNuevas()
    {
        var actual = Ruta("voltia.db");
        var fabrica = new FabricaDeArchivo(actual);
        await using (var db = fabrica.CreateDbContext())
        {
            await new DbInicializador(db).InicializarAsync();
        }

        var demo = new DatosDemoService(fabrica, new TarifaCalculator());
        await demo.CargarAsync(new DateOnly(2026, 11, 3));
        var servicio = Servicio(actual);
        var respaldo = Ruta("con-inquilinos.voltiabak");
        Assert.Null((await servicio.CrearAsync(respaldo)).Error);

        // Vaciar: quitar la demo borra lugar, inquilinos y repartos.
        await demo.QuitarAsync();
        Assert.Equal((0, 0, 0), await ConteosInquilinosAsync(actual));

        var validacion = await servicio.ValidarAsync(respaldo);
        var restauracion = await servicio.RestaurarAsync(respaldo);

        // El manifiesto no cambia: sus conteos siguen siendo los de siempre.
        Assert.True(validacion.EsValido);
        Assert.Equal(new ConteosRespaldo(1, 15, 6, 5, 4), validacion.Manifiesto!.Conteos);
        Assert.True(restauracion.Restaurado);
        Assert.Equal((3, 1, 3), await ConteosInquilinosAsync(actual));
    }

    private static async Task<(long, long, long)> ConteosInquilinosAsync(string ruta)
    {
        await using var db = Contexto(ruta);
        return (await db.Inquilinos.LongCountAsync(), await db.Repartos.LongCountAsync(), await db.RepartoInquilinos.LongCountAsync());
    }

    private sealed class FabricaDeArchivo(string ruta) : IDbContextFactory<VoltiaDbContext>
    {
        public VoltiaDbContext CreateDbContext() => Contexto(ruta);
    }

    private sealed class RelojDePrueba(DateTimeOffset ahora) : TimeProvider
    {
        private DateTimeOffset _ahora = ahora;

        public override DateTimeOffset GetUtcNow() => _ahora;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public void Avanzar(TimeSpan lapso) => _ahora += lapso;
    }
}
