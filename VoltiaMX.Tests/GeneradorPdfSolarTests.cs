using System.Text;
using System.Text.RegularExpressions;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class GeneradorPdfSolarTests
{
    private static Func<string, string> Texto => TextosDeRecursos.Texto;

    private static DatosPdfSolar Datos(decimal? costo = null, bool demo = false, string? referencia = null, DatosLugarSolar? lugar = null)
    {
        lugar ??= new DatosLugarSolar("Casa de ejemplo", demo, 600m, TarifasDePrueba.Escalonada(), 1, []);
        var parametros = new ParametrosSolar(600m, 100m, 5m, 0.80m, 550m, costo);
        var resultado = SimuladorSolar.Calcular(parametros, lugar, new TarifaCalculator(), new DacService());
        return new DatosPdfSolar(lugar, parametros, resultado, referencia, new DateTime(2026, 11, 4, 12, 0, 0));
    }

    private static void EsPdf(byte[] pdf)
    {
        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static string Todo(DatosPdfSolar d)
    {
        var s = GeneradorPdfSolar.Secciones(d, Texto);
        return string.Join("\n", s.Encabezado.Concat(s.Parametros).Concat(s.Resultado).Concat(s.Dac).Select(l => $"{l.Etiqueta}: {l.Valor}").Concat(s.Avisos));
    }

    [Fact]
    public void ConYSinCosto_ConYSinDemo_SonPdf()
    {
        EsPdf(GeneradorPdfSolar.Generar(Datos(), Texto));
        EsPdf(GeneradorPdfSolar.Generar(Datos(costo: 20_000m), Texto));
        EsPdf(GeneradorPdfSolar.Generar(Datos(demo: true), Texto));
    }

    [Fact]
    public void DatosVacios_NoLanza()
    {
        var d = Datos(lugar: DatosLugarSolar.SinLugar);
        EsPdf(GeneradorPdfSolar.Generar(d, Texto));
        Assert.Contains("Sin tarifa no se calcula el ahorro en pesos.", Todo(d));
    }

    [Fact]
    public void ReferenciaEnSuLargoMaximo_NoLanzaYSeRecorta()
    {
        var cuarenta = new string('R', GeneradorPdfSolar.LargoMaximoReferencia);
        EsPdf(GeneradorPdfSolar.Generar(Datos(referencia: cuarenta), Texto));
        Assert.Contains($"Referencia: {cuarenta}", Todo(Datos(referencia: cuarenta + "XYZ")));
        Assert.DoesNotContain("Referencia:", Todo(Datos(referencia: "   ")));
    }

    [Fact]
    public void ContieneLosAvisosDelResw_YLasCifrasDelResultado()
    {
        var todo = Todo(Datos(costo: 20_000m));

        Assert.Contains(Texto("Solar_AvisoEstimacion"), todo);
        Assert.Contains("No es una cotización.", todo);
        Assert.Contains(Texto("Solar_AvisoNoIncluye"), todo);
        // Fase 7b: las etiquetas salen del resw; lo que se comprueba son las cifras.
        Assert.Contains($"{Texto("SolarPdf_Kwp")}: 4.95 kWp", todo);
        Assert.Contains($"{Texto("SolarPdf_Generacion")}: 602.25 kWh", todo);
        Assert.Contains($"{Texto("SolarPdf_Excedente")}: 2.25 kWh", todo);
        // 75 + 130 + 460 × 3 = $1,585 al mes; inversión 4.95 × 20,000 = $99,000; 99,000 / 1,585 = 62.5 meses.
        Assert.Contains($"{Texto("SolarPdf_AhorroMes")}: $1,585.00", todo);
        Assert.Contains($"{Texto("SolarPdf_Inversion")}: $99,000.00", todo);
        Assert.Contains("Recuperación de la inversión: 62.5 meses (5.2 años)", todo);
    }

    [Fact]
    public void SinCosto_LoDice()
    {
        var todo = Todo(Datos());
        Assert.Contains($"{Texto("SolarPdf_Inversion")}: {Texto("SolarPdf_SinCosto")}", todo);
        Assert.Contains("Costo por kWp instalado: No se indicó costo del sistema", todo);
    }

    [Fact]
    public void ContieneElEspacioSusSupuestosYElAvisoDelTecho()
    {
        // Fase 9e (D4): 9 paneles × 2.6 m² × 1.10 = 25.74 → 26.0 m².
        var todo = Todo(Datos());

        Assert.Contains($"{Texto("SolarPdf_Espacio")}: 26.0 m²", todo);
        Assert.Contains($"{Texto("SolarPdf_SuperficiePanel")}: 2.6 m²", todo);
        Assert.Contains($"{Texto("SolarPdf_EspacioPaso")}: 10 %", todo);
        Assert.Contains(Texto("Solar_AvisoTecho"), todo);
    }

    [Fact]
    public void SinDatosDeContacto()
    {
        var todo = Todo(Datos(costo: 20_000m, referencia: "Proyecto techo"));
        Assert.DoesNotMatch(new Regex("direcci|tel[eé]fono|correo|@|calle|colonia", RegexOptions.IgnoreCase), todo);
        var campos = typeof(DatosLugarSolar).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain(campos, c => Regex.IsMatch(c, "Direccion|Telefono|Correo|Estado|Municipio"));
    }
}
