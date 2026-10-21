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
/// Fila con nombre y detalle (propiedades en renta, aparatos de ejemplo).
/// </summary>
public sealed record FilaDetalle(string Titulo, string Detalle);

/// <summary>
/// Aviso del Dashboard. Con <see cref="Accion"/> se muestra como enlace (p. ej. ir a capturar la lectura).
/// </summary>
public sealed record AvisoDashboard(string Texto, IRelayCommand? Accion = null)
{
    public bool EsAccion => Accion is not null;
}

/// <summary>
/// Dashboard compuesto como docs/mockup-dashboard.html. Las cifras salen de
/// <see cref="IResumenDashboardService"/>; las secciones dependen de los usos y módulos del perfil.
/// </summary>
public sealed partial class DashboardViewModel(
    IResumenDashboardService resumenes,
    IPerfilService perfiles,
    IDatosDemoService datosDemo,
    ITextos textos) : ObservableObject
{
    private static readonly CultureInfo Cultura = new("es-MX");

    // Secciones
    [ObservableProperty] public partial bool MostrarHogar { get; set; }
    [ObservableProperty] public partial bool MostrarVacio { get; set; }
    [ObservableProperty] public partial bool MostrarRentas { get; set; }
    [ObservableProperty] public partial bool MostrarSolar { get; set; }
    [ObservableProperty] public partial bool MostrarAparatos { get; set; }
    [ObservableProperty] public partial bool TieneDatos { get; set; }
    [ObservableProperty] public partial bool EsDemo { get; set; }

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
    [ObservableProperty] public partial string KwhHastaHoyTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string KwhCierreTexto { get; set; } = string.Empty;

    // Presupuesto inverso
    [ObservableProperty] public partial bool TienePresupuesto { get; set; }
    [ObservableProperty] public partial string KwhPorDiaTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string TextoInverso { get; set; } = string.Empty;

    // Historial
    [ObservableProperty] public partial string TituloHistorial { get; set; } = string.Empty;
    [ObservableProperty] public partial bool TieneHistorial { get; set; }

    // Estado
    [ObservableProperty] public partial bool MostrarDac { get; set; }
    [ObservableProperty] public partial NivelRiesgoDac NivelDac { get; set; }
    [ObservableProperty] public partial string TextoNivelDac { get; set; } = string.Empty;
    [ObservableProperty] public partial string PromedioDacTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string LimiteDacTexto { get; set; } = string.Empty;
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

    public ObservableCollection<FilaDetalle> AparatosDemo { get; } = [];

    /// <summary>
    /// Los botones que llevan a módulos que todavía no existen se ven deshabilitados.
    /// </summary>
    public bool AccionesPendientesHabilitadas => false;

    /// <summary>
    /// "Capturar lectura" y el aviso de periodo terminado llevan a Lecturas si el módulo está activo.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrALecturasCommand), nameof(IrAHistorialCommand))]
    public partial bool LecturasHabilitadas { get; set; }

    [RelayCommand(CanExecute = nameof(LecturasHabilitadas))]
    private void IrALecturas() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Lecturas));

    /// <summary>
    /// "Ajustar presupuesto" abre Presupuesto solo si ese módulo está activo en el perfil.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(IrAPresupuestoCommand))]
    public partial bool PresupuestoHabilitado { get; set; }

    [RelayCommand(CanExecute = nameof(PresupuestoHabilitado))]
    private void IrAPresupuesto() => WeakReferenceMessenger.Default.Send(new IrAModuloMensaje(ModuloApp.Presupuesto));

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
            MostrarAparatos = modulos.Contains(ModuloApp.Aparatos);
            LecturasHabilitadas = modulos.Contains(ModuloApp.Lecturas);
            PresupuestoHabilitado = modulos.Contains(ModuloApp.Presupuesto);

            // Negocio sin casa: el historial muestra pesos ("gasto por periodo").
            var enPesos = usos.Contains(UsoApp.Negocio) && !usos.Contains(UsoApp.Casa);
            var resumen = await resumenes.ObtenerAsync(DateOnly.FromDateTime(DateTime.Today));
            AplicarResumen(resumen, perfil?.Objetivo?.Tipo, enPesos);

            if (MostrarRentas)
            {
                await CargarPropiedadesAsync();
            }
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

    private void AplicarResumen(ResumenDashboard? resumen, TipoObjetivo? objetivo, bool enPesos)
    {
        MostrarTarifa = resumen is not null;
        TieneDatos = resumen?.TieneDatos == true;
        EsDemo = resumen?.EsDemo == true;
        MostrarVacio = MostrarHogar && !TieneDatos;
        Historial.Clear();
        Avisos.Clear();
        AparatosDemo.Clear();

        if (resumen is null)
        {
            TieneProyeccion = TienePresupuesto = TieneHistorial = MostrarDac = MostrarMeta = TieneAvisos = false;
            return;
        }

        TarifaPorConfirmar = resumen.TarifaPorConfirmar;
        EtiquetaTarifa = textos.Formato(
            resumen.TarifaPorConfirmar ? "Dashboard_TarifaPorConfirmar" : "Dashboard_Tarifa", resumen.NombreTarifa);
        LeyendaPeriodo = textos.Formato(
            "Dashboard_LeyendaPeriodo",
            textos[$"TipoLugar_{resumen.TipoLugar}"],
            NombrePeriodo(resumen.InicioPeriodo, resumen.MesesDelPeriodo),
            resumen.DiaDelPeriodo,
            resumen.DiasDelPeriodo).ToUpper(Cultura);

        AplicarProyeccion(resumen);
        AplicarPresupuesto(resumen);
        AplicarHistorial(resumen, enPesos);
        AplicarEstado(resumen, objetivo);

        if (resumen.EsDemo)
        {
            AparatosDeEjemplo();
        }
    }

    private void AplicarProyeccion(ResumenDashboard resumen)
    {
        TituloPago = textos[resumen.MesesDelPeriodo == 1 ? "Dashboard_TituloPagoMes" : "Dashboard_TituloPagoBimestre"];
        TieneProyeccion = resumen.MontoProyectado is not null;
        MontoPago = resumen.MontoProyectado is decimal monto ? textos.Formato("Formato_Monto", monto) : textos["Dashboard_SinCifra"];

        TextoMeta = (resumen.MontoProyectado, resumen.PresupuestoMax, resumen.DiferenciaContraPresupuesto) switch
        {
            (null, _, _) => textos["Dashboard_MetaSinLecturas"],
            (_, null, _) => textos["Dashboard_MetaSinPresupuesto"],
            (_, decimal meta, decimal diferencia) when diferencia > 0 => textos.Formato("Dashboard_MetaTePasas", meta, diferencia),
            (_, decimal meta, decimal diferencia) => textos.Formato("Dashboard_MetaTeSobra", meta, -diferencia),
            _ => string.Empty,
        };

        if (resumen.KwhHastaHoy is decimal hastaHoy && resumen.KwhProyectados is decimal proyectados && proyectados > 0)
        {
            ProgresoPeriodo = (double)(hastaHoy / proyectados * 100);
            KwhHastaHoyTexto = textos.Formato("Dashboard_KwhHastaHoy", hastaHoy);
            KwhCierreTexto = textos.Formato("Dashboard_KwhAlCierre", proyectados);
        }
    }

    private void AplicarPresupuesto(ResumenDashboard resumen)
    {
        TienePresupuesto = resumen.KwhPorDiaRestante is not null;
        if (resumen.KwhPorDiaRestante is not decimal porDia || resumen.PresupuestoMax is not decimal presupuesto)
        {
            return;
        }

        KwhPorDiaTexto = textos.Formato("Dashboard_KwhAlDia", porDia);
        TextoInverso = resumen.PromedioDiarioKwh is decimal promedio
            ? textos.Formato("Dashboard_InversoConRitmo", presupuesto, promedio)
            : textos.Formato("Dashboard_Inverso", presupuesto);
    }

    private void AplicarHistorial(ResumenDashboard resumen, bool enPesos)
    {
        TituloHistorial = textos[enPesos ? "Dashboard_HistorialGasto"
            : resumen.MesesDelPeriodo == 1 ? "Dashboard_HistorialMes" : "Dashboard_HistorialBimestre"];

        var maximo = resumen.Historial.Select(p => enPesos ? p.Monto : p.Kwh).DefaultIfEmpty(0m).Max();
        foreach (var periodo in resumen.Historial)
        {
            var valor = enPesos ? periodo.Monto : periodo.Kwh;
            var texto = enPesos ? textos.Formato("Formato_Monto", periodo.Monto) : textos.Formato("Formato_Kwh", periodo.Kwh);
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
        if (resumen.Dac is { } dac && resumen.LimiteDacKwhMensual is decimal limite)
        {
            NivelDac = dac.Nivel;
            TextoNivelDac = textos[$"Dac_{dac.Nivel}"];
            PromedioDacTexto = textos.Formato("Formato_Entero", dac.PromedioMensualKwh);
            LimiteDacTexto = textos.Formato("Dashboard_DacLimite", limite);
        }

        if (resumen.PeriodoTerminado)
        {
            // Ya pasó el corte y falta la lectura que lo cierra.
            Avisos.Add(new AvisoDashboard(textos["Aviso_PeriodoTerminado"], LecturasHabilitadas ? IrALecturasCommand : null));
        }
        else if (resumen.TieneDatos)
        {
            Avisos.Add(new AvisoDashboard(textos.Formato("Aviso_Corte", resumen.DiasParaCorte)));
        }

        if (resumen.VariacionRitmoPresupuesto is decimal ritmo)
        {
            Avisos.Add(new AvisoDashboard(textos.Formato(ritmo > 0 ? "Aviso_ArribaDelRitmo" : "Aviso_AbajoDelRitmo", Math.Abs(ritmo) * 100)));
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
                propiedad.UltimoMonto is decimal monto && propiedad.UltimoKwh is decimal kwh
                    ? textos.Formato("Propiedad_Detalle", monto, kwh)
                    : textos["Propiedad_SinRecibos"]));
        }

        TienePropiedades = Propiedades.Count > 0;
    }

    /// <summary>
    /// Ranking fijo del mockup, solo con los datos de demo: todavía no existe el módulo Aparatos.
    /// </summary>
    private void AparatosDeEjemplo()
    {
        AparatosDemo.Add(new FilaDetalle(textos["Demo_AparatoAire"], textos.Formato("Formato_Porcentaje", 46))); // EJEMPLO
        AparatosDemo.Add(new FilaDetalle(textos["Demo_AparatoRefrigerador"], textos.Formato("Formato_Porcentaje", 19))); // EJEMPLO
        AparatosDemo.Add(new FilaDetalle(textos["Demo_AparatoLavadora"], textos.Formato("Formato_Porcentaje", 9))); // EJEMPLO
        AparatosDemo.Add(new FilaDetalle(textos["Demo_AparatoIluminacion"], textos.Formato("Formato_Porcentaje", 8))); // EJEMPLO
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
