using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Ayuda: cómo se calcula el recibo, qué significa cada palabra de la app y lo que VoltiaMX no hace. Las palabras usan
/// los mismos textos que los "¿Qué es esto?" (Ayuda_Termino_{clave} y Ayuda_{clave}).
/// </summary>
public sealed partial class AyudaViewModel(ITextos textos) : ObservableObject
{
    /// <summary>
    /// Términos en el orden en que aparecen en la página.
    /// </summary>
    public static readonly string[] Terminos =
    [
        "Kwh", "LoQueMarcaTuMedidor", "DiaDeCorte", "Recibo", "ReciboAnterior", "AEsteRitmo", "Aproximado",
        "TramosDePrecio", "TarifaAlta", "AntesDeImpuestos", "LoQueMasGasta", "QueParteDelTiempo", "MedidorPropio",
        "Kwp", "HorasDeSol", "Rendimiento", "LoQueCubre", "LoQueSobra",
    ];

    private const int FrasesDelCalculo = 5;
    private const int CosasQueNoHace = 3;

    public IReadOnlyList<string> Calculo { get; } =
        Enumerable.Range(1, FrasesDelCalculo).Select(n => textos[$"Ayuda_Calculo_{n}"]).ToList();

    public IReadOnlyList<FilaDetalle> Palabras { get; } =
        Terminos.Select(t => new FilaDetalle(textos[$"Ayuda_Termino_{t}"], textos[$"Ayuda_{t}"])).ToList();

    public IReadOnlyList<string> NoHace { get; } =
        Enumerable.Range(1, CosasQueNoHace).Select(n => textos[$"Ayuda_NoHace_{n}"]).ToList();

    [RelayCommand]
    private void VolverAMas() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());
}
