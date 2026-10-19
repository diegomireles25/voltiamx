using CommunityToolkit.Mvvm.ComponentModel;
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
    /// "Nueva lectura" se ve deshabilitado mientras el módulo Lecturas no exista.
    /// </summary>
    public bool NuevaLecturaHabilitada => false;

    public async Task IniciarAsync()
    {
        WeakReferenceMessenger.Default.Register<ShellViewModel, ModulosCambiadosMensaje>(
            this, static (receptor, mensaje) => _ = receptor.CargarElementosAsync(DestinoShell.Configuracion));

        await CargarElementosAsync(DestinoShell.Dashboard);
    }

    partial void OnSeleccionadoChanged(object? value)
    {
        if (value is ElementoNavegacion elemento)
        {
            navegacion.Navegar(elemento);
        }
    }

    private async Task CargarElementosAsync(DestinoShell seleccionar)
    {
        var modulos = await perfiles.ObtenerModulosActivosAsync();

        List<ElementoNavegacion> elementos = [new(DestinoShell.Dashboard, textos["Nav_Dashboard"])];
        elementos.AddRange(modulos.Select(m => new ElementoNavegacion(DestinoShell.Modulo, textos[$"Modulo_{m}"], m)));
        elementos.Add(new ElementoNavegacion(DestinoShell.Configuracion, textos["Nav_Configuracion"]));

        Seleccionado = null;
        Elementos = elementos;
        Seleccionado = elementos.First(e => e.Destino == seleccionar);
    }
}
