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
        switch (elemento.Destino)
        {
            case DestinoShell.Dashboard:
                IrA(typeof(DashboardPage));
                break;
            case DestinoShell.Configuracion:
                IrA(typeof(ConfiguracionPage));
                break;
            default:
                // Los módulos llegan en fases posteriores; mientras, muestran su estado vacío.
                IrA(typeof(ModuloPendientePage), elemento.Titulo);
                break;
        }
    }

    public void AbrirGaleriaDeDiseno() => IrA(typeof(DesignGalleryPage));

    private void IrA(Type pagina, object? parametro = null)
    {
        if (Marco is null)
        {
            return;
        }

        Marco.Navigate(pagina, parametro);
        Marco.BackStack.Clear();
    }
}
