using System.Diagnostics;
using ClosedXML.Excel;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;
using Xunit.Abstractions;

namespace VoltiaMX.Tests;

public sealed class GeneradoresReporteTests(ITestOutputHelper salida)
{
    private static readonly DateOnly Hoy = new(2026, 10, 19);

    private static Func<string, string> Texto => TextosDeRecursos.Texto;

    private static ReporteDatos Vacio() => new() { GeneradoEl = new DateTime(2026, 10, 19, 9, 30, 0) };

    // Datos inventados y con números redondos para revisar el Excel.
    private static ReporteDatos Completo(string nombreAparato = "Refrigerador", bool demo = false, bool preciosDeEjemplo = false) => new()
    {
        GeneradoEl = new DateTime(2026, 10, 19, 9, 30, 0),
        Lugar = new LugarDelReporte(1, "Mi casa", TipoLugar.Casa, demo),
        Tarifa = new TarifaDelReporte("1C", TipoTarifa.Domestica, 0m, 850m,
        [
            new RangoDelReporte("Básico", 0m, 150m, 1m),
            new RangoDelReporte("Intermedio", 150m, 450m, 1.25m),
            new RangoDelReporte("Excedente", 450m, null, 3.5m),
        ]),
        Resumen = new ResumenDashboard
        {
            LugarId = 1,
            NombreLugar = "Mi casa",
            TipoLugar = TipoLugar.Casa,
            NombreTarifa = "1C",
            EsDemo = demo,
            PreciosDeEjemplo = preciosDeEjemplo,
            InicioPeriodo = new DateOnly(2026, 9, 27),
            FinPeriodo = new DateOnly(2026, 11, 26),
            MesesDelPeriodo = 2,
            KwhHastaHoy = 412.8m,
            KwhProyectados = 1_049.2m,
            MontoProyectado = 1_572.2m,
            PresupuestoMax = 1_500m,
            DiferenciaContraPresupuesto = 72.2m,
            Dac = new EvaluacionDac(NivelRiesgoDac.Bajo, 428m, 422m, false),
            LimiteDacKwhMensual = 850m,
            DesgloseProyectado = new ResultadoCalculo(
                [new("Básico", 300m, 1m, 300m), new("Intermedio", 600m, 1.25m, 750m), new("Excedente", 149.2m, 3.5m, 522.2m)],
                1_572.2m, 0m, 1_572.2m),
        },
        Periodos =
        [
            new PeriodoDeHistorial(new DateOnly(2026, 5, 27), new DateOnly(2026, 7, 26), 871m, 1_096.5m, false, 1_200m, 14.5m, false),
            new PeriodoDeHistorial(new DateOnly(2026, 7, 27), new DateOnly(2026, 9, 26), 718m, 897.5m, true, null, 11.8m, false),
        ],
        Lecturas =
        [
            new LecturaListada(new DateOnly(2026, 7, 26), 47_492m, null, false, true),
            new LecturaListada(new DateOnly(2026, 9, 26), 48_210m, 718m, false, true),
        ],
        Aparatos = [new AparatoDelReporte(nombreAparato, CategoriaAparato.Refrigeracion, 1, 250m, 24m, 7, 0.55m, 100.1m)],
        Ranking = new RankingAparatos(EstadoRanking.Normal,
            [new FilaRanking(1, nombreAparato, CategoriaAparato.Refrigeracion, 100.1m, 0.191m)], 524.6m, 0.809m, 0.191m),
        Meta = new ProgresoMeta(EstadoMeta.EnRiesgo, 10, 718m, false, 646.2m, 412.8m, 1_049.2m, 6.3m, 89.75m, [], 2),
    };

    private static XLWorkbook Abrir(byte[] contenido) => new(new MemoryStream(contenido));

    // ---- Excel ----

