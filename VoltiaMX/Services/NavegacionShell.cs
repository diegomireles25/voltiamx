using VoltiaMX.ViewModels;

namespace VoltiaMX.Services;

/// <summary>
/// Relaciona cada elemento del menú del Shell con su página. ShellWindow le entrega su Frame.
/// </summary>
public sealed class NavegacionShell : INavegacionShell
{
    public Frame? Marco { get; set; }

    public void Navegar(ElementoNavegacion elemento)
    {
        if (Marco is null)
        {
            return;
        }

        // Dashboard y Configuración llegan en la Fase 2c; mientras, usan la página de módulo pendiente.
        Marco.Navigate(typeof(ModuloPendientePage), elemento.Titulo);
        Marco.BackStack.Clear();
    }
}
