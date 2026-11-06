using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Un aparato típico en "Elegir mis aparatos": "Lo tengo", las horas al día y, si suele haber varios, cuántos.
/// Los nombres accesibles incluyen el aparato ("Aire acondicionado, lo tengo", "Horas al día de Aire acondicionado").
/// </summary>
public sealed partial class FilaAparatoTipico : ObservableObject
{
    public FilaAparatoTipico(AparatoTipico tipico, ITextos textos)
    {
        Tipico = tipico;
        Nombre = tipico.Nombre;
        PreguntarCantidad = tipico.Plantilla.SuelenSerVarios;
        NombreCasilla = textos.Formato("ElegirAparatos_LoTengo", Nombre);
        NombreHoras = textos.Formato("ElegirAparatos_HorasDe", Nombre);
        NombreCantidad = textos.Formato("ElegirAparatos_CuantosDe", Nombre);

        // Un aparato que ya existe aparece marcado con sus horas y su cantidad.
        Marcado = tipico.Existente is not null;
        Horas = (double)(tipico.Existente?.HorasPorDia ?? tipico.Plantilla.HorasPorDia);
        Cantidad = tipico.Existente?.Cantidad ?? 1;
    }

    public AparatoTipico Tipico { get; }

    public string Nombre { get; }

    public bool PreguntarCantidad { get; }

    public string NombreCasilla { get; }

    public string NombreHoras { get; }

    public string NombreCantidad { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstaMarcado), nameof(MostrarCantidad))]
    public partial bool? Marcado { get; set; }

    public bool EstaMarcado => Marcado == true;

    public bool MostrarCantidad => EstaMarcado && PreguntarCantidad;

    [ObservableProperty] public partial double Horas { get; set; }

    [ObservableProperty] public partial double Cantidad { get; set; }

    /// <summary>
    /// Ya existía y la persona lo desmarcó: al guardar se elimina, previa confirmación.
    /// </summary>
    public bool Desmarcado => Tipico.Existente is not null && !EstaMarcado;
}

/// <summary>
/// "Elegir mis aparatos" (Fase 9d, D1): la lista de aparatos típicos para marcar los que hay en casa y contestar
/// cuántas horas al día se usan. Nada de potencia, días ni ciclo: salen de la plantilla. Se guarda con
/// <see cref="IAparatoService.GuardarElegidosAsync"/> y vuelve a la pantalla de origen.
/// </summary>
public sealed partial class ElegirAparatosViewModel(
    IAparatoService aparatos,
    ILecturaService lecturas,
    ITextos textos,
    IDialogos dialogos) : ObservableObject
{
    // Nombres que tuvieron antes algunas plantillas, para reconocer los aparatos guardados con ellos (E1).
    private static readonly Dictionary<string, string[]> ClavesAnteriores = new()
    {
        ["Televisor"] = ["Plantilla_Televisor_Anterior"],
        ["IluminacionLed"] = ["Plantilla_IluminacionLed_Anterior"],
    };

    private int? _lugarId;
    private string? _seccion;

    public ObservableCollection<FilaAparatoTipico> Filas { get; } = [];

    [ObservableProperty] public partial bool TieneLugar { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool HayCambios { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool Guardando { get; set; }

    [ObservableProperty] public partial string Error { get; set; } = string.Empty;

    /// <summary>
    /// <paramref name="seccion"/> dice de dónde se abrió (<see cref="SeccionesAparatos.ElegirDesdeInicio"/> o
    /// <see cref="SeccionesAparatos.ElegirDesdeAparatos"/>).
    /// </summary>
    public async Task CargarAsync(string? seccion)
    {
        _seccion = seccion;
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;

        var guardados = _lugarId is int id ? await aparatos.ListarAsync(id) : [];
        var plantillas = aparatos.PlantillasDeReferencia().Select(p => new PlantillaConNombre(
            p,
            textos[$"Plantilla_{p.Clave}"],
            ClavesAnteriores.GetValueOrDefault(p.Clave, []).Select(c => textos[c]).ToList()));

        foreach (var fila in Filas)
        {
            fila.PropertyChanged -= AlCambiarFila;
        }

        Filas.Clear();
        foreach (var tipico in AparatosTipicos.Emparejar(plantillas, guardados))
        {
            var fila = new FilaAparatoTipico(tipico, textos);
            fila.PropertyChanged += AlCambiarFila;
            Filas.Add(fila);
        }

        ActualizarCambios();
    }

    private void AlCambiarFila(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilaAparatoTipico.Marcado))
        {
            ActualizarCambios();
        }
    }

    // "Guardar mis aparatos" se habilita con algo marcado (o con algo desmarcado que haya que quitar).
    private void ActualizarCambios() => HayCambios = Filas.Any(f => f.EstaMarcado || f.Desmarcado);

    private bool PuedeGuardar() => HayCambios && !Guardando && TieneLugar;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        if (_lugarId is not int lugarId)
        {
            return;
        }

        Error = string.Empty;
        var quitar = Filas.Where(f => f.Desmarcado).ToList();
        if (quitar.Count > 0 && !await dialogos.ConfirmarAsync(
                textos["ElegirAparatos_QuitarTitulo"],
                textos.Formato("ElegirAparatos_QuitarTexto", string.Join(", ", quitar.Select(f => f.Nombre))),
                textos["ElegirAparatos_QuitarConfirmar"],
                textos["Aparatos_Cancelar/Content"]))
        {
            return;
        }

        var elegidos = Filas.Where(f => f.EstaMarcado)
            .Select(f => new AparatoElegido(
                f.Tipico.Plantilla,
                f.Nombre,
                double.IsNaN(f.Horas) ? 0m : (decimal)f.Horas,
                f.PreguntarCantidad && !double.IsNaN(f.Cantidad) ? (int)Math.Round(f.Cantidad) : f.Tipico.Existente?.Cantidad ?? 1,
                f.Tipico.Existente?.Id))
            .ToList();

        Guardando = true;
        ResultadoGuardarElegidos resultado;
        try
        {
            resultado = await aparatos.GuardarElegidosAsync(lugarId, elegidos, quitar.Select(f => f.Tipico.Existente!.Id).ToList());
        }
        finally
        {
            Guardando = false;
        }

        if (!resultado.Guardado)
        {
            // El primer error, con el aparato: "Ventilador: …".
            var (clave, errores) = resultado.Errores.First();
            var nombre = Filas.First(f => f.Tipico.Plantilla.Clave == clave).Nombre;
            Error = textos.Formato("ElegirAparatos_Error", nombre, textos[$"Aparato_Error_{errores[0]}"]);
            return;
        }

        Volver();
    }

    [RelayCommand]
    private void Cancelar() => Volver();

    private void Volver()
    {
        if (SeccionesAparatos.ElegirVuelveAInicio(_seccion))
        {
            WeakReferenceMessenger.Default.Send(new IrADashboardMensaje());
            return;
        }

        WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Aparatos));
    }
}
