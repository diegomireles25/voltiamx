using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Opción de categoría para el ComboBox.
/// </summary>
public sealed record OpcionCategoria(CategoriaAparato Categoria, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Plantilla de referencia con su nombre visible.
/// </summary>
public sealed record OpcionPlantilla(PlantillaAparato Plantilla, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Fila de "Tus aparatos" con sus enlaces.
/// </summary>
public sealed record FilaAparato(
    string Nombre,
    string Categoria,
    string Detalle,
    string KwhTexto,
    IRelayCommand Editar,
    IAsyncRelayCommand Eliminar,
    IRelayCommand Simular);

/// <summary>
/// Aparatos del lugar principal: lista, alta desde plantilla o a mano y editor en la misma página.
/// Valida con AparatoValidador 300 ms después de cada cambio, igual que el editor de tarifas.
/// </summary>
public sealed partial class AparatosViewModel(
    IAparatoService aparatos,
    ILecturaService lecturas,
    IDialogos dialogos,
    ITextos textos) : ObservableObject
{
    private static readonly TimeSpan EsperaValidacion = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DuracionAviso = TimeSpan.FromSeconds(6);

    private int? _lugarId;
    private int? _id;
    private bool _cargando;
    private CancellationTokenSource? _validacion;
    private CancellationTokenSource? _aviso;
    private IReadOnlyList<Aparato> _lista = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AgregarAparatoCommand))]
    public partial bool TieneLugar { get; set; }

    [ObservableProperty] public partial bool EsDemo { get; set; }
    [ObservableProperty] public partial bool TieneAparatos { get; set; }
    [ObservableProperty] public partial bool MostrarVacio { get; set; }
    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;

    // ---- Ranking: porcentajes contra el consumo medido ----

    [ObservableProperty] public partial bool TieneRanking { get; set; }
    [ObservableProperty] public partial string TextoRanking { get; set; } = string.Empty;
    [ObservableProperty] public partial bool SumaExcedeMedido { get; set; }
    [ObservableProperty] public partial bool TieneSinIdentificar { get; set; }
    [ObservableProperty] public partial string SinIdentificarTexto { get; set; } = string.Empty;

    public ObservableCollection<FilaDetalle> FilasRanking { get; } = [];

    // ---- Editor ----

    [ObservableProperty] public partial bool EnEdicion { get; set; }
    [ObservableProperty] public partial string TituloEditor { get; set; } = string.Empty;
    [ObservableProperty] public partial string Nombre { get; set; } = string.Empty;
    [ObservableProperty] public partial OpcionCategoria? CategoriaElegida { get; set; }
    [ObservableProperty] public partial double Cantidad { get; set; } = 1;
    [ObservableProperty] public partial double Watts { get; set; } = double.NaN;
    [ObservableProperty] public partial double HorasPorDia { get; set; } = double.NaN;
    [ObservableProperty] public partial double DiasPorSemana { get; set; } = 7;
    [ObservableProperty] public partial double CicloDeTrabajo { get; set; } = 1;
    [ObservableProperty] public partial bool TieneErrores { get; set; }
    [ObservableProperty] public partial bool TieneVistaPrevia { get; set; }
    [ObservableProperty] public partial string VistaPreviaTexto { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Valida { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Guardando { get; set; }

    [ObservableProperty] public partial OpcionPlantilla? PlantillaElegida { get; set; }

    public IReadOnlyList<OpcionCategoria> Categorias { get; } =
        Enum.GetValues<CategoriaAparato>().Select(c => new OpcionCategoria(c, textos[$"CategoriaAparato_{c}"])).ToList();

    public IReadOnlyList<OpcionPlantilla> Plantillas { get; } =
        aparatos.PlantillasDeReferencia().Select(p => new OpcionPlantilla(p, textos[$"Plantilla_{p.Clave}"])).ToList();

    public ObservableCollection<FilaAparato> Filas { get; } = [];

    public ObservableCollection<string> Errores { get; } = [];

    public async Task CargarAsync()
    {
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;
        EsDemo = lugar?.EsDemo == true;
        await RecargarListaAsync();
    }

    private async Task RecargarListaAsync()
    {
        _lista = _lugarId is int lugarId ? await aparatos.ListarAsync(lugarId) : [];
        Filas.Clear();
        foreach (var aparato in _lista)
        {
            var id = aparato.Id;
            var nombre = aparato.Nombre;
            Filas.Add(new FilaAparato(
                aparato.Nombre,
                textos[$"CategoriaAparato_{aparato.Categoria}"],
                textos.Formato("Aparatos_Detalle", aparato.Cantidad, aparato.Watts, aparato.HorasPorDia, aparato.DiasPorSemana),
                textos.Formato("Aparatos_KwhMes", ConsumoAparato.KwhPorMes(aparato)),
                new RelayCommand(() => Editar(id)),
                new AsyncRelayCommand(() => EliminarAsync(id, nombre)),
                new RelayCommand(() => WeakReferenceMessenger.Default.Send(
                    new IrAModuloMensaje(ModuloApp.Aparatos, SeccionesAparatos.SimuladorDe(id))))));
        }

        TieneAparatos = Filas.Count > 0;
        MostrarVacio = !TieneAparatos;
        await RecargarRankingAsync();
    }

    private async Task RecargarRankingAsync()
    {
        FilasRanking.Clear();
        if (_lugarId is not int lugarId || !TieneAparatos)
        {
            TieneRanking = SumaExcedeMedido = TieneSinIdentificar = false;
            return;
        }

        var ranking = await aparatos.ObtenerRankingAsync(lugarId, DateOnly.FromDateTime(DateTime.Today));
        foreach (var fila in ranking.Filas)
        {
            FilasRanking.Add(new FilaDetalle(fila.Nombre, FormatoRanking.Valor(fila, textos)));
        }

        TieneRanking = FilasRanking.Count > 0;
        SumaExcedeMedido = ranking.Estado == EstadoRanking.SumaExcedeMedido;
        TieneSinIdentificar = ranking.SinIdentificar is not null;
        SinIdentificarTexto = ranking.SinIdentificar is decimal sinIdentificar
            ? FormatoRanking.Porcentaje(sinIdentificar, textos)
            : string.Empty;
        TextoRanking = ranking.KwhMedidoPorMes is decimal medido && ranking.Estado != EstadoRanking.SinConsumoMedido
            ? textos.Formato("Ranking_Medido", medido)
            : textos["Ranking_SinConsumo"];
    }

    partial void OnNombreChanged(string value) => ProgramarValidacion();

    partial void OnCategoriaElegidaChanged(OpcionCategoria? value) => ProgramarValidacion();

    partial void OnCantidadChanged(double value) => ProgramarValidacion();

    partial void OnWattsChanged(double value) => ProgramarValidacion();

    partial void OnHorasPorDiaChanged(double value) => ProgramarValidacion();

    partial void OnDiasPorSemanaChanged(double value) => ProgramarValidacion();

    partial void OnCicloDeTrabajoChanged(double value) => ProgramarValidacion();

    // Elegir una plantilla abre el editor ya lleno con sus valores típicos.
    partial void OnPlantillaElegidaChanged(OpcionPlantilla? value)
    {
        if (value is not { } opcion || !TieneLugar)
        {
            return;
        }

        var plantilla = opcion.Plantilla;
        AbrirEditor(
            null,
            textos["Aparatos_TituloNuevo"],
            new AparatoEditable(
                null, _lugarId ?? 0, opcion.Texto, plantilla.Categoria, 1, plantilla.Watts, plantilla.HorasPorDia,
                plantilla.DiasPorSemana, plantilla.CicloDeTrabajo));
    }

    [RelayCommand(CanExecute = nameof(TieneLugar))]
    private void AgregarAparato()
    {
        _cargando = true;
        _id = null;
        TituloEditor = textos["Aparatos_TituloNuevo"];
        Nombre = string.Empty;
        CategoriaElegida = Categorias.First(c => c.Categoria == CategoriaAparato.Otro);
        Cantidad = 1;
        Watts = double.NaN;
        HorasPorDia = double.NaN;
        DiasPorSemana = 7;
        CicloDeTrabajo = 1;
        _cargando = false;
        EnEdicion = true;
        Validar();
    }

    private void Editar(int id)
    {
        if (_lista.FirstOrDefault(a => a.Id == id) is not { } aparato)
        {
            return;
        }

        AbrirEditor(
            id,
            textos["Aparatos_TituloEditar"],
            new AparatoEditable(
                id, aparato.LugarId, aparato.Nombre, aparato.Categoria, aparato.Cantidad, aparato.Watts,
                aparato.HorasPorDia, aparato.DiasPorSemana, aparato.CicloDeTrabajo));
    }

    private void AbrirEditor(int? id, string titulo, AparatoEditable valores)
    {
        _cargando = true;
        _id = id;
        TituloEditor = titulo;
        Nombre = valores.Nombre;
        CategoriaElegida = Categorias.First(c => c.Categoria == valores.Categoria);
        Cantidad = valores.Cantidad;
        Watts = (double)valores.Watts;
        HorasPorDia = (double)valores.HorasPorDia;
        DiasPorSemana = valores.DiasPorSemana;
        CicloDeTrabajo = (double)valores.CicloDeTrabajo;
        _cargando = false;
        EnEdicion = true;
        Validar();
    }

    [RelayCommand]
    private void Cancelar()
    {
        _validacion?.Cancel();
        EnEdicion = false;
        PlantillaElegida = null;
    }

    private async void ProgramarValidacion()
    {
        if (_cargando || !EnEdicion)
        {
            return;
        }

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
        Validar();
    }

    // Un campo vacío (NaN) se toma como 0 para que el validador diga qué falta.
    private static decimal Numero(double valor) => double.IsNaN(valor) ? 0m : (decimal)valor;

    private static int Entero(double valor) => double.IsNaN(valor) ? 0 : (int)Math.Round(valor);

    private AparatoEditable Editable() => new(
        _id,
        _lugarId ?? 0,
        Nombre,
        CategoriaElegida?.Categoria ?? CategoriaAparato.Otro,
        Entero(Cantidad),
        Numero(Watts),
        Numero(HorasPorDia),
        Entero(DiasPorSemana),
        Numero(CicloDeTrabajo));

    private void Validar()
    {
        var editable = Editable();
        var errores = AparatoValidador.Validar(editable);
        Errores.Clear();
        foreach (var error in errores)
        {
            Errores.Add(textos[$"Aparato_Error_{error}"]);
        }

        TieneErrores = Errores.Count > 0;
        Valida = !TieneErrores;
        TieneVistaPrevia = Valida;
        VistaPreviaTexto = Valida ? textos.Formato("Aparatos_KwhPrevia", ConsumoAparato.KwhPorMes(editable)) : string.Empty;
    }

    private bool PuedeGuardar() => Valida && !Guardando;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        Validar();
        if (!Valida || _lugarId is null)
        {
            return;
        }

        Guardando = true;
        try
        {
            var resultado = await aparatos.GuardarAsync(Editable());
            if (resultado.Errores.Count > 0)
            {
                Errores.Clear();
                foreach (var error in resultado.Errores)
                {
                    Errores.Add(textos[$"Aparato_Error_{error}"]);
                }

                TieneErrores = true;
                return;
            }

            EnEdicion = false;
            PlantillaElegida = null;
            await RecargarListaAsync();
            MostrarAviso(textos.Formato("Aparatos_Guardado", Nombre.Trim()));
        }
        finally
        {
            Guardando = false;
        }
    }

    private async Task EliminarAsync(int id, string nombre)
    {
        var confirmado = await dialogos.ConfirmarAsync(
            textos["Aparatos_EliminarTitulo"],
            textos.Formato("Aparatos_EliminarTexto", nombre),
            textos["Eliminar_Confirmar"],
            textos["Eliminar_Cancelar"]);
        if (!confirmado)
        {
            return;
        }

        await aparatos.EliminarAsync(id);
        if (_id == id)
        {
            Cancelar();
        }

        await RecargarListaAsync();
        MostrarAviso(textos.Formato("Aparatos_Eliminado", nombre));
    }

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    /// <summary>
    /// Aviso breve (la vista lo anuncia al lector de pantalla); se borra solo.
    /// </summary>
    private async void MostrarAviso(string texto)
    {
        _aviso?.Cancel();
        var cancelacion = _aviso = new CancellationTokenSource();
        Aviso = texto;
        try
        {
            await Task.Delay(DuracionAviso, cancelacion.Token);
            Aviso = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Llegó otro aviso.
        }
    }
}
