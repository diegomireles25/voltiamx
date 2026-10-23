using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;
using VoltiaMX.ViewModels.Onboarding;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Aparato del ComboBox con su consumo actual.
/// </summary>
public sealed record OpcionAparato(Aparato Aparato, string Texto)
{
    public override string ToString() => Texto;
}

/// <summary>
/// Simulador de ahorro sobre un aparato del lugar principal. No guarda nada: cada cambio válido
/// (300 ms después) vuelve a simular con la tarifa, el consumo medido y el historial del lugar.
/// </summary>
public sealed partial class SimuladorViewModel(
    ISimuladorAhorroService simulador,
    IAparatoService aparatos,
    ILecturaService lecturas,
    IResumenDashboardService resumenes,
    ITextos textos) : ObservableObject
{
    private static readonly TimeSpan EsperaValidacion = TimeSpan.FromMilliseconds(300);

    private int? _lugarId;
    private int _mesesPorPeriodo = 2;
    private bool _cargando;
    private CancellationTokenSource? _simulacion;

    [ObservableProperty] public partial bool EsDemo { get; set; }
    [ObservableProperty] public partial bool TieneAparatos { get; set; }
    [ObservableProperty] public partial bool MostrarVacio { get; set; }
    [ObservableProperty] public partial IReadOnlyList<OpcionAparato> Opciones { get; set; } = [];
    [ObservableProperty] public partial OpcionAparato? AparatoElegido { get; set; }

    // ---- Escenario ----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarMenosHoras), nameof(MostrarEficiente))]
    public partial bool? EsMenosHoras { get; set; } = true;

    [ObservableProperty] public partial bool? EsEficiente { get; set; } = false;

    public bool MostrarMenosHoras => EsMenosHoras == true;

    public bool MostrarEficiente => EsMenosHoras != true;

    [ObservableProperty] public partial double HorasPorDia { get; set; } = double.NaN;
    [ObservableProperty] public partial double DiasPorSemana { get; set; } = double.NaN;
    [ObservableProperty] public partial double Watts { get; set; } = double.NaN;
    [ObservableProperty] public partial double CicloDeTrabajo { get; set; } = double.NaN;
    [ObservableProperty] public partial double PrecioEquipo { get; set; } = double.NaN;
    [ObservableProperty] public partial bool TieneErrores { get; set; }

    public ObservableCollection<string> Errores { get; } = [];

    // ---- Resultado ----

    [ObservableProperty] public partial bool TieneResultado { get; set; }
    [ObservableProperty] public partial string CifraPrincipal { get; set; } = string.Empty;
    [ObservableProperty] public partial string LeyendaCifra { get; set; } = string.Empty;
    [ObservableProperty] public partial string AvisoEstado { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneDac { get; set; }
    [ObservableProperty] public partial NivelRiesgoDac DacAntesNivel { get; set; }
    [ObservableProperty] public partial string DacAntesEtiqueta { get; set; } = string.Empty;
    [ObservableProperty] public partial string DacAntesTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial NivelRiesgoDac DacDespuesNivel { get; set; }
    [ObservableProperty] public partial string DacDespuesEtiqueta { get; set; } = string.Empty;
    [ObservableProperty] public partial string DacDespuesTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool MostrarNotaRecuperacion { get; set; }

    public ObservableCollection<FilaDetalle> FilasResultado { get; } = [];

    /// <param name="aparatoId">El aparato con el que se abre (enlace "Simular" de su fila), o null.</param>
    public async Task CargarAsync(int? aparatoId)
    {
        _cargando = true;
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        EsDemo = lugar?.EsDemo == true;

        var lista = _lugarId is int lugarId ? await aparatos.ListarAsync(lugarId) : [];
        Opciones = lista
            .Select(a => new OpcionAparato(a, textos.Formato("Simulador_OpcionAparato", a.Nombre, ConsumoAparato.KwhPorMes(a))))
            .ToList();
        TieneAparatos = Opciones.Count > 0;
        MostrarVacio = !TieneAparatos;

        // Mensual o bimestral, para el texto del ahorro por periodo (el mismo lugar del Dashboard).
        var resumen = await resumenes.ObtenerAsync(DateOnly.FromDateTime(DateTime.Today));
        _mesesPorPeriodo = resumen?.MesesDelPeriodo ?? 2;
        _cargando = false;

        AparatoElegido = Opciones.FirstOrDefault(o => o.Aparato.Id == aparatoId) ?? Opciones.FirstOrDefault();
    }

    partial void OnAparatoElegidoChanged(OpcionAparato? value)
    {
        if (_cargando || value is null)
        {
            return;
        }

        // Los campos empiezan con los valores actuales del aparato: el resultado arranca en 0.
        _cargando = true;
        var aparato = value.Aparato;
        HorasPorDia = (double)aparato.HorasPorDia;
        DiasPorSemana = aparato.DiasPorSemana;
        Watts = (double)aparato.Watts;
        CicloDeTrabajo = (double)aparato.CicloDeTrabajo;
        PrecioEquipo = double.NaN;
        _cargando = false;
        ProgramarSimulacion();
    }

    partial void OnEsMenosHorasChanged(bool? value) => ProgramarSimulacion();

    partial void OnHorasPorDiaChanged(double value) => ProgramarSimulacion();

    partial void OnDiasPorSemanaChanged(double value) => ProgramarSimulacion();

    partial void OnWattsChanged(double value) => ProgramarSimulacion();

    partial void OnCicloDeTrabajoChanged(double value) => ProgramarSimulacion();

    partial void OnPrecioEquipoChanged(double value) => ProgramarSimulacion();

    private async void ProgramarSimulacion()
    {
        if (_cargando)
        {
            return;
        }

        _simulacion?.Cancel();
        var cancelacion = _simulacion = new CancellationTokenSource();
        try
        {
            await Task.Delay(EsperaValidacion, cancelacion.Token);
            await SimularAsync(cancelacion.Token);
        }
        catch (OperationCanceledException)
        {
            // Llegó otro cambio.
        }
    }

    // Un campo vacío (NaN) se toma como 0 para que el validador diga qué falta; el ciclo y el precio
    // son opcionales: vacíos significan "el mismo ciclo" y "sin precio".
    private static decimal Numero(double valor) => double.IsNaN(valor) ? 0m : (decimal)valor;

    private static decimal? Opcional(double valor) => double.IsNaN(valor) ? null : (decimal)valor;

    private EscenarioAhorro Escenario() => EsMenosHoras == true
        ? EscenarioAhorro.MenosHoras(Numero(HorasPorDia), double.IsNaN(DiasPorSemana) ? 0 : (int)Math.Round(DiasPorSemana))
        : EscenarioAhorro.EquipoEficiente(Numero(Watts), Opcional(CicloDeTrabajo), Opcional(PrecioEquipo));

    private async Task SimularAsync(CancellationToken cancellationToken)
    {
        if (AparatoElegido is not { } opcion || _lugarId is not int lugarId)
        {
            TieneResultado = false;
            return;
        }

        var escenario = Escenario();
        var errores = SimuladorValidador.Validar(opcion.Aparato, escenario);
        Errores.Clear();
        foreach (var error in errores)
        {
            Errores.Add(textos[$"Simulador_Error_{error}"]);
        }

        TieneErrores = Errores.Count > 0;
        if (TieneErrores)
        {
            TieneResultado = false;
            return;
        }

        var resultado = await simulador.SimularAsync(
            lugarId, opcion.Aparato.Id, escenario, DateOnly.FromDateTime(DateTime.Today), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (resultado is null)
        {
            TieneResultado = false;
            return;
        }

        Mostrar(resultado, escenario);
    }

    private void Mostrar(ResultadoSimulacion resultado, EscenarioAhorro escenario)
    {
        var consumiriasMas = resultado.Estado == EstadoSimulacion.ConsumiriasMas;

        if (resultado.AhorroPesosMes is decimal pesosMes)
        {
            CifraPrincipal = textos.Formato("Formato_Monto", Math.Abs(pesosMes));
            LeyendaCifra = textos[consumiriasMas ? "Simulador_LeyendaPagariasMas" : "Simulador_LeyendaAhorroMes"];
        }
        else
        {
            CifraPrincipal = textos.Formato("Simulador_CifraKwh", Math.Abs(resultado.AhorroKwhPorMes));
            LeyendaCifra = textos[consumiriasMas ? "Simulador_LeyendaKwhMas" : "Simulador_LeyendaKwhMenos"];
        }

        FilasResultado.Clear();
        if (resultado.AhorroPesosAnio is decimal pesosAnio)
        {
            FilasResultado.Add(new FilaDetalle(
                textos[consumiriasMas ? "Simulador_FilaPagariasMasAnio" : "Simulador_FilaAhorroAnio"],
                textos.Formato("Formato_Monto", Math.Abs(pesosAnio))));
        }

        if (resultado.AhorroPesosPeriodo is decimal pesosPeriodo)
        {
            var periodo = _mesesPorPeriodo == 1 ? "Mes" : "Bimestre";
            FilasResultado.Add(new FilaDetalle(
                textos[consumiriasMas ? $"Simulador_FilaMasPeriodo{periodo}" : $"Simulador_FilaAhorroPeriodo{periodo}"],
                textos.Formato("Formato_Centavos", Math.Abs(pesosPeriodo))));
        }

        FilasResultado.Add(new FilaDetalle(textos["Simulador_FilaKwhAntes"], textos.Formato("Aparatos_KwhMes", resultado.KwhPorMesAntes)));
        FilasResultado.Add(new FilaDetalle(textos["Simulador_FilaKwhDespues"], textos.Formato("Aparatos_KwhMes", resultado.KwhPorMesDespues)));

        // La recuperación solo tiene sentido si se compra un equipo.
        MostrarNotaRecuperacion = escenario.Tipo == TipoEscenario.EquipoEficiente;
        if (MostrarNotaRecuperacion)
        {
            FilasResultado.Add(new FilaDetalle(textos["Simulador_FilaRecuperacion"], Recuperacion(resultado, escenario)));
        }

        TieneDac = resultado.DacAntes is not null && resultado.DacDespues is not null;
        if (resultado.DacAntes is { } antes && resultado.DacDespues is { } despues)
        {
            DacAntesNivel = antes.Nivel;
            DacAntesEtiqueta = textos[$"Dac_{antes.Nivel}"];
            DacAntesTexto = textos.Formato("Simulador_DacPromedio", antes.PromedioMensualKwh);
            DacDespuesNivel = despues.Nivel;
            DacDespuesEtiqueta = textos[$"Dac_{despues.Nivel}"];
            DacDespuesTexto = textos.Formato("Simulador_DacPromedio", despues.PromedioMensualKwh);
        }

        AvisoEstado = resultado.Estado == EstadoSimulacion.Normal ? string.Empty : textos[$"EstadoSimulacion_{resultado.Estado}"];
        TieneResultado = true;
    }

    private string Recuperacion(ResultadoSimulacion resultado, EscenarioAhorro escenario)
        => escenario.PrecioEquipo is not > 0m ? textos["Simulador_SinPrecio"]
            : resultado.RecuperacionMasDe10Anios ? textos["Simulador_MasDe10Anios"]
            : resultado.MesesRecuperacion is decimal meses ? textos.Formato("Simulador_MesesRecuperacion", meses)
            : textos["Simulador_NoSeRecupera"];

    [RelayCommand]
    private void IrAAparatos() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Aparatos));

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());
}
