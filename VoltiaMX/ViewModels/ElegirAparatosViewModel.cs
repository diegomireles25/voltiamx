using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Una unidad de un aparato típico que se usa distinto (Fase 9g): su nombre opcional ("Sala") y sus horas al día.
/// Los nombres accesibles incluyen el aparato y el número de la unidad.
/// </summary>
public sealed partial class FilaUnidad : ObservableObject
{
    public FilaUnidad(string aparato, int numero, int? id, string apodo, double horas, ITextos textos)
    {
        Id = id;
        Apodo = apodo;
        Horas = horas;
        NombreApodo = textos.Formato("ElegirAparatos_ApodoDe", aparato, numero);
        NombreHoras = textos.Formato("ElegirAparatos_HorasDeUnidad", aparato, numero);
    }

    /// <summary>
    /// El aparato guardado que corresponde a esta unidad (se actualiza) o null (es nueva).
    /// </summary>
    public int? Id { get; }

    public string NombreApodo { get; }

    public string NombreHoras { get; }

    [ObservableProperty] public partial string Apodo { get; set; }

    [ObservableProperty] public partial double Horas { get; set; }
}

/// <summary>
/// Un aparato típico en "Elegir mis aparatos": "Lo tengo", cuántos tiene y sus horas al día. Fase 9g: con más de uno,
/// "¿Los usas las mismas horas?"; con "No, cada uno distinto", una fila por unidad con su nombre y sus horas.
/// Los nombres accesibles incluyen el aparato ("Aire acondicionado, lo tengo", "Horas al día de Aire acondicionado").
/// </summary>
public sealed partial class FilaAparatoTipico : ObservableObject
{
    private readonly ITextos _textos;

    public FilaAparatoTipico(AparatoTipico tipico, ITextos textos)
    {
        _textos = textos;
        Tipico = tipico;
        Nombre = tipico.Nombre;
        NombreCasilla = textos.Formato("ElegirAparatos_LoTengo", Nombre);
        NombreHoras = textos.Formato("ElegirAparatos_HorasDe", Nombre);
        NombreCantidad = textos.Formato("ElegirAparatos_CuantosDe", Nombre);
        NombreMismas = textos.Formato("ElegirAparatos_MismasDe", Nombre);
        NombreDistintas = textos.Formato("ElegirAparatos_DistintasDe", Nombre);
        GrupoHoras = "Horas_" + tipico.Plantilla.Clave;

        // Lo que ya está guardado: un aparato con su cantidad (mismas horas) o una unidad por aparato.
        var registros = tipico.Registros;
        Marcado = registros.Count > 0;
        Horas = (double)(tipico.Existente?.HorasPorDia ?? tipico.Plantilla.HorasPorDia);
        if (registros.Count > 1)
        {
            for (var i = 0; i < registros.Count; i++)
            {
                var apodo = AparatosTipicos.ApodoDe(registros[i].Nombre, Nombre);
                // "· 2" sin apodo: se muestra vacío para que la persona escriba uno si quiere.
                if (apodo == (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture))
                {
                    apodo = string.Empty;
                }

                Unidades.Add(new FilaUnidad(Nombre, i + 1, registros[i].Id, apodo, (double)registros[i].HorasPorDia, textos));
            }

            _cargando = true;
            Cantidad = registros.Count;
            CadaUnoDistinto = true;
            MismasHoras = false;
            _cargando = false;
        }
        else
        {
            _cargando = true;
            Cantidad = tipico.Existente?.Cantidad ?? 1;
            MismasHoras = true;
            CadaUnoDistinto = false;
            _cargando = false;
        }
    }

    private bool _cargando;

    public AparatoTipico Tipico { get; }

    public string Nombre { get; }

    public string NombreCasilla { get; }

    public string NombreHoras { get; }

    public string NombreCantidad { get; }

    public string NombreMismas { get; }

    public string NombreDistintas { get; }

    /// <summary>
    /// El grupo de "Sí" y "No, cada uno distinto" de esta fila (uno por aparato).
    /// </summary>
    public string GrupoHoras { get; }

