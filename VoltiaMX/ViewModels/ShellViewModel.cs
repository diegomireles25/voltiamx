using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

public enum DestinoShell
{
    Dashboard,
    Modulo,

    /// <summary>
    /// Página Más: los módulos que no son principales, los ajustes y sus secciones (Tarifas, Reportes, Respaldo).
    /// </summary>
    Mas
}

/// <summary>
/// Destino del Shell. El menú lleva Inicio, hasta dos módulos principales y Más; los demás módulos son destinos
/// que se abren desde Más (y desde enlaces) sin tener pestaña.
/// </summary>
public sealed record ElementoNavegacion(DestinoShell Destino, string Titulo, ModuloApp? Modulo = null);

public sealed partial class ShellViewModel(IPerfilService perfiles, ITextos textos, INavegacionShell navegacion)
    : ObservableObject
{
    /// <summary>
    /// Se reemplaza completa al cambiar de modo: vaciar y rellenar la misma colección hacía que el
    /// NavigationView reusara contenedores con el estado "seleccionado" de otro elemento.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<ElementoNavegacion> Elementos { get; set; } = [];

    /// <summary>
    /// Elemento seleccionado del NavigationView (object porque así lo expone el control).
    /// </summary>
    [ObservableProperty]
    public partial object? Seleccionado { get; set; }

    /// <summary>
    /// "Nueva lectura" funciona cuando el módulo Lecturas está activo en el perfil.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NuevaLecturaCommand))]
    public partial bool NuevaLecturaHabilitada { get; set; }

    // Sección pedida por IrAModuloMensaje; se entrega al navegar al elemento seleccionado.
    private string? _seccionPendiente;

    // Módulos sin pestaña (van en Más): se abren sin marcar ninguna pestaña.
    private IReadOnlyList<ElementoNavegacion> _sinPestana = [];

    // Mientras se desmarca el menú para abrir un módulo sin pestaña, no se navega.
    private bool _soloMarcar;

    // La pestaña marcada no muestra su página principal, sino una sección (p. ej. Tarifas bajo Más).
    private bool _fueraDeLaPagina;

    /// <summary>
    /// Tocar la pestaña que ya está marcada vuelve a su página si lo que se ve es una de sus secciones. Si la
    /// pestaña cambia, la navegación sale de la selección.
    /// </summary>
    public void AlTocarElemento(ElementoNavegacion elemento)
    {
        if (_fueraDeLaPagina && Equals(Seleccionado, elemento))
        {
            _fueraDeLaPagina = false;
            navegacion.Navegar(elemento, null);
        }
    }

    public async Task IniciarAsync()
    {
        WeakReferenceMessenger.Default.Register<ShellViewModel, ModulosCambiadosMensaje>(
            this, static (receptor, mensaje) => _ = receptor.CargarElementosAsync(DestinoShell.Mas));
        WeakReferenceMessenger.Default.Register<ShellViewModel, IrAModuloMensaje>(
            this, static (receptor, mensaje) => receptor.IrA(e => e.Modulo == mensaje.Modulo, mensaje.Seccion));
        WeakReferenceMessenger.Default.Register<ShellViewModel, IrAConfiguracionMensaje>(
            this, static (receptor, mensaje) => receptor.IrA(e => e.Destino == DestinoShell.Mas, mensaje.Seccion));
        WeakReferenceMessenger.Default.Register<ShellViewModel, IrADashboardMensaje>(
            this, static (receptor, _) => receptor.IrA(e => e.Destino == DestinoShell.Dashboard, null));

        await CargarElementosAsync(DestinoShell.Dashboard);
        WeakReferenceMessenger.Default.Send(new ShellListoMensaje());
    }

    [RelayCommand(CanExecute = nameof(NuevaLecturaHabilitada))]
    private void NuevaLectura() => IrA(e => e.Modulo == ModuloApp.Lecturas, null);

    partial void OnSeleccionadoChanged(object? value)
    {
        if (!_soloMarcar && value is ElementoNavegacion elemento)
        {
            _fueraDeLaPagina = _seccionPendiente is not null;
            navegacion.Navegar(elemento, _seccionPendiente);
            _seccionPendiente = null;
        }
    }

    /// <summary>
    /// Selecciona el elemento del menú (la navegación sale de <see cref="OnSeleccionadoChanged"/>);
    /// si ya estaba seleccionado, vuelve a abrir su página.
    /// </summary>
    private void IrA(Func<ElementoNavegacion, bool> criterio, string? seccion)
    {
        if (Elementos.FirstOrDefault(criterio) is not { } elemento)
        {
            // Un módulo sin pestaña se abre directamente y el menú queda sin marcar: así, tocar "Más" (o
            // cualquier pestaña) cambia la selección y abre su página.
            if (_sinPestana.FirstOrDefault(criterio) is { } oculto)
            {
                _soloMarcar = true;
                Seleccionado = null;
                _soloMarcar = false;
                _fueraDeLaPagina = false;
                navegacion.Navegar(oculto, seccion);
            }

            return;
        }

        if (Equals(Seleccionado, elemento))
        {
            _fueraDeLaPagina = seccion is not null;
            navegacion.Navegar(elemento, seccion);
            return;
        }

        _seccionPendiente = seccion;
        Seleccionado = elemento;
    }

    private async Task CargarElementosAsync(DestinoShell seleccionar)
    {
        var modulos = await perfiles.ObtenerModulosActivosAsync();
        var usos = (await perfiles.ObtenerPerfilAsync())?.Usos.Select(u => u.Uso).ToList() ?? [];
        var menu = ModulosPrincipales.Calcular(modulos, usos);

        // Inicio, hasta dos módulos principales y Más.
        List<ElementoNavegacion> elementos = [new(DestinoShell.Dashboard, textos["Nav_Dashboard"])];
        elementos.AddRange(menu.Principales.Select(m => new ElementoNavegacion(DestinoShell.Modulo, textos[$"Modulo_{m}"], m)));
        elementos.Add(new ElementoNavegacion(DestinoShell.Mas, textos["Nav_Mas"]));
        _sinPestana = menu.Secundarios.Select(m => new ElementoNavegacion(DestinoShell.Modulo, textos[$"Modulo_{m}"], m)).ToList();

        NuevaLecturaHabilitada = modulos.Contains(ModuloApp.Lecturas);
        Seleccionado = null;
        Elementos = elementos;
        Seleccionado = elementos.First(e => e.Destino == seleccionar);
    }
}
