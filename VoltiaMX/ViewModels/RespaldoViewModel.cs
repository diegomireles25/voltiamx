using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Una fila del contenido de un respaldo validado.
/// </summary>
public sealed record FilaDatoRespaldo(string Etiqueta, string Valor);

/// <summary>
/// Una copia automática de la carpeta Respaldos.
/// </summary>
public sealed record FilaCopiaRespaldo(string Fecha, string Tipo, string Tamano);

/// <summary>
/// Crear un respaldo, validar y restaurar uno, y ver las copias automáticas. Todo queda en el equipo.
/// </summary>
public sealed partial class RespaldoViewModel(
    IRespaldoService respaldo,
    ILecturaService lecturas,
    ISelectorArchivos selector,
    IGuardadorArchivos carpetas,
    IDialogos dialogos,
    IAplicacion aplicacion,
    ITextos textos) : ObservableObject
{
    private string? _rutaValidada;

    [ObservableProperty] public partial bool EsDemo { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CrearRespaldoCommand), nameof(ElegirRespaldoCommand), nameof(RestaurarCommand))]
    public partial bool Trabajando { get; set; }

    /// <summary>
    /// La restauración terminó: la pantalla ya no muestra datos y solo queda cerrar la app.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarContenido))]
    [NotifyCanExecuteChangedFor(nameof(CrearRespaldoCommand), nameof(ElegirRespaldoCommand), nameof(RestaurarCommand))]
    public partial bool Restaurado { get; set; }

    public bool MostrarContenido => !Restaurado;

    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;
    [ObservableProperty] public partial string Rechazo { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestaurarCommand))]
    public partial bool RespaldoValido { get; set; }

    [ObservableProperty] public partial bool HayCopias { get; set; }

    public ObservableCollection<FilaDatoRespaldo> Contenido { get; } = [];

    public ObservableCollection<FilaCopiaRespaldo> Copias { get; } = [];

    public async Task CargarAsync()
    {
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        EsDemo = lugar?.EsDemo == true;
        CargarCopias();
    }

    private bool PuedeTrabajar() => !Trabajando && !Restaurado;

    [RelayCommand(CanExecute = nameof(PuedeTrabajar))]
    private async Task CrearRespaldoAsync()
    {
        Limpiar();
        var destino = await selector.ElegirDestinoRespaldoAsync(respaldo.NombreSugerido());
        if (destino is null)
        {
            return;
        }

        Trabajando = true;
        try
        {
            var resultado = await respaldo.CrearAsync(destino);
            if (resultado.Error is { } error)
            {
                Error = textos[$"Respaldo_Error_{error}"];
            }
            else
            {
                Aviso = textos.Formato("Respaldo_Guardado", resultado.Ruta);
            }
        }
        finally
        {
            Trabajando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeTrabajar))]
    private async Task ElegirRespaldoAsync()
    {
        var origen = await selector.ElegirRespaldoAsync();
        if (origen is null)
        {
            return;
        }

        Limpiar();
        Trabajando = true;
        try
        {
            var validacion = await respaldo.ValidarAsync(origen);
            if (validacion is { Motivo: null, Manifiesto: { } manifiesto })
            {
                MostrarManifiesto(manifiesto);
                _rutaValidada = origen;
                RespaldoValido = true;
            }
            else
            {
                Rechazo = textos[$"Respaldo_Rechazo_{validacion.Motivo}"];
            }
        }
        finally
        {
            Trabajando = false;
        }
    }

    private bool PuedeRestaurar() => PuedeTrabajar() && RespaldoValido;

    [RelayCommand(CanExecute = nameof(PuedeRestaurar))]
    private async Task RestaurarAsync()
    {
        if (_rutaValidada is not { } origen
            || !await dialogos.ConfirmarAsync(
                textos["Respaldo_ConfirmarTitulo"],
                textos.Formato("Respaldo_ConfirmarTexto", respaldo.CarpetaRespaldos),
                textos["Respaldo_ConfirmarBoton"],
                textos["Respaldo_Cancelar"]))
        {
            return;
        }

        Aviso = Error = string.Empty;
        Trabajando = true;
        ResultadoRestauracion resultado;
        try
        {
            await aplicacion.PausarTareasAsync();
            resultado = await respaldo.RestaurarAsync(origen);
        }
        finally
        {
            Trabajando = false;
        }

        if (resultado.Restaurado)
        {
            // Sin datos viejos en pantalla: solo queda cerrar y volver a abrir.
            Restaurado = true;
            Contenido.Clear();
            Copias.Clear();
            HayCopias = RespaldoValido = false;
            await dialogos.AvisarAsync(textos["Respaldo_RestauradoTitulo"], textos["Respaldo_RestauradoTexto"], textos["Respaldo_CerrarApp"]);
            aplicacion.Cerrar();
            return;
        }

        aplicacion.ReanudarTareas();
        if (resultado.Rechazo is { } motivo)
        {
            Limpiar();
            Rechazo = textos[$"Respaldo_Rechazo_{motivo}"];
        }
        else
        {
            Error = textos[$"Respaldo_Error_{resultado.Error}"]
                + (resultado.Revertido ? " " + textos["Respaldo_Revertido"] : string.Empty);
        }

        CargarCopias();
    }

    [RelayCommand]
    private async Task AbrirCarpetaAsync() => await carpetas.AbrirDirectorioAsync(respaldo.CarpetaRespaldos);

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    private void Limpiar()
    {
        Aviso = Error = Rechazo = string.Empty;
        Contenido.Clear();
        _rutaValidada = null;
        RespaldoValido = false;
    }

    private void MostrarManifiesto(ManifiestoRespaldo manifiesto)
    {
        var creado = manifiesto.CreadoUtc.ToLocalTime();
        Contenido.Add(new(textos["Respaldo_DatoFecha"], $"{FormatoReporte.Fecha(creado)}, {creado:HH:mm}"));
        Contenido.Add(new(textos["Respaldo_DatoVersion"], manifiesto.VersionApp));
        Contenido.Add(new(textos["Respaldo_DatoLugares"], Entero(manifiesto.Conteos.Lugares)));
        Contenido.Add(new(textos["Respaldo_DatoLecturas"], Entero(manifiesto.Conteos.Lecturas)));
        Contenido.Add(new(textos["Respaldo_DatoPeriodos"], Entero(manifiesto.Conteos.Periodos)));
        Contenido.Add(new(textos["Respaldo_DatoTarifas"], Entero(manifiesto.Conteos.Tarifas)));
        Contenido.Add(new(textos["Respaldo_DatoAparatos"], Entero(manifiesto.Conteos.Aparatos)));
    }

    private void CargarCopias()
    {
        Copias.Clear();
        foreach (var copia in respaldo.ListarCopias())
        {
            Copias.Add(new(
                $"{FormatoReporte.Fecha(copia.Fecha)}, {copia.Fecha:HH:mm}",
                textos[copia.Tipo == TipoCopia.AntesDeMigrar ? "Respaldo_TipoAntesDeMigrar" : "Respaldo_TipoAntesDeRestaurar"],
                Tamano(copia.Bytes)));
        }

        HayCopias = Copias.Count > 0;
    }

    private static string Entero(int valor) => valor.ToString("#,##0", CultureInfo.InvariantCulture);

    private string Tamano(long bytes) => bytes >= 1024 * 1024
        ? textos.Formato("Respaldo_TamanoMb", bytes / (1024d * 1024d))
        : textos.Formato("Respaldo_TamanoKb", Math.Max(1, (int)Math.Ceiling(bytes / 1024d)));

#if DEBUG
    /// <summary>
    /// Solo para pruebas automatizadas: carpeta donde se guarda el respaldo nuevo sin abrir el selector.
    /// </summary>
    public string CarpetaPrueba
    {
        get => selector.CarpetaPrueba ?? string.Empty;
        set => selector.CarpetaPrueba = value;
    }

    /// <summary>
    /// Solo para pruebas automatizadas: respaldo que se elige sin abrir el selector.
    /// </summary>
    public string RespaldoPrueba
    {
        get => selector.RespaldoPrueba ?? string.Empty;
        set => selector.RespaldoPrueba = value;
    }
#endif
}
