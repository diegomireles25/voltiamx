using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using VoltiaMX.Services;

namespace VoltiaMX.ViewModels;

/// <summary>
/// Barra del historial: <see cref="Fraccion"/> es su alto relativo a la barra más alta.
/// </summary>
public sealed record BarraHistorial(string Etiqueta, string Valor, double Fraccion, bool EsActual)
{
    public double Restante => 1 - Fraccion;
}

/// <summary>
/// Fila con nombre y detalle (propiedades en renta, aparatos que más gastan).
/// </summary>
public sealed record FilaDetalle(string Titulo, string Detalle);

/// <summary>
/// Aviso del Dashboard. Con <see cref="Accion"/> y sin <see cref="TextoAccion"/>, todo el aviso es un
/// enlace (p. ej. ir a capturar la lectura); con <see cref="TextoAccion"/>, el texto lleva un enlace aparte.
/// </summary>
public sealed record AvisoDashboard(string Texto, IRelayCommand? Accion = null, string? TextoAccion = null)
{
    public bool EsAccion => Accion is not null && TextoAccion is null;

    public bool TieneEnlace => Accion is not null && TextoAccion is not null;
}

/// <summary>
/// Dashboard compuesto como docs/mockup-dashboard.html. Las cifras salen de
/// <see cref="IResumenDashboardService"/>; las secciones dependen de los usos y módulos del perfil.
/// </summary>
public sealed partial class DashboardViewModel(
    IResumenDashboardService resumenes,
    IPerfilService perfiles,
    IDatosDemoService datosDemo,
    IAparatoService aparatos,
    IMetaService metas,
    ILecturaService lecturas,
    ITextos textos) : ObservableObject
{
    /// <summary>
    /// "Confirmar tarifa" junto a la etiqueta de tarifa por confirmar.
    /// </summary>
    [RelayCommand]
    private void ConfirmarTarifa()
        => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje(SeccionesConfiguracion.Tarifas));

    private static readonly CultureInfo Cultura = new("es-MX");

    // Secciones
    [ObservableProperty] public partial bool MostrarHogar { get; set; }
    [ObservableProperty] public partial bool MostrarVacio { get; set; }

    // Fase 9b (D6): qué muestra Inicio. Con lecturas, todo como antes.
    [ObservableProperty] public partial bool EsSinRecibo { get; set; }
    [ObservableProperty] public partial bool EsSiSiguesIgual { get; set; }
    [ObservableProperty] public partial bool EsFechaFutura { get; set; }

    /// <summary>
    /// La cifra grande: con lecturas (a este ritmo) o con el recibo anterior ("Si sigues igual…").
    /// </summary>
    [ObservableProperty] public partial bool MostrarCifra { get; set; }

    /// <summary>
    /// La tarjeta "Comparado con tu recibo anterior": solo con lecturas.
    /// </summary>
    [ObservableProperty] public partial bool MostrarComparacion { get; set; }

    /// <summary>
    /// El estado vacío de siempre ("Agrega tu primera lectura"): sin datos y con alguna lectura suelta.
    /// </summary>
    [ObservableProperty] public partial bool MostrarVacioLecturas { get; set; }

    [ObservableProperty] public partial bool ReciboAproximado { get; set; }
    [ObservableProperty] public partial string TextoFechaFutura { get; set; } = string.Empty;

    [RelayCommand]
    private void IrAReciboPorMonto()
        => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, SeccionesLecturas.PorMonto));

    [RelayCommand]
    private void IrAConfiguracion() => WeakReferenceMessenger.Default.Send(new IrAConfiguracionMensaje());
    [ObservableProperty] public partial bool MostrarRentas { get; set; }
    [ObservableProperty] public partial bool MostrarSolar { get; set; }
    [ObservableProperty] public partial bool MostrarAparatos { get; set; }
    [ObservableProperty] public partial bool TieneDatos { get; set; }
    [ObservableProperty] public partial bool EsDemo { get; set; }

    /// <summary>
    /// La tarifa del lugar trae precios de ejemplo (D3): aviso arriba y "con precios de ejemplo" junto a la cifra.
    /// </summary>
    [ObservableProperty] public partial bool PreciosDeEjemplo { get; set; }

    // Hero
    [ObservableProperty] public partial bool MostrarTarifa { get; set; }
    [ObservableProperty] public partial string EtiquetaTarifa { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TarifaPorConfirmar { get; set; }
    [ObservableProperty] public partial string LeyendaPeriodo { get; set; } = string.Empty;
    [ObservableProperty] public partial string TituloPago { get; set; } = string.Empty;
    [ObservableProperty] public partial string MontoPago { get; set; } = string.Empty;
    [ObservableProperty] public partial string TextoMeta { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneProyeccion { get; set; }
    [ObservableProperty] public partial double ProgresoPeriodo { get; set; }

    // Bajo la barra (D7): "DÍA n DE m" y "FALTAN d DÍAS PARA TU CORTE" (sin texto si ya pasó el corte).
    [ObservableProperty] public partial string TextoDiaDelPeriodo { get; set; } = string.Empty;
    [ObservableProperty] public partial string TextoFaltanDias { get; set; } = string.Empty;

    // Comparado con tu recibo anterior
    [ObservableProperty] public partial bool TieneComparacion { get; set; }
    [ObservableProperty] public partial string CifraComparacion { get; set; } = string.Empty;
    [ObservableProperty] public partial string TextoComparacion { get; set; } = string.Empty;
    [ObservableProperty] public partial string LoQueMasGasta { get; set; } = string.Empty;

    // Para ahorrar (D5, D6): hasta 3 acciones calculadas y, con dos o más, su ahorro junto.
    [ObservableProperty] public partial bool TieneSugerencias { get; set; }
    [ObservableProperty] public partial string TextoCombinado { get; set; } = string.Empty;

    public ObservableCollection<string> Sugerencias { get; } = [];

    // Historial
    [ObservableProperty] public partial string TituloHistorial { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneHistorial { get; set; }

    // Estado
    [ObservableProperty] public partial bool MostrarDac { get; set; }
    [ObservableProperty] public partial NivelRiesgoDac NivelDac { get; set; }
    [ObservableProperty] public partial string TextoNivelDac { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneAvisos { get; set; }
    [ObservableProperty] public partial bool MostrarMeta { get; set; }
    [ObservableProperty] public partial string TextoMetaObjetivo { get; set; } = string.Empty;

    // Rentas
    [ObservableProperty] public partial bool TienePropiedades { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CargarDemoCommand))]
    public partial bool Cargando { get; set; }

    public ObservableCollection<BarraHistorial> Historial { get; } = [];

    public ObservableCollection<AvisoDashboard> Avisos { get; } = [];

    public ObservableCollection<FilaDetalle> Propiedades { get; } = [];

    // Qué gasta más: los primeros del ranking real del lugar principal.
    private const int AparatosEnElDashboard = 4;

    [ObservableProperty] public partial bool TieneAparatos { get; set; }
    [ObservableProperty] public partial string AvisoAparatos { get; set; } = string.Empty;

    /// <summary>
    /// Con pesos se aclara que los costos de los aparatos no suman el recibo (D4).
    /// </summary>
    [ObservableProperty] public partial bool AparatosConPesos { get; set; }

    public ObservableCollection<FilaDetalle> Aparatos { get; } = [];

    [RelayCommand]
    private void IrAAparatos() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Aparatos));

    /// <summary>
    /// "Simular cuánto ahorrarías": solo con el módulo Aparatos activo y al menos un aparato.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrASimuladorCommand))]
    public partial bool PuedeSimular { get; set; }

    [ObservableProperty] public partial string TextoSimular { get; set; } = string.Empty;

    /// <summary>
    /// El simulador de ahorro es una herramienta del modo avanzado (Fase 9a, D4): sin él, el botón no se muestra.
    /// </summary>
    [ObservableProperty] public partial bool MostrarSimular { get; set; }

    [RelayCommand(CanExecute = nameof(PuedeSimular))]
    private void IrASimulador()
        => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Aparatos, SeccionesAparatos.Simulador));

    /// <summary>
    /// Los botones que llevan a módulos que todavía no existen se ven deshabilitados.
    /// </summary>
    public bool AccionesPendientesHabilitadas => false;

    /// <summary>
    /// "Capturar lectura" y el aviso de periodo terminado llevan a Lecturas si el módulo está activo.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrALecturasCommand), nameof(IrAHistorialCommand), nameof(EscanearReciboCommand))]
    [NotifyPropertyChangedFor(nameof(TextoEscanear))]
    public partial bool LecturasHabilitadas { get; set; }

    [RelayCommand(CanExecute = nameof(LecturasHabilitadas))]
    private void IrALecturas() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas));

    /// <summary>
    /// "Escanear recibo" del cierre: solo con el módulo Lecturas activo.
    /// </summary>
    [RelayCommand(CanExecute = nameof(LecturasHabilitadas))]
    private void EscanearRecibo()
        => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, SeccionesLecturas.Escanear));

    public string TextoEscanear => textos[LecturasHabilitadas ? "Dashboard_CierreEscanear" : "Dashboard_CierreEscanearProximamente"];


    /// <summary>
    /// "Ver detalle" del riesgo DAC abre el Predictor si ese módulo está activo.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrAPredictorCommand))]
    public partial bool PredictorHabilitado { get; set; }

    [RelayCommand(CanExecute = nameof(PredictorHabilitado))]
    private void IrAPredictor() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Predictor));

    /// <summary>
    /// Con el módulo Metas activo, la fila META muestra el progreso real y "Ver meta" o "Crear meta".
    /// </summary>
    [ObservableProperty] public partial string TextoEnlaceMeta { get; set; } = string.Empty;

    [RelayCommand]
    private void IrAMetas() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Metas));

    /// <summary>
    /// El enlace de la sección Solar se habilita solo con el módulo activo.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrASolarCommand))]
    public partial bool SolarHabilitado { get; set; }

    [RelayCommand(CanExecute = nameof(SolarHabilitado))]
    private void IrASolar() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Solar));

    [RelayCommand(CanExecute = nameof(LecturasHabilitadas))]
    private void IrAHistorial()
        => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas, SeccionesLecturas.Historial));

    public async Task CargarAsync()
    {
        Cargando = true;
        try
        {
            var perfil = await perfiles.ObtenerPerfilAsync();
            var usos = perfil?.Usos.Select(u => u.Uso).ToHashSet() ?? [];
            var modulos = await perfiles.ObtenerModulosActivosAsync();

            MostrarHogar = usos.Count == 0 || usos.Contains(UsoApp.Casa) || usos.Contains(UsoApp.Negocio);
            MostrarRentas = usos.Contains(UsoApp.Rentas);
            MostrarSolar = usos.Contains(UsoApp.Solar);
            SolarHabilitado = modulos.Contains(ModuloApp.Solar);
            MostrarAparatos = modulos.Contains(ModuloApp.Aparatos);
            MostrarSimular = perfil?.ModoAvanzado == true;
            LecturasHabilitadas = modulos.Contains(ModuloApp.Lecturas);
            PredictorHabilitado = modulos.Contains(ModuloApp.Predictor);

            var resumen = await resumenes.ObtenerAsync(DateOnly.FromDateTime(DateTime.Today));
            // Un objetivo viejo de presupuesto se trata como "sin objetivo": el presupuesto salió de la app (Fase 7a).
            var objetivo = perfil?.Objetivo?.Tipo is TipoObjetivo.NoPasarmeDePresupuesto ? null : perfil?.Objetivo?.Tipo;
            AplicarResumen(resumen, objetivo);
            await CargarAparatosAsync();
            LoQueMasGasta = TieneAparatos ? textos.Formato("Dashboard_LoQueMasGasta", Aparatos[0].Titulo) : string.Empty;
            TextoEnlaceMeta = string.Empty;
            if (modulos.Contains(ModuloApp.Metas))
            {
                await CargarMetaAsync();
            }

            if (MostrarRentas)
            {
                await CargarPropiedadesAsync();
            }

            WeakReferenceMessenger.Default.Send(new EvaluarAvisosMensaje());
        }
        finally
        {
            Cargando = false;
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeCargarDemo))]
    private async Task CargarDemoAsync()
    {
        await datosDemo.CargarAsync(DateOnly.FromDateTime(DateTime.Today));
        await CargarAsync();
    }

    private bool PuedeCargarDemo() => !Cargando;

    private void AplicarResumen(ResumenDashboard? resumen, TipoObjetivo? objetivo)
    {
        MostrarTarifa = resumen is not null;
        TieneDatos = resumen?.TieneDatos == true;
        EsDemo = resumen?.EsDemo == true;
        PreciosDeEjemplo = resumen?.PreciosDeEjemplo == true;
        MostrarVacio = MostrarHogar && !TieneDatos;
        Historial.Clear();
        Avisos.Clear();

        // D6: sin recibo, con recibo y sin lecturas, con lecturas o con la fecha de corte en el futuro.
        var estado = resumen?.EstadoInicio ?? EstadoPantallaInicio.SinRecibo;
        EsSinRecibo = MostrarHogar && estado == EstadoPantallaInicio.SinRecibo;
        EsSiSiguesIgual = MostrarHogar && estado == EstadoPantallaInicio.ReciboSinLecturas;
        EsFechaFutura = MostrarHogar && estado == EstadoPantallaInicio.FechaFutura;
        MostrarVacioLecturas = MostrarVacio && !EsSinRecibo;
        MostrarCifra = TieneDatos && estado is EstadoPantallaInicio.ConLecturas or EstadoPantallaInicio.ReciboSinLecturas;
        MostrarComparacion = TieneDatos && estado == EstadoPantallaInicio.ConLecturas;
        ReciboAproximado = EsSiSiguesIgual && resumen?.UltimoReciboEstimado == true;
        TextoFechaFutura = EsFechaFutura && resumen?.FechaCorteUltimoRecibo is DateOnly corte
            ? textos.Formato("Dashboard_FechaFutura", corte)
            : string.Empty;

        if (resumen is null)
        {
            TieneProyeccion = TieneComparacion = TieneHistorial = MostrarDac = MostrarMeta = TieneAvisos = false;
            return;
        }

        TarifaPorConfirmar = resumen.TarifaPorConfirmar;
        EtiquetaTarifa = textos.Formato(
            resumen.TarifaPorConfirmar ? "Dashboard_TarifaPorConfirmar" : "Dashboard_Tarifa", resumen.NombreTarifa);
        // Sin lecturas (o con la fecha de corte en el futuro) no se muestran días del recibo: nunca días negativos.
        LeyendaPeriodo = (estado == EstadoPantallaInicio.ConLecturas
            ? textos.Formato(
                "Dashboard_LeyendaPeriodo",
                textos[$"TipoLugar_{resumen.TipoLugar}"],
                NombrePeriodo(resumen.InicioPeriodo, resumen.MesesDelPeriodo),
                resumen.DiaDelPeriodo,
                resumen.DiasDelPeriodo)
            : textos.Formato(
                "Dashboard_LeyendaPeriodoSinDia",
                textos[$"TipoLugar_{resumen.TipoLugar}"],
                NombrePeriodo(resumen.InicioPeriodo, resumen.MesesDelPeriodo))).ToUpper(Cultura);

        AplicarProyeccion(resumen);
        if (EsSiSiguesIgual)
        {
            // "Si sigues igual que tu recibo anterior, pagarías unos $N": el último recibo con la tarifa actual,
            // antes de impuestos (ResumenDashboard.MontoReciboAnterior). Sin barra ni días.
            TituloPago = textos["Dashboard_SiSiguesIgualTitulo"];
            MontoPago = resumen.MontoReciboAnterior is decimal anterior ? textos.Formato("Formato_Monto", anterior) : textos["Dashboard_SinCifra"];
            TextoMeta = string.Empty;
            TieneProyeccion = false;
        }

        AplicarComparacion(resumen);
        AplicarHistorial(resumen);
        AplicarEstado(resumen, objetivo);
    }

    private void AplicarProyeccion(ResumenDashboard resumen)
    {
        TituloPago = textos[resumen.MesesDelPeriodo == 1 ? "Dashboard_TituloPagoMes" : "Dashboard_TituloPagoBimestre"];
        TieneProyeccion = resumen.MontoProyectado is not null;
        MontoPago = resumen.MontoProyectado is decimal monto ? textos.Formato("Formato_Monto", monto) : textos["Dashboard_SinCifra"];

        // Desde la Fase 7a no se compara contra un presupuesto: solo se pide capturar si no hay proyección.
        TextoMeta = resumen.MontoProyectado is null ? textos["Dashboard_MetaSinLecturas"] : string.Empty;

        if (resumen.KwhHastaHoy is decimal hastaHoy && resumen.KwhProyectados is decimal proyectados && proyectados > 0)
        {
            ProgresoPeriodo = (double)(hastaHoy / proyectados * 100);
        }

        // D7: bajo la barra, el día del recibo y los días para el corte; si ya pasó, queda el aviso de captura.
        TextoDiaDelPeriodo = textos.Formato("Dashboard_DiaDe", resumen.DiaDelPeriodo, resumen.DiasDelPeriodo);
        TextoFaltanDias = resumen.PeriodoTerminado || resumen.DiasParaCorte <= 0 ? string.Empty
            : resumen.DiasParaCorte == 1 ? textos["Dashboard_FaltaUnDia"]
            : textos.Formato("Dashboard_FaltanDias", resumen.DiasParaCorte);
    }

    /// <summary>
    /// "Comparado con tu recibo anterior", en pesos (D6): lo que pagarías a este ritmo contra el recibo anterior
    /// recalculado con la tarifa actual, con las reglas de <see cref="ComparacionRecibo"/>.
    /// </summary>
    private void AplicarComparacion(ResumenDashboard resumen)
    {
        // D6: en pesos, contra el recibo anterior recalculado con la tarifa actual.
        var comparacion = ComparacionRecibo.Evaluar(resumen.MontoProyectado, resumen.MontoReciboAnterior, resumen.DiaDelPeriodo);
        TieneComparacion = comparacion.Estado != EstadoComparacion.SinReciboAnterior;

        // "unos $N": se redondean los dos montos y la diferencia es la de los redondeados, para que la frase cuadre.
        var ritmo = Math.Round(comparacion.MontoARitmo, MidpointRounding.AwayFromZero);
        var anterior = Math.Round(comparacion.MontoAnterior, MidpointRounding.AwayFromZero);
        var diferencia = Math.Abs(ritmo - anterior);
        (CifraComparacion, TextoComparacion) = comparacion.Estado switch
        {
            EstadoComparacion.MasAlto => (
                textos.Formato("Dashboard_ComparacionCifraArriba", diferencia),
                textos.Formato("Dashboard_ComparacionArriba", ritmo, anterior, diferencia)),
            EstadoComparacion.MasBajo => (
                textos.Formato("Dashboard_ComparacionCifraAbajo", diferencia),
                textos.Formato("Dashboard_ComparacionAbajo", ritmo, anterior, diferencia)),
            EstadoComparacion.Igual => (
                textos["Dashboard_ComparacionCifraIgual"],
                textos.Formato("Dashboard_ComparacionIgual", ritmo, anterior)),
            EstadoComparacion.AunEsPronto => (string.Empty, textos.Formato("Dashboard_ComparacionPronto", comparacion.Dias)),
            _ => (string.Empty, string.Empty),
        };
    }

    // D7: el historial de Inicio va siempre en pesos (antes de impuestos).
    private void AplicarHistorial(ResumenDashboard resumen)
    {
        TituloHistorial = textos["Dashboard_HistorialGasto"];

        var maximo = resumen.Historial.Select(p => p.Monto).DefaultIfEmpty(0m).Max();
        foreach (var periodo in resumen.Historial)
        {
            var valor = periodo.Monto;
            var texto = textos.Formato("Formato_Monto", periodo.Monto);
            var etiqueta = EtiquetaCorta(periodo.Inicio, resumen.MesesDelPeriodo);
            Historial.Add(new BarraHistorial(
                periodo.EsActual ? textos.Formato("Dashboard_EtiquetaActual", etiqueta) : etiqueta,
                periodo.EsActual ? textos.Formato("Dashboard_ValorProyectado", texto) : texto,
                maximo > 0 ? (double)(valor / maximo) : 0,
                periodo.EsActual));
        }

        TieneHistorial = Historial.Count > 0;
    }

    private void AplicarEstado(ResumenDashboard resumen, TipoObjetivo? objetivo)
    {
        MostrarDac = resumen.Dac is not null;
        // Inicio muestra solo el semáforo; el promedio y el límite en kWh están en Cómo se calcula tu recibo.
        if (resumen.Dac is { } dac)
        {
            NivelDac = dac.Nivel;
            TextoNivelDac = textos[$"Dac_{dac.Nivel}"];
        }

        if (resumen.PeriodoTerminado)
        {
            // Ya pasó el corte y falta la lectura que lo cierra.
            Avisos.Add(new AvisoDashboard(textos["Aviso_PeriodoTerminado"], LecturasHabilitadas ? IrALecturasCommand : null));
        }

        // Los días para el corte ya se ven bajo la barra (D7): no se repiten aquí.

        // Riesgo DAC Medio o Alto: aviso con enlace al detalle en el Predictor.
        if (resumen.Dac is { Nivel: NivelRiesgoDac.Medio or NivelRiesgoDac.Alto } riesgo)
        {
            Avisos.Add(new AvisoDashboard(
                textos[riesgo.Nivel == NivelRiesgoDac.Alto ? "Aviso_DacAlto" : "Aviso_DacMedio"],
                PredictorHabilitado ? IrAPredictorCommand : null,
                PredictorHabilitado ? textos["Aviso_VerDetalle"] : null));
        }

        TieneAvisos = Avisos.Count > 0;

        MostrarMeta = objetivo is not null && resumen.VariacionVsPeriodoAnterior is not null;
        if (objetivo is TipoObjetivo tipo && resumen.VariacionVsPeriodoAnterior is decimal variacion)
        {
            TextoMetaObjetivo = textos.Formato(
                variacion > 0 ? "Meta_ArribaDelAnterior" : "Meta_AbajoDelAnterior",
                textos[$"MetaObjetivo_{tipo}"],
                Math.Abs(variacion) * 100);
        }
    }

    private async Task CargarPropiedadesAsync()
    {
        Propiedades.Clear();
        foreach (var propiedad in await resumenes.ObtenerPropiedadesAsync())
        {
            Propiedades.Add(new FilaDetalle(
                propiedad.Nombre,
                propiedad.UltimoMonto is decimal monto
                    ? textos.Formato("Propiedad_Detalle", monto)
                    : textos["Propiedad_SinRecibos"]));
        }

        TienePropiedades = Propiedades.Count > 0;
    }

    // Fila META con la meta del lugar principal; sustituye la comparación del objetivo del onboarding.
    private async Task CargarMetaAsync()
    {
        if (await lecturas.ObtenerLugarPrincipalAsync() is not { } lugar)
        {
            return;
        }

        var progreso = await metas.ObtenerProgresoAsync(lugar.Id, DateOnly.FromDateTime(DateTime.Today));
        MostrarMeta = true;
        if (progreso is null || progreso.Estado == EstadoMeta.Inactiva)
        {
            TextoMetaObjetivo = textos["Dashboard_MetaSinMeta"];
            TextoEnlaceMeta = textos["Dashboard_CrearMeta"];
            return;
        }

        // D8: en pesos con la tarifa actual.
        TextoMetaObjetivo = progreso.Estado switch
        {
            EstadoMeta.Cumpliendo => textos.Formato(
                "Dashboard_MetaCumpliendo", progreso.PorcentajeObjetivo, progreso.ObjetivoPesos ?? 0m, progreso.MontoARitmo ?? 0m),
            EstadoMeta.EnRiesgo => textos.Formato(
                "Dashboard_MetaEnRiesgo", progreso.PorcentajeObjetivo, progreso.FaltaPorAhorrar ?? 0m),
            EstadoMeta.Superada => textos.Formato("Dashboard_MetaSuperada", progreso.PorcentajeObjetivo),
            _ => textos[$"Dashboard_Meta{progreso.Estado}"],
        };
        TextoEnlaceMeta = textos["Dashboard_VerMeta"];
    }

    private async Task CargarAparatosAsync()
    {
        Aparatos.Clear();
        Sugerencias.Clear();
        TieneSugerencias = AparatosConPesos = false;
        TextoCombinado = string.Empty;
        AvisoAparatos = string.Empty;
        var lugar = MostrarAparatos ? await lecturas.ObtenerLugarPrincipalAsync() : null;
        if (lugar is null)
        {
            TieneAparatos = PuedeSimular = false;
            TextoSimular = textos["Dashboard_SimularProximamente"];
            return;
        }

        // D7: los primeros por costo con "unos $N al mes"; sin pesos, solo los nombres (por consumo estimado).
        var costos = await aparatos.ObtenerCostosAsync(lugar.Id, DateOnly.FromDateTime(DateTime.Today));
        foreach (var fila in costos.Costos.Filas.Take(AparatosEnElDashboard))
        {
            Aparatos.Add(new FilaDetalle(
                fila.Nombre, fila.CostoMensual is decimal costo ? textos.Formato("Aparatos_CostoMes", costo) : string.Empty));
        }

        TieneAparatos = Aparatos.Count > 0;
        AparatosConPesos = TieneAparatos && costos.Costos.ConPesos;
        PuedeSimular = TieneAparatos;
        TextoSimular = textos[PuedeSimular ? "Dashboard_Simular" : "Dashboard_SimularProximamente"];
        AvisoAparatos = costos.Ranking.Estado switch
        {
            EstadoRanking.SumaExcedeMedido => textos["Ranking_SumaExcede"],
            EstadoRanking.SinConsumoMedido => textos["Ranking_SinConsumoCorto"],
            _ => string.Empty,
        };

        AplicarSugerencias(costos.Sugerencias);
    }

    // "PARA AHORRAR": cada acción con su ahorro y, con dos o tres, el ahorro de hacerlas juntas (no la suma).
    private void AplicarSugerencias(SugerenciasDeAhorro sugerencias)
    {
        foreach (var sugerencia in sugerencias.Sugerencias)
        {
            Sugerencias.Add(textos.Formato("Sugerencia_Accion", sugerencia.Nombre, sugerencia.AhorroMensual));
        }

        TieneSugerencias = Sugerencias.Count > 0;
        TextoCombinado = sugerencias.AhorroCombinadoMensual is decimal combinado && Sugerencias.Count > 1
            ? textos.Formato(Sugerencias.Count == 3 ? "Sugerencia_CombinadoTres" : "Sugerencia_CombinadoDos", combinado)
            : string.Empty;
    }

    // Bimestres B1..B6 del año; meses con su abreviatura.
    private string EtiquetaCorta(DateOnly inicio, int meses)
        => meses == 1
            ? inicio.ToDateTime(TimeOnly.MinValue).ToString("MMM", Cultura).TrimEnd('.').ToUpper(Cultura)
            : textos.Formato("Dashboard_EtiquetaBimestre", (inicio.Month + 1) / 2);

    private string NombrePeriodo(DateOnly inicio, int meses)
        => meses == 1
            ? inicio.ToDateTime(TimeOnly.MinValue).ToString("MMMM", Cultura)
            : textos.Formato("Dashboard_Bimestre", (inicio.Month + 1) / 2);
}
