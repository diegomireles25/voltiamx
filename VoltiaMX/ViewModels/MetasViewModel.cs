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
/// Periodo del historial de la meta, ya en texto.
/// </summary>
public sealed record FilaHistorialMetaVista(string Periodo, string Monto, string Reduccion, bool Cumplio, string Etiqueta);

/// <summary>
/// Meta del lugar principal: "reducir X % contra el periodo anterior". El progreso no se guarda; se
/// recalcula al abrir la pantalla y después de cada cambio.
/// </summary>
public sealed partial class MetasViewModel(
    IMetaService metas,
    ILecturaService lecturas,
    IAparatoService aparatos,
    IDialogos dialogos,
    ITextos textos) : ObservableObject
{
    private static readonly TimeSpan EsperaValidacion = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DuracionAviso = TimeSpan.FromSeconds(6);
    private const int PorcentajePorDefecto = 10;

    private int? _lugarId;
    private CancellationTokenSource? _validacion;
    private CancellationTokenSource? _aviso;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool TieneLugar { get; set; }

    [ObservableProperty] public partial bool EsDemo { get; set; }

    /// <summary>
    /// La tarifa del lugar trae precios de ejemplo: se muestra el aviso con el enlace a Tarifas (D3).
    /// </summary>
    [ObservableProperty] public partial bool PreciosDeEjemplo { get; set; }

    [RelayCommand]
    private void IrATarifas() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Tarifas));
    [ObservableProperty] public partial bool TieneMeta { get; set; }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarFormulario))]
    public partial bool MostrarVacio { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarFormulario))]
    public partial bool MostrarEditor { get; set; }

    /// <summary>
    /// El campo del porcentaje: para crear la meta o al cambiarla.
    /// </summary>
    public bool MostrarFormulario => MostrarVacio || MostrarEditor;
    [ObservableProperty] public partial string Aviso { get; set; } = string.Empty;

    // ---- Porcentaje ----

    [ObservableProperty] public partial double Porcentaje { get; set; } = PorcentajePorDefecto;
    [ObservableProperty] public partial string ErrorPorcentaje { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    public partial bool PorcentajeValido { get; set; } = true;

    // ---- Progreso ----

    [ObservableProperty] public partial EstadoMeta Estado { get; set; }
    [ObservableProperty] public partial string EstadoEtiqueta { get; set; } = string.Empty;
    [ObservableProperty] public partial string MetaTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial bool MostrarTarjeta { get; set; }
    [ObservableProperty] public partial bool MostrarSinDatos { get; set; }
    [ObservableProperty] public partial string SinDatosTitulo { get; set; } = string.Empty;
    [ObservableProperty] public partial string SinDatosTexto { get; set; } = string.Empty;
    // D8: la cifra y la leyenda de la tarjeta, en pesos con la tarifa actual.
    [ObservableProperty] public partial string CifraMeta { get; set; } = string.Empty;
    [ObservableProperty] public partial string LeyendaMeta { get; set; } = string.Empty;
    [ObservableProperty] public partial double Progreso { get; set; }
    [ObservableProperty] public partial string ProgresoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string AvisoBase { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneHistorial { get; set; }

    // Cómo lograrlo (D5, D8): las sugerencias calculadas y su ahorro junto contra lo que falta.
    [ObservableProperty] public partial bool TieneSugerencias { get; set; }
    [ObservableProperty] public partial string TextoLograrlo { get; set; } = string.Empty;

    public ObservableCollection<string> Sugerencias { get; } = [];

    public ObservableCollection<FilaDetalle> Filas { get; } = [];

    public ObservableCollection<FilaHistorialMetaVista> Historial { get; } = [];

    public async Task CargarAsync()
    {
        var lugar = await lecturas.ObtenerLugarPrincipalAsync();
        _lugarId = lugar?.Id;
        TieneLugar = lugar is not null;
        EsDemo = lugar?.EsDemo == true;
        PreciosDeEjemplo = lugar?.PreciosDeEjemplo == true;
        await RecargarAsync();
    }

    private async Task RecargarAsync()
    {
        var progreso = _lugarId is int lugarId ? await metas.ObtenerProgresoAsync(lugarId, DateOnly.FromDateTime(DateTime.Today)) : null;
        TieneMeta = progreso is not null && progreso.Estado != EstadoMeta.Inactiva;
        MostrarVacio = !TieneMeta;
        MostrarEditor = false;
        Porcentaje = progreso?.PorcentajeObjetivo ?? PorcentajePorDefecto;
        ErrorPorcentaje = string.Empty;
        PorcentajeValido = true;
        Filas.Clear();
        Historial.Clear();
        Sugerencias.Clear();
        TieneHistorial = TieneSugerencias = false;
        TextoLograrlo = string.Empty;
        if (TieneMeta)
        {
            Mostrar(progreso!);
            if (MostrarTarjeta && _lugarId is int lugar)
            {
                MostrarSugerencias(await aparatos.ObtenerCostosAsync(lugar, DateOnly.FromDateTime(DateTime.Today)), progreso!);
            }
        }
    }

    private void Mostrar(ProgresoMeta progreso)
    {
        Estado = progreso.Estado;
        EstadoEtiqueta = textos[$"EstadoMeta_{progreso.Estado}"];
        MetaTexto = textos.Formato("Metas_MetaActual", progreso.PorcentajeObjetivo);

        MostrarTarjeta = progreso.Estado is EstadoMeta.Cumpliendo or EstadoMeta.EnRiesgo or EstadoMeta.Superada;
        MostrarSinDatos = !MostrarTarjeta;
        SinDatosTitulo = MostrarSinDatos ? textos[$"Metas_{progreso.Estado}Titulo"] : string.Empty;
        SinDatosTexto = MostrarSinDatos ? textos[$"Metas_{progreso.Estado}Texto"] : string.Empty;

        MostrarCifra(progreso);

        // La barra sigue la parte del objetivo en kWh ya consumida; el texto solo dice el porcentaje (I5).
        if (progreso.ConsumidoKwh is decimal consumido && progreso.ObjetivoKwh is decimal objetivo && objetivo > 0m)
        {
            Progreso = Math.Min(100d, (double)(consumido / objetivo * 100m));
            ProgresoTexto = textos.Formato("Metas_ProgresoTexto", Math.Round(consumido / objetivo * 100m, MidpointRounding.AwayFromZero));
        }

        Agregar("Metas_FilaBase", progreso.BasePesos);
        Agregar("Metas_FilaObjetivo", progreso.ObjetivoPesos);
        Agregar("Metas_FilaConsumido", progreso.ConsumidoPesos);
        Agregar("Metas_FilaProyectado", progreso.MontoARitmo);
        if (progreso.AhorroPesosObjetivo is decimal pesos)
        {
            Filas.Add(new FilaDetalle(textos["Metas_FilaPesos"], textos.Formato("Formato_Centavos", pesos)));
        }

        Filas.Add(new FilaDetalle(textos["Metas_FilaRacha"], progreso.Racha switch
        {
            0 => textos["Metas_Racha0"],
            1 => textos["Metas_Racha1"],
            _ => textos.Formato("Metas_Racha", progreso.Racha),
        }));
        AvisoBase = progreso.BaseEstimada ? textos["Metas_BaseEstimada"] : string.Empty;

        foreach (var fila in progreso.Historial)
        {
            Historial.Add(new FilaHistorialMetaVista(
                textos.Formato("Historial_Periodo", fila.Inicio, fila.Fin),
                fila.Monto is decimal monto ? textos.Formato("Formato_Unos", monto) : string.Empty,
                textos.Formato("Metas_Reduccion", fila.Reduccion * 100m),
                fila.Cumplio,
                textos[fila.Cumplio ? "Metas_Cumplida" : "Metas_NoCumplida"]));
        }

        TieneHistorial = Historial.Count > 0;

        void Agregar(string clave, decimal? pesos)
        {
            if (pesos is decimal valor)
            {
                Filas.Add(new FilaDetalle(textos[clave], textos.Formato("Formato_Unos", valor)));
            }
        }
    }

    // "Tu recibo debería quedar en unos $x o menos. A este ritmo irías en unos $y. Te faltan unos $z."
    private void MostrarCifra(ProgresoMeta progreso)
    {
        CifraMeta = LeyendaMeta = string.Empty;
        if (progreso.ObjetivoPesos is not decimal objetivoPesos || progreso.MontoARitmo is not decimal ritmoPesos)
        {
            return;
        }

        if (progreso.Estado == EstadoMeta.Superada)
        {
            CifraMeta = textos["Metas_CifraSuperada"];
            LeyendaMeta = textos["Metas_LeyendaSuperada"];
            return;
        }

        // Se redondean los dos montos y lo que falta es la diferencia de los redondeados, para que la frase cuadre.
        var objetivo = Math.Round(objetivoPesos, MidpointRounding.AwayFromZero);
        var ritmo = Math.Round(ritmoPesos, MidpointRounding.AwayFromZero);
        var falta = Math.Max(0m, ritmo - objetivo);
        (CifraMeta, LeyendaMeta) = falta > 0m
            ? (textos.Formato("Metas_CifraFalta", falta), textos.Formato("Metas_LeyendaPesos", objetivo, ritmo, falta))
            : (textos["Metas_CifraDentro"], textos.Formato("Metas_LeyendaPesosCumplida", objetivo, ritmo));
    }

    // Cómo lograrlo: las acciones de Inicio y lo que ahorrarían juntas en un recibo, contra lo que falta.
    private void MostrarSugerencias(CostosDelLugar costos, ProgresoMeta progreso)
    {
        var sugerencias = costos.Sugerencias;
        foreach (var sugerencia in sugerencias.Sugerencias)
        {
            Sugerencias.Add(textos.Formato("Sugerencia_Accion", sugerencia.Nombre, sugerencia.AhorroMensual));
        }

        TieneSugerencias = Sugerencias.Count > 0;
        if (sugerencias.AhorroCombinadoMensual is not decimal combinado)
        {
            return;
        }

        var porRecibo = combinado * Math.Max(costos.MesesPorPeriodo, 1);
        var juntas = textos.Formato(
            Sugerencias.Count switch { 1 => "Metas_LograrloUna", 2 => "Metas_LograrloDos", _ => "Metas_LograrloTres" }, porRecibo);
        TextoLograrlo = progreso.FaltaPorAhorrar is decimal falta && falta > 0m
            ? $"{juntas} {textos[porRecibo >= falta ? "Metas_LograrloCubre" : "Metas_LograrloParte"]}"
            : juntas;
    }

    partial void OnPorcentajeChanged(double value) => ProgramarValidacion();

    private async void ProgramarValidacion()
    {
        _validacion?.Cancel();
        var cancelacion = _validacion = new CancellationTokenSource();
        try
        {
            await Task.Delay(EsperaValidacion, cancelacion.Token);
            ValidarAhora();
        }
        catch (OperationCanceledException)
        {
            // Llegó otro cambio.
        }
    }

    /// <summary>
    /// Valida sin esperar (Enter guarda aunque no hayan pasado los 300 ms).
    /// </summary>
    public void ValidarAhora()
    {
        _validacion?.Cancel();
        var errores = MetaValidador.Validar(PorcentajeEntero());
        ErrorPorcentaje = errores.Count > 0 ? textos[$"Meta_Error_{errores[0]}"] : string.Empty;
        PorcentajeValido = errores.Count == 0;
    }

    private int PorcentajeEntero() => double.IsNaN(Porcentaje) ? 0 : (int)Math.Round(Porcentaje);

    private bool PuedeGuardar() => TieneLugar && PorcentajeValido;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        ValidarAhora();
        if (!PorcentajeValido || _lugarId is not int lugarId)
        {
            return;
        }

        var creada = !TieneMeta;
        var porcentaje = PorcentajeEntero();
        var resultado = await metas.GuardarAsync(lugarId, porcentaje, DateOnly.FromDateTime(DateTime.Today));
        if (resultado.Errores.Count > 0)
        {
            ErrorPorcentaje = textos[$"Meta_Error_{resultado.Errores[0]}"];
            PorcentajeValido = false;
            return;
        }

        await RecargarAsync();
        MostrarAviso(textos.Formato(creada ? "Metas_Creada" : "Metas_Cambiada", porcentaje));
    }

    [RelayCommand]
    private void CambiarPorcentaje() => MostrarEditor = true;

    [RelayCommand]
    private async Task CancelarCambioAsync() => await RecargarAsync();

    [RelayCommand]
    private async Task DesactivarAsync()
    {
        if (_lugarId is not int lugarId)
        {
            return;
        }

        var confirmado = await dialogos.ConfirmarAsync(
            textos["Metas_DesactivarTitulo"],
            textos["Metas_DesactivarTexto"],
            textos["Metas_DesactivarConfirmar"],
            textos["Eliminar_Cancelar"]);
        if (!confirmado)
        {
            return;
        }

        await metas.DesactivarAsync(lugarId);
        await RecargarAsync();
        MostrarAviso(textos["Metas_Desactivada"]);
    }

    [RelayCommand]
    private void IrALecturas() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas));

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());

    private async void MostrarAviso(string texto)
    {
        _aviso?.Cancel();
        var cancelacion = _aviso = new CancellationTokenSource();
        Aviso = texto;
        try
        {
            await Task.Delay(DuracionAviso, cancelacion.Token);
            Aviso = string.Empty;
        }
        catch (OperationCanceledException)
        {
            // Llegó otro aviso.
        }
    }
}