    public ObservableCollection<FilaUnidad> Unidades { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EstaMarcado), nameof(PreguntarMismas), nameof(MostrarHorasUnicas), nameof(MostrarUnidades))]
    public partial bool? Marcado { get; set; }

    public bool EstaMarcado => Marcado == true;

    [ObservableProperty] public partial double Horas { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreguntarMismas), nameof(MostrarHorasUnicas), nameof(MostrarUnidades))]
    public partial double Cantidad { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarHorasUnicas), nameof(MostrarUnidades))]
    public partial bool? MismasHoras { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarHorasUnicas), nameof(MostrarUnidades))]
    public partial bool? CadaUnoDistinto { get; set; }

    /// <summary>
    /// Cuántos tiene (al menos 1; un campo vacío cuenta como 1).
    /// </summary>
    public int CantidadEntera => double.IsNaN(Cantidad) || Cantidad < 1 ? 1 : (int)Math.Round(Cantidad);

    public bool PreguntarMismas => EstaMarcado && CantidadEntera > 1;

    public bool UsaUnidades => CantidadEntera > 1 && CadaUnoDistinto == true;

    public bool MostrarHorasUnicas => EstaMarcado && !UsaUnidades;

    public bool MostrarUnidades => EstaMarcado && UsaUnidades;

    /// <summary>
    /// Ya existía y la persona lo desmarcó: al guardar se elimina, previa confirmación.
    /// </summary>
    public bool Desmarcado => Tipico.Registros.Count > 0 && !EstaMarcado;

    partial void OnCantidadChanged(double value) => AjustarUnidades();

    partial void OnCadaUnoDistintoChanged(bool? value)
    {
        if (value == true)
        {
            MismasHoras = false;
            AjustarUnidades();
        }
    }

    partial void OnMismasHorasChanged(bool? value)
    {
        if (value != true)
        {
            return;
        }

        CadaUnoDistinto = false;

        // Precisión (1): al volver a "mismas horas", las horas del primero.
        if (!_cargando && Unidades.Count > 0 && !double.IsNaN(Unidades[0].Horas))
        {
            Horas = Unidades[0].Horas;
        }
    }

    // Cambiar la cantidad agrega o quita filas de unidades; las nuevas empiezan con las horas del aparato.
    private void AjustarUnidades()
    {
        if (_cargando || CadaUnoDistinto != true)
        {
            return;
        }

        var objetivo = CantidadEntera;
        while (Unidades.Count > objetivo)
        {
            Unidades.RemoveAt(Unidades.Count - 1);
        }

        while (Unidades.Count < objetivo)
        {
            var numero = Unidades.Count + 1;
            var id = Tipico.Registros.Count >= numero ? Tipico.Registros[numero - 1].Id : (int?)null;
            Unidades.Add(new FilaUnidad(Nombre, numero, id, string.Empty, Horas, _textos));
        }
    }

    /// <summary>
    /// Lo que se manda a guardar: un aparato con su cantidad (mismas horas) o una unidad por aparato.
    /// </summary>
    public AparatoElegido Elegido()
    {
        var registros = Tipico.Registros;
        var horas = double.IsNaN(Horas) ? 0m : (decimal)Horas;
        if (!UsaUnidades)
        {
            return new AparatoElegido(
                Tipico.Plantilla, Nombre, horas, CantidadEntera, registros.FirstOrDefault()?.Id, registros.Skip(1).Select(r => r.Id).ToList());
        }

        return new AparatoElegido(
            Tipico.Plantilla,
            Nombre,
            horas,
            CantidadEntera,
            null,
            registros.Select(r => r.Id).ToList(),
            Unidades.Select(u => new UnidadElegida(u.Id, u.Apodo ?? string.Empty, double.IsNaN(u.Horas) ? 0m : (decimal)u.Horas)).ToList());
    }
}

/// <summary>
/// "Elegir mis aparatos" (Fase 9d, D1; Fase 9g): la lista de aparatos típicos para marcar los que hay en casa,
/// cuántos son y cuántas horas al día se usan (todos iguales o cada uno distinto). Nada de potencia, días ni ciclo:
/// salen de la plantilla. Se guarda con <see cref="IAparatoService.GuardarElegidosAsync"/> y vuelve a la pantalla de
/// origen. Los cambios de forma y lo que se quita se confirman una sola vez.
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
        var marcadas = Filas.Where(f => f.EstaMarcado).ToList();
        var elegidos = marcadas.Select(f => f.Elegido()).ToList();

        // Una sola confirmación con todo lo que se reemplaza o se quita (Fase 9g, D2).
        var avisos = new List<string>();
        for (var i = 0; i < marcadas.Count; i++)
        {
            if (AparatosTipicos.Cambios(elegidos[i], marcadas[i].Tipico.Registros) is { } cambio)
            {
                avisos.Add(TextoCambio(cambio));
            }
        }

        if (quitar.Count > 0)
        {
            avisos.Add(textos.Formato("ElegirAparatos_QuitarTexto", string.Join(", ", quitar.Select(f => f.Nombre))));
        }

        if (avisos.Count > 0 && !await dialogos.ConfirmarAsync(
                textos["ElegirAparatos_CambiosTitulo"],
                string.Join("\n\n", avisos),
                textos["ElegirAparatos_CambiosConfirmar"],
                textos["Aparatos_Cancelar/Content"]))
        {
            return;
        }

        Guardando = true;
        ResultadoGuardarElegidos resultado;
        try
        {
            resultado = await aparatos.GuardarElegidosAsync(
                lugarId, elegidos, quitar.SelectMany(f => f.Tipico.Registros.Select(r => r.Id)).ToList());
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

    // Lo que cambia, en palabras sencillas (precisión 1 de la Fase 9g).
    private string TextoCambio(CambioAparato cambio) => cambio.Tipo switch
    {
        TipoCambioAparato.AUnoConMismasHoras => textos.Formato(
            "ElegirAparatos_CambioAUno", cambio.RegistrosAntes, cambio.Nombre, cambio.Cantidad, cambio.HorasPorDia),
        TipoCambioAparato.AUnidades => textos.Formato("ElegirAparatos_CambioAUnidades", cambio.Nombre, cambio.RegistrosDespues),
        _ => textos.Formato("ElegirAparatos_QuitarTexto", string.Join(", ", cambio.Quitadas)),
    };

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