    [Fact]
    public void Excel_HojasYEncabezadosEnEspanol()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Completo(), Texto));

        // Fase 7b: los nombres de hojas y encabezados salen del resw (la hoja "Periodos" pasó a "Recibos").
        Assert.Equal(["Resumen", "Lecturas", "Recibos", "Tarifa", "Aparatos"], libro.Worksheets.Select(h => h.Name));
        Assert.Equal(Texto("Reporte_Fecha"), libro.Worksheet("Lecturas").Cell(1, 1).GetString());
        Assert.Equal(Texto("Reporte_MontoRecibo"), libro.Worksheet(Texto("Reporte_HojaPeriodos")).Cell(1, 5).GetString());
        Assert.Equal("Qué parte del tiempo consume", libro.Worksheet("Aparatos").Cell(1, 7).GetString());
    }

    [Fact]
    public void Excel_NumerosComoNumerosYFechasComoFechas()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Completo(), Texto));
        var lecturas = libro.Worksheet("Lecturas");
        var periodos = libro.Worksheet(Texto("Reporte_HojaPeriodos"));

        Assert.Equal(XLDataType.DateTime, lecturas.Cell(2, 1).DataType);
        Assert.Equal(new DateTime(2026, 7, 26), lecturas.Cell(2, 1).GetDateTime());
        Assert.Equal(XLDataType.Number, lecturas.Cell(3, 2).DataType);
        Assert.Equal(48_210d, lecturas.Cell(3, 2).GetDouble());
        Assert.Equal(718d, lecturas.Cell(3, 3).GetDouble());
        Assert.True(lecturas.Cell(2, 3).IsEmpty()); // la primera no tiene anterior

        Assert.Equal(XLDataType.Number, periodos.Cell(2, 4).DataType);
        Assert.Equal(1_096.5d, periodos.Cell(2, 4).GetDouble());
        Assert.Equal(1_200d, periodos.Cell(2, 5).GetDouble());
        Assert.True(periodos.Cell(3, 5).IsEmpty()); // sin monto de recibo
    }

    [Fact]
    public void Excel_PesosConFormatoYEncabezadoInmovilizado()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Completo(), Texto));

        Assert.Equal("$#,##0.00", libro.Worksheet(Texto("Reporte_HojaPeriodos")).Cell(2, 4).Style.NumberFormat.Format);
        // El precio por kWh muestra de 2 a 4 decimales ($1.25, $0.982); el valor siempre va completo.
        Assert.Equal("$#,##0.00##", libro.Worksheet("Tarifa").Cell(2, 4).Style.NumberFormat.Format);
        Assert.Equal(1.25d, libro.Worksheet("Tarifa").Cell(3, 4).GetDouble());
        Assert.All(libro.Worksheets.Skip(1), h => Assert.Equal(1, h.SheetView.SplitRow));
        Assert.True(libro.Worksheet("Lecturas").Row(1).Style.Font.Bold);
    }

    [Theory]
    [InlineData("=1+1")]
    [InlineData("+5")]
    [InlineData("-3")]
    [InlineData("@SUMA(A1)")]
    public void Excel_TextoQueParecFormula_QuedaComoTexto(string nombre)
    {
        using var libro = Abrir(GeneradorExcel.Generar(Completo(nombre), Texto));
        var celda = libro.Worksheet("Aparatos").Cell(2, 1);

        Assert.False(celda.HasFormula);
        Assert.Equal(XLDataType.Text, celda.DataType);
        Assert.Equal(nombre, celda.GetString());
    }

    [Fact]
    public void Excel_Demo_LoDiceEnLaPrimeraFilaDelResumen()
    {
        using (var demo = Abrir(GeneradorExcel.Generar(Completo(demo: true), Texto)))
        {
            Assert.Equal("DATOS DE EJEMPLO", demo.Worksheet("Resumen").Cell(1, 1).GetString());
        }

        using var real = Abrir(GeneradorExcel.Generar(Completo(), Texto));
        Assert.Equal("Dato", real.Worksheet("Resumen").Cell(1, 1).GetString());
    }

    [Fact]
    public void Excel_PreciosDeEjemplo_LoDiceEnUnaLineaAntesDelEncabezado()
    {
        using (var ejemplo = Abrir(GeneradorExcel.Generar(Completo(demo: true, preciosDeEjemplo: true), Texto)))
        {
            var hoja = ejemplo.Worksheet("Resumen");
            Assert.Equal("DATOS DE EJEMPLO", hoja.Cell(1, 1).GetString());
            Assert.Equal(Texto("Reporte_PreciosDeEjemplo"), hoja.Cell(2, 1).GetString());
            Assert.Equal("Dato", hoja.Cell(3, 1).GetString());
            Assert.Equal(3, hoja.SheetView.SplitRow);
        }

        using var real = Abrir(GeneradorExcel.Generar(Completo(), Texto));
        Assert.DoesNotContain(
            real.Worksheet("Resumen").CellsUsed(),
            c => c.GetString() == Texto("Reporte_PreciosDeEjemplo"));
    }

    [Fact]
    public void Pdf_ConPreciosDeEjemplo_EsUnPdf()
    {
        EsPdf(GeneradorPdf.Generar(Completo(preciosDeEjemplo: true), Texto));
    }

    [Fact]
    public void Excel_ResumenConElPagoProyectadoComoNumero()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Completo(), Texto));
        var resumen = libro.Worksheet("Resumen");

        var fila = resumen.RowsUsed().First(r => r.Cell(1).GetString() == Texto("Reporte_PagoProyectado"));
        Assert.Equal(1_572.2d, fila.Cell(2).GetDouble());
        Assert.Equal("$#,##0.00", fila.Cell(2).Style.NumberFormat.Format);
    }

    [Fact]
    public void Excel_SinDatos_GeneraLasHojasConSusEncabezados()
    {
        using var libro = Abrir(GeneradorExcel.Generar(Vacio(), Texto));

        Assert.Equal(5, libro.Worksheets.Count);
        Assert.Equal("Fecha", libro.Worksheet("Lecturas").Cell(1, 1).GetString());
        Assert.True(libro.Worksheet("Lecturas").Cell(2, 1).IsEmpty());
    }

    // ---- PDF ----

    private static void EsPdf(byte[] pdf)
    {
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
        Assert.InRange(pdf.Length, 1_000, 5_000_000);
    }

    [Fact]
    public void Pdf_Completo_EsUnPdf()
    {
        EsPdf(GeneradorPdf.Generar(Completo(), Texto));
    }

    [Fact]
    public void Pdf_SinDatos_NoLanza()
    {
        EsPdf(GeneradorPdf.Generar(Vacio(), Texto));
    }

    [Fact]
    public void Pdf_ConYSinMarcaDeDemo()
    {
        EsPdf(GeneradorPdf.Generar(Completo(demo: true), Texto));
        EsPdf(GeneradorPdf.Generar(Completo(demo: false), Texto));
    }

    [Fact]
    public void Pdf_NombresMuyLargos_NoLanza()
    {
        var largo = new string('A', 300);
        var datos = Completo(largo) with { Lugar = new LugarDelReporte(1, largo, TipoLugar.Casa, false) };

        EsPdf(GeneradorPdf.Generar(datos, Texto));
    }

    [Fact]
    public void Pdf_CasoGrande_CienPeriodosYSesentaAparatos()
    {
        var periodos = Enumerable.Range(0, 100)
            .Select(i => new PeriodoDeHistorial(
                new DateOnly(2018, 1, 1).AddMonths(i), new DateOnly(2018, 1, 31).AddMonths(i), 500m + i, 600m + i, i % 7 == 0, null, 16m, false))
            .ToList();
        var aparatos = Enumerable.Range(1, 60)
            .Select(i => new AparatoDelReporte($"Aparato {i}", CategoriaAparato.Otro, 1, 100m, 2m, 7, 1m, 8.67m))
            .ToList();
        var filas = aparatos.Select((a, i) => new FilaRanking(i + 1, a.Nombre, a.Categoria, a.KwhPorMes, 0.01m)).ToList();
        var datos = Completo() with
        {
            Periodos = periodos,
            Aparatos = aparatos,
            Ranking = new RankingAparatos(EstadoRanking.Normal, filas, 867m, 0.4m, 0.6m),
        };

        GeneradorPdf.Generar(Vacio(), Texto); // calienta QuestPDF (fuentes, motor)
        var reloj = Stopwatch.StartNew();
        var pdf = GeneradorPdf.Generar(datos, Texto);
        reloj.Stop();

        EsPdf(pdf);
        salida.WriteLine($"PDF con 100 periodos y 60 aparatos: {pdf.Length / 1024} KB en {reloj.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void Formato_FechasYPesosEnEspanolDeMexico()
    {
        Assert.Equal("12 sep 2026", FormatoReporte.Fecha(new DateOnly(2026, 9, 12)));
        Assert.Equal("1 ene 2027", FormatoReporte.Fecha(new DateOnly(2027, 1, 1)));
        Assert.Equal("$1,572.20", FormatoReporte.Pesos(1_572.2m));
        Assert.Equal("-$72.50", FormatoReporte.Pesos(-72.5m));
        Assert.Equal("1,049.20", FormatoReporte.Kwh(1_049.2m));
    }
}
