using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.ViewModels;

namespace VoltiaMX.Services;

/// <summary>
/// Evalúa los avisos y los envía como notificaciones mientras la app está abierta: al iniciar, cada hora,
/// cuando se carga el Dashboard y después de guardar lecturas o cambiar tarifas (EvaluarAvisosMensaje).
/// No hay servicio en segundo plano.
/// </summary>
public sealed class ProgramadorAvisos(IAvisoService avisos, INotificador notificador, IPerfilService perfiles)
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(60);

    private readonly SemaphoreSlim _uno = new(1, 1);
    private DispatcherQueue? _cola;
    private DispatcherQueueTimer? _reloj;
    private TipoAviso? _navegacionPendiente;
    private bool _abrirAlEstarListo;

    public bool NotificacionesDisponibles => notificador.EstaDisponible;

    public void Iniciar(DispatcherQueue cola)
    {
        if (_cola is not null)
        {
            return;
        }

        _cola = cola;
        notificador.Registrar();
        notificador.Activado += (remitente, tipo) => _cola.TryEnqueue(() => _ = AbrirAsync(tipo));
        WeakReferenceMessenger.Default.Register<ProgramadorAvisos, EvaluarAvisosMensaje>(this, static (r, mensaje) => _ = r.EvaluarAsync());
        WeakReferenceMessenger.Default.Register<ProgramadorAvisos, ShellListoMensaje>(this, static (r, mensaje) => r.AlEstarListoElShell());

        _reloj = cola.CreateTimer();
        _reloj.Interval = Intervalo;
        _reloj.IsRepeating = true;
        _reloj.Tick += (_, _) => _ = EvaluarAsync();
        _reloj.Start();
        _ = EvaluarAsync();
    }

    /// <summary>
    /// Detiene las evaluaciones y espera la que esté en curso (antes de reemplazar la base).
    /// </summary>
    public async Task PausarAsync()
    {
        _reloj?.Stop();
        await _uno.WaitAsync();
    }

    /// <summary>
    /// Deshace <see cref="PausarAsync"/> si la restauración no se completó.
    /// </summary>
    public void Reanudar()
    {
        _uno.Release();
        _reloj?.Start();
    }

    /// <summary>
    /// La app se abrió desde una notificación: navega cuando el Shell esté listo.
    /// </summary>
    public void AbrirAlIniciar(TipoAviso? tipo)
    {
        _navegacionPendiente = tipo;
        _abrirAlEstarListo = true;
    }

    public async Task EvaluarAsync()
    {
        await _uno.WaitAsync();
        try
        {
            var resultado = await avisos.EvaluarAsync(DateOnly.FromDateTime(DateTime.Today));
            if (resultado.LugarId is not int lugarId)
            {
                return;
            }

            foreach (var aviso in resultado.Pendientes)
            {
                RegistroDepuracion.Escribir($"[Avisos] pendiente: {aviso.Tipo} {aviso.Clave}");

                // Se marca como enviado solo si Windows aceptó la notificación.
                if (notificador.Enviar(aviso))
                {
                    await avisos.MarcarEnviadoAsync(lugarId, aviso, DateTime.Now);
                }
            }
        }
        catch (Exception ex)
        {
            // Un aviso que falla nunca detiene la app; se vuelve a intentar en la siguiente evaluación.
            RegistroDepuracion.Escribir($"[Avisos] no se pudo evaluar: {ex.GetType().Name}");
        }
        finally
        {
            _uno.Release();
        }
    }

    public bool EnviarPrueba() => notificador.EnviarPrueba();

    private void AlEstarListoElShell()
    {
        if (_abrirAlEstarListo)
        {
            _abrirAlEstarListo = false;
            _ = AbrirAsync(_navegacionPendiente);
        }
    }

    /// <summary>
    /// Trae la app al frente y abre la pantalla del aviso; si su módulo no está activo, el Dashboard.
    /// </summary>
    private async Task AbrirAsync(TipoAviso? tipo)
    {
        App.VentanaPrincipal?.Activate();
        ModuloApp? modulo = tipo switch
        {
            TipoAviso.Lectura => ModuloApp.Lecturas,
            TipoAviso.Dac => ModuloApp.Predictor,
            TipoAviso.Meta => ModuloApp.Metas,
            // Notificaciones viejas de presupuesto (ya no se generan): abren Inicio.
            _ => null,
        };

        if (modulo is ModuloApp m && (await perfiles.ObtenerModulosActivosAsync()).Contains(m))
        {
            WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(m));
        }
        else
        {
            WeakReferenceMessenger.Default.Send(new IrADashboardMensaje());
        }
    }
}
