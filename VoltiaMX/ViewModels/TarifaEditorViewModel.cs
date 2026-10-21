using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Opción del tipo de tarifa para el ComboBox.
/// </summary>
public sealed record OpcionTipoTarifa(TipoTarifa Tipo, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Rango del editor. <see cref="Hasta"/> es NaN cuando está vacío; el último rango siempre queda sin
/// límite y no se edita su tope. <see cref="DesdeTexto"/> lo calcula el editor.
/// </summary>
public sealed partial class RangoEnEdicion : ObservableObject
{
    [ObservableProperty] public partial string Nombre { get; set; } = string.Empty;
    [ObservableProperty] public partial double Hasta { get; set; } = double.NaN;
    [ObservableProperty] public partial double Precio { get; set; } = double.NaN;
    [ObservableProperty] public partial string DesdeTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string Numero { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HastaEditable))]
    public partial bool EsUltimo { get; set; }

    public bool HastaEditable => !EsUltimo;

    public IRelayCommand? Quitar { get; set; }
}

/// <summary>
/// Fila de la vista previa "Así se vería".
/// </summary>
public sealed record FilaVistaTarifa(string Consumo, string Monto);

/// <summary>
/// Alta y edición de una tarifa. Valida con TarifaValidador 300 ms después de cada cambio y calcula
/// la vista previa con TarifaCalculator (el cálculo real) mientras los datos sean válidos.
/// </summary>
public sealed partial class TarifaEditorViewModel(
    ITarifaService tarifas,
    ITarifaCalculator calculadora,
    IDialogos dialogos,
    ITextos textos,
    INavegacionShell navegacion) : ObservableObject
{
    private static readonly TimeSpan EsperaValidacion = TimeSpan.FromMilliseconds(300);
    private static readonly decimal[] KwhDeLaVistaPrevia = [100m, 250m, 500m]; // bimestrales
    private const int MesesDeLaVistaPrevia = 2;

    private int? _id;
    private bool _enUso;
    private IReadOnlyList<string> _otrosNombres = [];
    private CancellationTokenSource? _validacion;
    private bool _cargando;

    [ObservableProperty] public partial string Titulo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Nombre { get; set; } = string.Empty;
    [ObservableProperty] public partial OpcionTipoTarifa? TipoElegido { get; set; }
    [ObservableProperty] public partial DateTimeOffset? Vigencia { get; set; }
    [ObservableProperty] public partial double CargoFijo { get; set; } = 0;
    [ObservableProperty] public partial double LimiteDac { get; set; } = double.NaN;
    [ObservableProperty] public partial bool EsDomestica { get; set; } = true;
    [ObservableProperty] public partial bool TieneErrores { get; set; }
    [ObservableProperty] public partial bool TieneVistaPrevia { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Valida { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Guardando { get; set; }

    public IReadOnlyList<OpcionTipoTarifa> Tipos { get; } =
        Enum.GetValues<TipoTarifa>().Select(t => new OpcionTipoTarifa(t, textos[$"TipoTarifa_{t}"])).ToList();

    public ObservableCollection<RangoEnEdicion> Rangos { get; } = [];

    public ObservableCollection<string> Errores { get; } = [];

    public ObservableCollection<FilaVistaTarifa> VistaPrevia { get; } = [];

    public async Task CargarAsync(int? id)
    {
        _cargando = true;
        _id = id;
        var lista = await tarifas.ListarConUsoAsync();
        _otrosNombres = lista.Where(t => t.Id != id).Select(t => t.Nombre).ToList();
        _enUso = lista.FirstOrDefault(t => t.Id == id)?.EnUso == true;

        var editable = id is int existente ? await tarifas.ObtenerParaEditarAsync(existente) : null;
        Titulo = textos[editable is null ? "TarifaEditor_TituloNueva" : "TarifaEditor_TituloEditar"];
        Nombre = editable?.Nombre ?? string.Empty;
        TipoElegido = Tipos.First(t => t.Tipo == (editable?.Tipo ?? TipoTarifa.Domestica));
        Vigencia = new DateTimeOffset((editable?.Vigencia ?? DateOnly.FromDateTime(DateTime.Today)).ToDateTime(TimeOnly.MinValue));
        CargoFijo = (double)(editable?.CargoFijoMensual ?? 0m);
        LimiteDac = editable?.LimiteDacKwhMensual is decimal limite ? (double)limite : double.NaN;

        Rangos.Clear();
        var rangos = editable?.Rangos ?? [new RangoEditable(textos["TarifaEditor_RangoPorDefecto"], null, 0m)];
        foreach (var rango in rangos)
        {
            Agregar(new RangoEnEdicion
            {
                Nombre = rango.Nombre,
                Hasta = rango.KwhHasta is decimal hasta ? (double)hasta : double.NaN,
                Precio = editable is null ? double.NaN : (double)rango.PrecioPorKwh,
            });
        }

        _cargando = false;
        Renumerar();
        Validar();
    }

    partial void OnNombreChanged(string value) => ProgramarValidacion();

    partial void OnVigenciaChanged(DateTimeOffset? value) => ProgramarValidacion();

    partial void OnCargoFijoChanged(double value) => ProgramarValidacion();

    partial void OnLimiteDacChanged(double value) => ProgramarValidacion();

    partial void OnTipoElegidoChanged(OpcionTipoTarifa? value)
    {
        EsDomestica = value?.Tipo == TipoTarifa.Domestica;
        ProgramarValidacion();
    }

    [RelayCommand]
    private void AgregarRango()
    {
        // El nuevo rango entra antes del último, que siempre es "sin límite".
        var nuevo = new RangoEnEdicion { Nombre = textos.Formato("TarifaEditor_RangoNuevo", Rangos.Count + 1) };
        Agregar(nuevo, Math.Max(Rangos.Count - 1, 0));
        Renumerar();
        ProgramarValidacion();
    }

    private void Agregar(RangoEnEdicion rango, int? posicion = null)
    {
        rango.Quitar = new RelayCommand(() => QuitarRango(rango), () => Rangos.Count > 1);
        rango.PropertyChanged += AlCambiarRango;
        if (posicion is int indice)
        {
            Rangos.Insert(indice, rango);
        }
        else
        {
            Rangos.Add(rango);
        }
    }

    private void QuitarRango(RangoEnEdicion rango)
    {
        if (Rangos.Count <= 1)
        {
            return;
        }

        rango.PropertyChanged -= AlCambiarRango;
        Rangos.Remove(rango);
        Renumerar();
        ProgramarValidacion();
    }

    private void AlCambiarRango(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(RangoEnEdicion.Nombre) or nameof(RangoEnEdicion.Hasta) or nameof(RangoEnEdicion.Precio))
        {
            ProgramarValidacion();
        }
    }

    /// <summary>
    /// Números, "desde" calculado y el último sin límite.
    /// </summary>
    private void Renumerar()
    {
        var desde = 0d;
        for (var i = 0; i < Rangos.Count; i++)
        {
            var rango = Rangos[i];
            rango.Numero = textos.Formato("TarifaEditor_NumeroRango", i + 1);
            rango.EsUltimo = i == Rangos.Count - 1;
            if (rango.EsUltimo)
            {
                rango.Hasta = double.NaN;
            }

            rango.DesdeTexto = textos.Formato("TarifaEditor_Desde", desde);
            desde = double.IsNaN(rango.Hasta) ? desde : rango.Hasta;
            rango.Quitar?.NotifyCanExecuteChanged();
        }
    }

    private async void ProgramarValidacion()
    {
        if (_cargando)
        {
            return;
        }

        Renumerar();
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

    private TarifaEditable Editable() => new(
        _id,
        Nombre,
        TipoElegido?.Tipo ?? TipoTarifa.Domestica,
        double.IsNaN(CargoFijo) ? 0m : (decimal)CargoFijo,
        EsDomestica && !double.IsNaN(LimiteDac) ? (decimal)LimiteDac : null,
        Vigencia is DateTimeOffset vigencia ? DateOnly.FromDateTime(vigencia.Date) : null,
        Rangos.Select(r => new RangoEditable(
            r.Nombre,
            double.IsNaN(r.Hasta) ? null : (decimal)r.Hasta,
            double.IsNaN(r.Precio) ? 0m : (decimal)r.Precio)).ToList());

    private void Validar()
    {
        var editable = Editable();
        var errores = TarifaValidador.Validar(editable, _otrosNombres);
        Errores.Clear();
        foreach (var error in errores)
        {
            var texto = textos[$"Tarifa_Error_{error.Codigo}"];
            Errores.Add(error.Rango is int numero ? textos.Formato("Tarifa_ErrorEnRango", numero, texto) : texto);
        }

        TieneErrores = Errores.Count > 0;
        Valida = !TieneErrores;

        // La vista previa usa el cálculo real; solo se puede con rangos válidos.
        VistaPrevia.Clear();
        TieneVistaPrevia = Valida;
        if (Valida)
        {
            var tarifa = editable.ComoTarifa();
            foreach (var kwh in KwhDeLaVistaPrevia)
            {
                VistaPrevia.Add(new FilaVistaTarifa(
                    textos.Formato("TarifaEditor_VistaKwh", kwh),
                    textos.Formato("Formato_Centavos", calculadora.Calcular(kwh, tarifa, MesesDeLaVistaPrevia).Total)));
            }
        }
    }

    private bool PuedeGuardar() => Valida && !Guardando;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        Validar();
        if (!Valida)
        {
            return;
        }

        if (_enUso && !await dialogos.ConfirmarAsync(
                textos["TarifaEditor_EnUsoTitulo"],
                textos["TarifaEditor_EnUsoTexto"],
                textos["TarifaEditor_Guardar"],
                textos["Eliminar_Cancelar"]))
        {
            return;
        }

        Guardando = true;
        try
        {
            var resultado = await tarifas.GuardarAsync(Editable());
            if (resultado.Guardada)
            {
                navegacion.Volver();
                return;
            }

            Errores.Clear();
            foreach (var error in resultado.Errores)
            {
                Errores.Add(textos[$"Tarifa_Error_{error.Codigo}"]);
            }

            TieneErrores = true;
        }
        finally
        {
            Guardando = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => navegacion.Volver();
}
