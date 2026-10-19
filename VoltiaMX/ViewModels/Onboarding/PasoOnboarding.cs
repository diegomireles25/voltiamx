namespace VoltiaMX.ViewModels.Onboarding;

/// <summary>
/// Pasos del onboarding en orden. Los cinco del medio son las preguntas numeradas.
/// </summary>
public enum PasoOnboarding
{
    Bienvenida,
    Uso,
    Ubicacion,
    Recibo,
    Objetivo,
    Presupuesto,
    Listo
}

/// <summary>
/// Tarifa elegible en el paso de ubicación.
/// </summary>
public sealed record OpcionTarifa(int Id, string Nombre)
{
    public override string ToString() => Nombre;
}

/// <summary>
/// Renglón del resumen de ListoPage.
/// </summary>
public sealed record FilaResumen(string Etiqueta, string Valor);
