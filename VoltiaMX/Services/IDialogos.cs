namespace VoltiaMX.Services;

/// <summary>
/// Diálogos de confirmación y aviso. Los ViewModels piden el diálogo sin conocer los controles.
/// </summary>
public interface IDialogos
{
    /// <summary>
    /// Devuelve true si la persona elige <paramref name="confirmar"/>. El botón por defecto es cancelar.
    /// </summary>
    Task<bool> ConfirmarAsync(string titulo, string mensaje, string confirmar, string cancelar);

    Task AvisarAsync(string titulo, string mensaje, string cerrar);
}
