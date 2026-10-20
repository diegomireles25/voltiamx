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
    Configuracion
}

/// <summary>
/// Elemento del menú del Shell. Los módulos salen del perfil; Dashboard y Configuración siempre están.
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

    public async Task IniciarAsync()
    {
        WeakReferenceMessenger.Default.Register<ShellViewModel, ModulosCambiadosMensaje>(
            this, static (receptor, mensaje) => _ = receptor.CargarElementosAsync(DestinoShell.Configuracion));
        WeakReferenceMessenger.Default.Register<ShellViewModel, IrAModuloMensaje>(
            this, static (receptor, mensaje) => receptor.IrA(e => e.Modulo == mensaje.Modulo, mensaje.Seccion));
        WeakReferenceMessenger.Default.Register<ShellViewModel, IrAConfiguracionMensaje>(
            this, static (receptor, mensaje) => receptor.IrA(e => e.Destino == DestinoShell.Configuracion, null));

        await CargarElementosAsync(DestinoShell.Dashboard);
    }

    [RelayCommand(CanExecute = nameof(NuevaLecturaHabilitada))]
    private void NuevaLectura() => IrA(e => e.Modulo == ModuloApp.Lecturas, null);

    partial void OnSeleccionadoChanged(object? value)
    {
        if (value is ElementoNavegacion elemento)
        {
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
            return;
        }

        if (Equals(Seleccionado, elemento))
        {
            navegacion.Navegar(elemento, seccion);
            return;
        }

        _seccionPendiente = seccion;
        Seleccionado = elemento;
    }

    private async Task CargarElementosAsync(DestinoShell seleccionar)
    {
        var modulos = await perfiles.ObtenerModulosActivosAsync();

        List<ElementoNavegacion> elementos = [new(DestinoShell.Dashboard, textos["Nav_Dashboard"])];
        elementos.AddRange(modulos.Select(m => new ElementoNavegacion(DestinoShell.Modulo, textos[$"Modulo_{m}"], m)));
        elementos.Add(new ElementoNavegacion(DestinoShell.Configuracion, textos["Nav_Configuracion"]));

        NuevaLecturaHabilitada = modulos.Contains(ModuloApp.Lecturas);
        Seleccionado = null;
        Elementos = elementos;
        Seleccionado = elementos.First(e => e.Destino == seleccionar);
    }
}
