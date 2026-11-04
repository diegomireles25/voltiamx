using VoltiaMX.Core.Models;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Services;

/// <summary>
/// Relaciona cada elemento del menú del Shell con su página. ShellWindow le entrega su Frame.
/// </summary>
public sealed class NavegacionShell : INavegacionShell
{
    public Frame? Marco { get; set; }

    public void Navegar(ElementoNavegacion elemento, string? seccion = null)
    {
        switch (elemento.Destino)
        {
            case DestinoShell.Dashboard:
                IrA(typeof(DashboardPage));
                break;
            case DestinoShell.Configuracion:
                IrA(seccion switch
                {
                    SeccionesConfiguracion.Tarifas => typeof(TarifasPage),
                    SeccionesConfiguracion.Reportes => typeof(ReportesPage),
                    SeccionesConfiguracion.Respaldo => typeof(RespaldoPage),
                    _ => typeof(ConfiguracionPage),
                });
                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Lecturas:
                IrA(seccion == SeccionesLecturas.Escanear ? typeof(EscanearReciboPage) : typeof(LecturasPage), seccion);
                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Presupuesto:
                IrA(typeof(PresupuestoPage));
                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Predictor:
                IrA(typeof(PredictorPage));
                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Reportes:
                IrA(typeof(ReportesPage));
                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Inquilinos:
                if (SeccionesInquilinos.EsReparto(seccion))
                {
                    IrA(typeof(RepartoPage), SeccionesInquilinos.RepartoDe(seccion));
                }
                else
                {
                    IrA(typeof(InquilinosPage));
                }

                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Solar:
                IrA(typeof(SolarPage));
                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Metas:
                IrA(typeof(MetasPage));
                break;
            case DestinoShell.Modulo when elemento.Modulo == ModuloApp.Aparatos:
                if (SeccionesAparatos.EsSimulador(seccion))
                {
                    IrA(typeof(SimuladorPage), SeccionesAparatos.AparatoDe(seccion));
                }
                else
                {
                    IrA(typeof(AparatosPage));
                }

                break;
            default:
                // Los módulos llegan en fases posteriores; mientras, muestran su estado vacío.
                IrA(typeof(ModuloPendientePage), elemento.Titulo);
                break;
        }
    }

    public void AbrirGaleriaDeDiseno() => IrA(typeof(DesignGalleryPage));

    public void AbrirTarifas() => IrA(typeof(TarifasPage));

    // Sin limpiar el historial: "Volver" regresa a Tarifas.
    public void AbrirEditorDeTarifa(int? tarifaId) => Marco?.Navigate(typeof(TarifaEditorPage), tarifaId);

    public void Volver()
    {
        if (Marco?.CanGoBack == true)
        {
            Marco.GoBack();
        }
        else
        {
            AbrirTarifas();
        }
    }

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
