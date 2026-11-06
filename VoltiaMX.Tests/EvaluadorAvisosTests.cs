using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class EvaluadorAvisosTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);
    private static readonly DateOnly Fin = new(2026, 10, 22);

    // Sin nada que avisar: faltan 20 días, sin DAC, sin tope y sin meta.
    private static ResumenDashboard Resumen(
        int diasParaCorte = 20,
        bool terminado = false,
        NivelRiesgoDac? nivelDac = null,
        decimal? diferenciaContraTope = null,
        bool demo = false) => new()
    {
        NombreLugar = "Mi casa",
        TipoLugar = TipoLugar.Casa,
        NombreTarifa = "1C",
        LugarId = 1,
        EsDemo = demo,
        FinPeriodo = Fin,
        DiasParaCorte = diasParaCorte,
        PeriodoTerminado = terminado,
        Dac = nivelDac is NivelRiesgoDac nivel ? new EvaluacionDac(nivel, 400m, 50m, false) : null,
        PresupuestoMax = diferenciaContraTope is null ? null : 500m,
        DiferenciaContraPresupuesto = diferenciaContraTope,
    };

    private static ProgresoMeta Meta(EstadoMeta estado)
        => new(estado, 10, 500m, false, 450m, 300m, 480m, 10m, null, [], 0);

    private static ResultadoAvisos Evaluar(
        ResumenDashboard? resumen, ProgresoMeta? meta = null, IReadOnlySet<string>? enviadas = null, bool activas = true,
        bool metasActivas = true)
        => EvaluadorAvisos.Evaluar(resumen, meta, Hoy, enviadas ?? new HashSet<string>(), activas, metasActivas);

    [Theory]
    [InlineData(EstadoMeta.EnRiesgo)]
    [InlineData(EstadoMeta.Superada)]
    public void Meta_ConElModuloMetasInactivo_NoAvisa(EstadoMeta estado)
    {
        // Fase 9b (D7): sin modo avanzado no hay pantalla de metas, así que tampoco avisos de meta.
        Assert.DoesNotContain(Evaluar(Resumen(), Meta(estado), metasActivas: false).Pendientes, a => a.Tipo == TipoAviso.Meta);
        Assert.Contains(Evaluar(Resumen(), Meta(estado)).Pendientes, a => a.Tipo == TipoAviso.Meta);
    }

    private static IReadOnlyList<string> Claves(ResultadoAvisos resultado) => resultado.Pendientes.Select(a => a.Clave).ToList();

    [Fact]
    public void Evaluar_SinNadaQueAvisar_Vacio()
    {
        var resultado = Evaluar(Resumen());

        Assert.Empty(resultado.Pendientes);
    }

    [Fact]
    public void Lectura_FaltanTresDias_Avisa()
    {
        // Fin 22 oct, hoy 19 oct: 22 − 19 = 3 días, justo en el umbral.
        var aviso = Assert.Single(Evaluar(Resumen(diasParaCorte: 3)).Pendientes);

        Assert.Equal(TipoAviso.Lectura, aviso.Tipo);
        Assert.Equal("Lectura:2026-10-22:faltan", aviso.Clave);
        Assert.Equal(3, aviso.DiasParaCorte);
        Assert.Equal(Fin, aviso.FinPeriodo);
        Assert.False(aviso.PeriodoTerminado);
    }

    [Fact]
    public void Lectura_FaltanCuatroDias_NoAvisa()
    {
        Assert.Empty(Evaluar(Resumen(diasParaCorte: 4)).Pendientes);
    }

    [Fact]
    public void Lectura_CorteHoy_AvisaComoFaltan()
    {
        Assert.Equal(["Lectura:2026-10-22:faltan"], Claves(Evaluar(Resumen(diasParaCorte: 0))));
    }

    [Fact]
    public void Lectura_PeriodoTerminado_AvisoConSuPropiaClave()
    {
        var aviso = Assert.Single(Evaluar(Resumen(diasParaCorte: -2, terminado: true)).Pendientes);

        Assert.Equal("Lectura:2026-10-22:terminado", aviso.Clave);
        Assert.True(aviso.PeriodoTerminado);
    }

    [Fact]
    public void Dac_Bajo_NoAvisa()
    {
        Assert.Empty(Evaluar(Resumen(nivelDac: NivelRiesgoDac.Bajo)).Pendientes);
    }

    [Theory]
    [InlineData(NivelRiesgoDac.Medio, "Dac:Medio")]
    [InlineData(NivelRiesgoDac.Alto, "Dac:Alto")]
    public void Dac_MedioOAlto_Avisa(NivelRiesgoDac nivel, string clave)
    {
        var aviso = Assert.Single(Evaluar(Resumen(nivelDac: nivel)).Pendientes);

        Assert.Equal(TipoAviso.Dac, aviso.Tipo);
        Assert.Equal(clave, aviso.Clave);
        Assert.Equal(nivel, aviso.NivelDac);
    }

    [Fact]
    public void Dac_YaAvisado_NoSeRepite()
    {
        Assert.Empty(Evaluar(Resumen(nivelDac: NivelRiesgoDac.Alto), enviadas: new HashSet<string> { "Dac:Alto" }).Pendientes);
    }

    [Fact]
    public void Dac_BajaABajo_RestableceMedioYAlto()
    {
        var resultado = Evaluar(Resumen(nivelDac: NivelRiesgoDac.Bajo), enviadas: new HashSet<string> { "Dac:Alto" });

        Assert.Empty(resultado.Pendientes);
        Assert.Equal(["Dac:Medio", "Dac:Alto"], resultado.ARestablecer.Select(r => r.Clave));
        Assert.All(resultado.ARestablecer, r => Assert.Equal(TipoAviso.Dac, r.Tipo));
    }

    [Fact]
    public void Dac_EnMedio_RestableceSoloAlto()
    {
        var resultado = Evaluar(Resumen(nivelDac: NivelRiesgoDac.Medio), enviadas: new HashSet<string> { "Dac:Medio", "Dac:Alto" });

        Assert.Empty(resultado.Pendientes);
        Assert.Equal(["Dac:Alto"], resultado.ARestablecer.Select(r => r.Clave));
    }

    [Fact]
    public void Dac_DeMedioAAlto_AvisaAlto()
    {
        Assert.Equal(["Dac:Alto"], Claves(Evaluar(Resumen(nivelDac: NivelRiesgoDac.Alto), enviadas: new HashSet<string> { "Dac:Medio" })));
    }

    [Fact]
    public void Dac_SinEvaluacion_NoRestableceNada()
    {
        Assert.Empty(Evaluar(Resumen()).ARestablecer);
    }

    [Theory]
    [InlineData(EstadoMeta.SinBase)]
    [InlineData(EstadoMeta.SinConsumoMedido)]
    [InlineData(EstadoMeta.Inactiva)]
    [InlineData(EstadoMeta.Cumpliendo)]
    public void Meta_SinRiesgo_NoAvisa(EstadoMeta estado)
    {
        Assert.Empty(Evaluar(Resumen(), Meta(estado)).Pendientes);
    }

    [Fact]
    public void Meta_SinMeta_NoAvisa()
    {
        Assert.Empty(Evaluar(Resumen(), meta: null).Pendientes);
    }

    [Theory]
    [InlineData(EstadoMeta.EnRiesgo, "Meta:EnRiesgo:2026-10-22")]
    [InlineData(EstadoMeta.Superada, "Meta:Superada:2026-10-22")]
    public void Meta_EnRiesgoOSuperada_Avisa(EstadoMeta estado, string clave)
    {
        var aviso = Assert.Single(Evaluar(Resumen(), Meta(estado)).Pendientes);

        Assert.Equal(TipoAviso.Meta, aviso.Tipo);
        Assert.Equal(clave, aviso.Clave);
        Assert.Equal(estado, aviso.EstadoMeta);
    }

    [Fact]
    public void Presupuesto_SinTope_NoAvisa()
    {
        Assert.Empty(Evaluar(Resumen(diferenciaContraTope: null)).Pendientes);
    }

    [Fact]
    public void Presupuesto_JustoEnElTope_NoAvisa()
    {
        // Proyección 500 contra tope 500: diferencia 0, no se rebasa.
        Assert.Empty(Evaluar(Resumen(diferenciaContraTope: 0m)).Pendientes);
    }

    [Fact]
    public void Presupuesto_ProyeccionArribaDelTope_YaNoAvisa()
    {
        // Proyección 520.50 contra tope 500. Desde la Fase 7a el presupuesto salió de la experiencia:
        // aunque un perfil viejo tenga tope, no hay aviso.
        Assert.Empty(Evaluar(Resumen(diferenciaContraTope: 20.50m)).Pendientes);
    }

    [Fact]
    public void Evaluar_VariosAlMismoTiempo_EnOrdenDeTipo()
    {
        var resultado = Evaluar(
            Resumen(diasParaCorte: 2, nivelDac: NivelRiesgoDac.Medio, diferenciaContraTope: 10m), Meta(EstadoMeta.Superada));

        // Con tope rebasado, pero sin aviso de presupuesto (Fase 7a).
        Assert.Equal(
            [TipoAviso.Lectura, TipoAviso.Dac, TipoAviso.Meta],
            resultado.Pendientes.Select(a => a.Tipo));
    }

    [Fact]
    public void Evaluar_LugarDeDemo_Nada()
    {
        var resultado = Evaluar(Resumen(diasParaCorte: 1, nivelDac: NivelRiesgoDac.Bajo, diferenciaContraTope: 10m, demo: true));

        Assert.Empty(resultado.Pendientes);
        Assert.Empty(resultado.ARestablecer);
    }

    [Fact]
    public void Evaluar_NotificacionesApagadas_NadaNiRestablece()
    {
        var resultado = Evaluar(
            Resumen(diasParaCorte: 1, nivelDac: NivelRiesgoDac.Bajo), Meta(EstadoMeta.EnRiesgo),
            enviadas: new HashSet<string> { "Dac:Alto" }, activas: false);

        Assert.Empty(resultado.Pendientes);
        Assert.Empty(resultado.ARestablecer);
    }

    [Fact]
    public void Evaluar_SinResumen_Nada()
    {
        Assert.Empty(Evaluar(null).Pendientes);
    }

    [Fact]
    public void Evaluar_LaMismaClaveYaEnviada_NoSeRepite()
    {
        var enviadas = new HashSet<string> { "Lectura:2026-10-22:faltan", "Presupuesto:2026-10-22" };

        Assert.Empty(Evaluar(Resumen(diasParaCorte: 1, diferenciaContraTope: 5m), enviadas: enviadas).Pendientes);

        // Otro periodo (otra fecha de fin) es otra clave: sí se avisa.
        Assert.Equal(
            ["Lectura:2026-12-22:faltan"],
            Claves(Evaluar(Resumen(diasParaCorte: 1) with { FinPeriodo = new DateOnly(2026, 12, 22) }, enviadas: enviadas)));
    }
}
