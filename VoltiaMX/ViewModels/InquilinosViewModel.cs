using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Un inquilino en la lista, con sus acciones. <see cref="AccionEstado"/> da de baja o reactiva.
/// </summary>
public sealed record FilaInquilino(
    string Nombre,
    bool Activo,
    string TextoEstado,
    bool PuedeEliminar,
    IRelayCommand Editar,
    IAsyncRelayCommand AccionEstado,
    IAsyncRelayCommand Eliminar);

/// <summary>
/// Un reparto del historial, con sus acciones.
/// </summary>
public sealed record FilaReparto(
    string Periodo,
    string Total,
    string Detalle,
    IRelayCommand Ver,
    IAsyncRelayCommand ExportarPdf,
    IAsyncRelayCommand Eliminar);

/// <summary>
/// Inquilinos del lugar principal (alta, edición, baja y eliminación) y el historial de repartos.
/// El nombre se valida 300 ms después de cada cambio.
/// </summary>
public sealed partial class InquilinosViewModel(
    IInquilinoService inquilinos,
    IRepartoService repartos,
    ILecturaService lecturas,
    IGuardadorArchivos guardador,
    IDialogos dialogos,
    OpcionesPdf fuentes,
    ITextos textos) : ObservableObject
{
    private static readonly TimeSpan EsperaValidacion = TimeSpan.FromMilliseconds(300);

    private int? _lugarId;
    private int? _editando;
    private bool _intentoGuardar;
    private CancellationTokenSource? _validacion;
    private IReadOnlyList<InquilinoListado> _lista = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool TieneLugar { get; set; }

    [ObservableProperty] public partial bool EsDemo { get; set; }
    [ObservableProperty] public partial bool TieneInquilinos { get; set; }
    [ObservableProperty] public partial bool MostrarVacio { get; set; }
    [ObservableProperty] public partial bool TieneRepartos { get; set; }
    [ObservableProperty] public partial bool SinRepartos { get; set; }
    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;
    [ObservableProperty] public partial string Error { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NuevoRepartoCommand))]
    public partial bool HayActivos { get; set; }

    // ---- Formulario ----

    [ObservableProperty] public partial string Nombre { get; set; } = string.Empty;
    [ObservableProperty] public partial bool EnEdicion { get; set; }
    [ObservableProperty] public partial string TextoGuardar { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneErrores { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Valido { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Trabajando { get; set; }

    public ObservableCollection<string> Errores { get; } = [];

    public ObservableCollection<FilaInquilino> Filas { get; } = [];

    public ObservableCollection<FilaReparto> Repartos { get; } = [];

    public async Task CargarAsync()
    {
        TextoGuardar = textos["Inquilinos_Agregar"];
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;
        EsDemo = lugar?.EsDemo == true;
        await RecargarAsync();
    }

    private async Task RecargarAsync()
    {
        _lista = _lugarId is int lugarId ? await inquilinos.ListarAsync(lugarId) : [];
        Filas.Clear();
        foreach (var inquilino in _lista)
        {
            var (id, nombre) = (inquilino.Id, inquilino.Nombre);
            Filas.Add(new FilaInquilino(
                nombre,
                inquilino.Activo,
                textos[inquilino.Activo ? "Inquilinos_DarDeBaja" : "Inquilinos_Reactivar"],
                !inquilino.TieneRepartos,
                new RelayCommand(() => Editar(id)),
                new AsyncRelayCommand(() => inquilino.Activo ? DarDeBajaAsync(id, nombre) : ReactivarAsync(id, nombre)),
                new AsyncRelayCommand(() => EliminarAsync(id, nombre))));
        }

        TieneInquilinos = Filas.Count > 0;
        MostrarVacio = !TieneInquilinos;
        HayActivos = _lista.Any(i => i.Activo);

        Repartos.Clear();
        foreach (var reparto in _lugarId is int l ? await repartos.ListarAsync(l) : [])
        {
            var (id, fin) = (reparto.Id, reparto.Fin);
            Repartos.Add(new FilaReparto(
                $"{FormatoReporte.Fecha(reparto.Inicio)} – {FormatoReporte.Fecha(reparto.Fin)}",
                textos.Formato("Inquilinos_RepartoTotal", reparto.TotalCobrado),
                textos.Formato("Inquilinos_RepartoDetalle", reparto.Inquilinos, reparto.TotalRepartido),
                new RelayCommand(() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Inquilinos, SeccionesInquilinos.VerReparto(id)))),
                new AsyncRelayCommand(() => ExportarResumenAsync(id, fin)),
                new AsyncRelayCommand(() => EliminarRepartoAsync(id))));
        }

        TieneRepartos = Repartos.Count > 0;
        SinRepartos = !TieneRepartos;
    }

    partial void OnNombreChanged(string value) => ProgramarValidacion();

    private async void ProgramarValidacion()
    {
        _validacion?.Cancel();
        var cancelacion = _validacion = new CancellationTokenSource();
        try
        {
            await Task.Delay(EsperaValidacion, cancelacion.Token);
            Validar();
        }
        catch (OperationCanceledException)
        {
            // Llegó otro cambio.
        }
    }

    /// <summary>
    /// Valida sin esperar (Enter guarda aunque no hayan pasado los 300 ms).
    /// </summary>
    public void ValidarAhora()
    {
        _validacion?.Cancel();
        _intentoGuardar = true;
        Validar();
    }

    private void Validar()
    {
        var otros = _lista.Where(i => i.Id != _editando).ToList();
        var editado = _lista.FirstOrDefault(i => i.Id == _editando);
        var activos = editado is { Activo: false } ? 0 : otros.Count(i => i.Activo);
        var errores = InquilinoValidador.Validar(Nombre, otros.Select(i => i.Nombre), activos);
        MostrarErrores(errores, mostrarVacio: _intentoGuardar || Nombre.Length > 0);
        Valido = errores.Count == 0;
    }

    private void MostrarErrores(IReadOnlyList<CodigoErrorInquilino> errores, bool mostrarVacio)
    {
        Errores.Clear();
        foreach (var error in errores.Where(e => mostrarVacio || e != CodigoErrorInquilino.NombreVacio))
        {
            Errores.Add(textos[$"Inquilino_Error_{error}"]);
        }

        TieneErrores = Errores.Count > 0;
    }

    private bool PuedeGuardar() => TieneLugar && Valido && !Trabajando;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (_lugarId is not int lugarId)
        {
            return;
        }

        Trabajando = true;
        try
        {
            var resultado = await inquilinos.GuardarAsync(new InquilinoEditable(_editando, lugarId, Nombre), DateOnly.FromDateTime(DateTime.Today));
            if (resultado.Errores.Count > 0)
            {
                MostrarErrores(resultado.Errores, mostrarVacio: true);
                Valido = false;
                return;
            }

            var nombre = Nombre.Trim();
            Limpiar();
            await RecargarAsync();
            Aviso = textos.Formato("Inquilinos_Guardado", nombre);
        }
        finally
        {
            Trabajando = false;
        }
    }

    private void Editar(int id)
    {
        if (_lista.FirstOrDefault(i => i.Id == id) is not { } inquilino)
        {
            return;
        }

        _editando = id;
        EnEdicion = true;
        TextoGuardar = textos["Inquilinos_GuardarCambios"];
        Nombre = inquilino.Nombre;
        Aviso = Error = string.Empty;
    }

    [RelayCommand]
    private void Cancelar() => Limpiar();

    private void Limpiar()
    {
        _validacion?.Cancel();
        _editando = null;
        _intentoGuardar = false;
        EnEdicion = false;
        TextoGuardar = textos["Inquilinos_Agregar"];
        Nombre = string.Empty;
        _validacion?.Cancel();
        Errores.Clear();
        TieneErrores = Valido = false;
    }

    private async Task DarDeBajaAsync(int id, string nombre)
    {
        await inquilinos.DarDeBajaAsync(id);
        await RecargarAsync();
        Aviso = textos.Formato("Inquilinos_DadoDeBaja", nombre);
        Error = string.Empty;
    }

    private async Task ReactivarAsync(int id, string nombre)
    {
        var errores = await inquilinos.ReactivarAsync(id);
        Error = errores.Count > 0 ? textos[$"Inquilino_Error_{errores[0]}"] : string.Empty;
        await RecargarAsync();
        Aviso = errores.Count > 0 ? string.Empty : textos.Formato("Inquilinos_Reactivado", nombre);
    }

    private async Task EliminarAsync(int id, string nombre)
    {
        if (!await dialogos.ConfirmarAsync(
                textos["Inquilinos_EliminarTitulo"],
                textos.Formato("Inquilinos_EliminarTexto", nombre),
                textos["Eliminar_Confirmar"],
                textos["Eliminar_Cancelar"]))
        {
            return;
        }

        var resultado = await inquilinos.EliminarAsync(id);
        Error = resultado == ResultadoEliminarInquilino.TieneRepartos ? textos["Inquilinos_NoSeEliminaConRepartos"] : string.Empty;
        if (_editando == id)
        {
            Limpiar();
        }

        await RecargarAsync();
        Aviso = resultado == ResultadoEliminarInquilino.Eliminado ? textos.Formato("Inquilinos_Eliminado", nombre) : string.Empty;
    }

    [RelayCommand(CanExecute = nameof(HayActivos))]
    private void NuevoReparto()
        => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Inquilinos, SeccionesInquilinos.NuevoReparto));

    private async Task ExportarResumenAsync(int id, DateOnly fin)
    {
        Aviso = Error = string.Empty;
        var resultado = await guardador.GuardarAsync(NombreArchivo.Resumen(fin), TipoArchivo.Pdf, async () =>
        {
            var reparto = await repartos.ObtenerAsync(id) ?? throw new InvalidOperationException("El reparto ya no existe.");
            return await Task.Run(() => GeneradorPdfReparto.Resumen(reparto, Texto, fuentes));
        });

        if (resultado.Ruta is { } ruta)
        {
            Aviso = textos.Formato("Reportes_Guardado", ruta);
        }
        else if (resultado.Error is { } error)
        {
            Error = textos[$"Reportes_Error_{error}"];
        }
    }

    private async Task EliminarRepartoAsync(int id)
    {
        if (!await dialogos.ConfirmarAsync(
                textos["Inquilinos_EliminarRepartoTitulo"],
                textos["Inquilinos_EliminarRepartoTexto"],
                textos["Eliminar_Confirmar"],
                textos["Eliminar_Cancelar"]))
        {
            return;
        }

        await repartos.EliminarAsync(id);
        await RecargarAsync();
        Aviso = textos["Inquilinos_RepartoEliminado"];
    }

    private string Texto(string clave) => textos[clave];

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

#if DEBUG
    /// <summary>
    /// Solo para pruebas automatizadas: carpeta donde se exporta sin abrir el selector.
    /// </summary>
    public string CarpetaPrueba
    {
        get => guardador.CarpetaPrueba ?? string.Empty;
        set => guardador.CarpetaPrueba = value;
    }
#endif
}
