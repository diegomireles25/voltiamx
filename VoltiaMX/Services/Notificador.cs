using System.Diagnostics;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Services;

/// <summary>
/// Notificaciones de Windows de los avisos. Si Windows no las soporta en esta instalación, no se envía
/// nada y <see cref="EstaDisponible"/> lo dice; la app sigue igual.
/// </summary>
public interface INotificador
{
    bool EstaDisponible { get; }

    /// <summary>
    /// Se toca una notificación mientras la app está abierta; null si es la de prueba.
    /// </summary>
    event EventHandler<TipoAviso?>? Activado;

    void Registrar();

    /// <summary>
    /// True si Windows aceptó la notificación (solo entonces se marca el aviso como enviado).
    /// </summary>
    bool Enviar(AvisoPendiente aviso);

    bool EnviarPrueba();
}

public sealed class Notificador(ITextos textos) : INotificador
{
    private const string ArgumentoTipo = "tipo";
    private const string TipoPrueba = "prueba";

    private bool _registrado;

    public bool EstaDisponible { get; private set; }

    public event EventHandler<TipoAviso?>? Activado;

    public void Registrar()
    {
        if (_registrado)
        {
            return;
        }

        _registrado = true;
        if (Simulado)
        {
            EstaDisponible = true;
            RegistroDepuracion.Escribir("[Avisos] notificaciones simuladas (solo Debug)");
            return;
        }

        try
        {
            if (!AppNotificationManager.IsSupported())
            {
                RegistroDepuracion.Escribir("[Avisos] Windows no soporta notificaciones en esta instalación");
                return;
            }

            // El manejador va antes de Register: así llegan los toques con la app abierta.
            AppNotificationManager.Default.NotificationInvoked += AlTocar;
            AppNotificationManager.Default.Register();
            EstaDisponible = true;
        }
        catch (Exception ex)
        {
            EstaDisponible = false;
            RegistroDepuracion.Escribir($"[Avisos] no se pudo registrar: {ex.GetType().Name} {ex.HResult:X8}");
        }
    }

    public bool Enviar(AvisoPendiente aviso)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        var (titulo, cuerpo) = Textos(aviso);
        return Mostrar(titulo, cuerpo, aviso.Tipo.ToString(), aviso.Clave);
    }

    public bool EnviarPrueba() => Mostrar(textos["Aviso_Prueba_Titulo"], textos["Aviso_Prueba_Cuerpo"], TipoPrueba, TipoPrueba);

    /// <summary>
    /// El tipo de aviso de los argumentos de una notificación (al tocarla o al abrir la app con ella).
    /// </summary>
    public static TipoAviso? TipoDe(IDictionary<string, string> argumentos)
        => argumentos.TryGetValue(ArgumentoTipo, out var tipo) && Enum.TryParse<TipoAviso>(tipo, out var valor) ? valor : null;

    // Solo estados y fechas: la notificación puede verse en la pantalla de bloqueo (sin pesos ni lecturas).
    private (string Titulo, string Cuerpo) Textos(AvisoPendiente aviso) => aviso.Tipo switch
    {
        TipoAviso.Lectura => (textos["Aviso_Lectura_Titulo"], aviso switch
        {
            { PeriodoTerminado: true } => textos.Formato("Aviso_Lectura_CuerpoTerminado", aviso.FinPeriodo),
            { DiasParaCorte: <= 0 } => textos["Aviso_Lectura_CuerpoHoy"],
            _ => textos.Formato("Aviso_Lectura_Cuerpo", aviso.DiasParaCorte, aviso.FinPeriodo),
        }),
        TipoAviso.Dac => (textos["Aviso_Dac_Titulo"], textos[$"Aviso_Dac_Cuerpo_{aviso.NivelDac}"]),
        TipoAviso.Meta => (textos["Aviso_Meta_Titulo"], textos.Formato($"Aviso_Meta_Cuerpo_{aviso.EstadoMeta}", aviso.FinPeriodo)),
        _ => (textos["Aviso_Presupuesto_Titulo"], textos.Formato("Aviso_Presupuesto_Cuerpo", aviso.FinPeriodo)),
    };

    private bool Mostrar(string titulo, string cuerpo, string tipo, string clave)
    {
        if (!EstaDisponible)
        {
            RegistroDepuracion.Escribir($"[Avisos] no se envía (notificaciones no disponibles): {tipo} {clave}");
            return false;
        }

        if (Simulado)
        {
            RegistroDepuracion.Escribir($"[Avisos] habría enviado: {tipo} {clave}");
            return true;
        }

        try
        {
            var notificacion = new AppNotificationBuilder()
                .AddArgument(ArgumentoTipo, tipo)
                .AddText(titulo)
                .AddText(cuerpo)
                .BuildNotification();
            AppNotificationManager.Default.Show(notificacion);
            RegistroDepuracion.Escribir($"[Avisos] enviado: {tipo} {clave}");
            return notificacion.Id != 0;
        }
        catch (Exception ex)
        {
            RegistroDepuracion.Escribir($"[Avisos] no se pudo enviar {tipo}: {ex.GetType().Name} {ex.HResult:X8}");
            return false;
        }
    }

    private void AlTocar(AppNotificationManager sender, AppNotificationActivatedEventArgs args)
        => Activado?.Invoke(this, TipoDe(args.Arguments));

#if DEBUG
    // Solo para probar sin la versión empaquetada: con VOLTIAMX_AVISOS_SIMULAR=1 "enviar" solo se registra.
    private static bool Simulado => Environment.GetEnvironmentVariable("VOLTIAMX_AVISOS_SIMULAR") == "1";
#else
    private static bool Simulado => false;
#endif
}

/// <summary>
/// Registro de depuración de los avisos: solo tipo, clave y causa de errores, nunca datos de la persona.
/// No existe en Release.
/// </summary>
public static class RegistroDepuracion
{
    [Conditional("DEBUG")]
    public static void Escribir(string texto)
    {
        Debug.WriteLine(texto);

        // Para revisar sin depurador: VOLTIAMX_AVISOS_LOG = ruta de un archivo de texto.
        if (Environment.GetEnvironmentVariable("VOLTIAMX_AVISOS_LOG") is { Length: > 0 } ruta)
        {
            try
            {
                File.AppendAllText(ruta, $"{DateTime.Now:HH:mm:ss} {texto}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // El registro de depuración nunca detiene la app.
            }
        }
    }
}
