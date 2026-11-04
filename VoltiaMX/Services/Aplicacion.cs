namespace VoltiaMX.Services;

/// <summary>
/// Lo que la restauración necesita de la app: pausar las tareas que leen la base y cerrar la aplicación.
/// </summary>
public interface IAplicacion
{
    /// <summary>
    /// Detiene la evaluación de avisos para que nada abra la base durante el reemplazo.
    /// </summary>
    Task PausarTareasAsync();

    void ReanudarTareas();

    /// <summary>
    /// Cierra VoltiaMX (después de restaurar, para volver a abrirla con los datos nuevos).
    /// </summary>
    void Cerrar();
}

public sealed class Aplicacion(ProgramadorAvisos avisos) : IAplicacion
{
    public Task PausarTareasAsync() => avisos.PausarAsync();

    public void ReanudarTareas() => avisos.Reanudar();

    public void Cerrar() => Application.Current.Exit();
}
