using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public class ReciboCoherenciaTests
{
    private static ReciboAConfirmar Completo() => ReciboAConfirmar.Desde(ReciboParser.Analizar(ReciboParserTests.ReciboCompleto));

    [Fact]
    public void Comprobar_ReciboCoherente_SinAvisos()
    {
        // 48,623 − 48,201 = 422 = consumo; 150 + 150 + 122 = 422; 147.30 + 177.00 + 427.00 = 751.30 = energía.
        Assert.Empty(ReciboCoherencia.Comprobar(Completo(), ["1", "1C", "1F"]));
    }

    [Fact]
    public void Comprobar_DiferenciaDe1Kwh_SeTolera()
    {
        var recibo = Completo();
        recibo.ConsumoKwh = 421m; // |422 − 421| = 1: dentro de la tolerancia, en lecturas y en rangos.

        Assert.Empty(ReciboCoherencia.Comprobar(recibo));
    }

    [Fact]
    public void Comprobar_ConsumoQueNoCuadra_AvisaLecturasYRangos()
    {
        var recibo = Completo();
        recibo.ConsumoKwh = 425m; // lecturas: 422 ≠ 425; rangos: 422 ≠ 425.

        var avisos = ReciboCoherencia.Comprobar(recibo);

        Assert.Equal(
            [CodigoAvisoRecibo.LecturasNoCuadranConConsumo, CodigoAvisoRecibo.RangosNoSumanConsumo],
            avisos.Select(a => a.Codigo));
        Assert.Equal(422m, avisos[0].Calculado);
        Assert.Equal(425m, avisos[0].Leido);
    }

    [Fact]
    public void Comprobar_ImportesQueNoSuman_Avisa()
    {
        var recibo = Completo();
        recibo.ImporteEnergia = 755m; // 751.30 ≠ 755: 3.70 de diferencia, más de $1.

        var aviso = Assert.Single(ReciboCoherencia.Comprobar(recibo));

        Assert.Equal(CodigoAvisoRecibo.ImportesNoSumanEnergia, aviso.Codigo);
        Assert.Equal(751.30m, aviso.Calculado);
    }

    [Fact]
    public void Comprobar_ImporteDentroDeUnPeso_SeTolera()
    {
        var recibo = Completo();
        recibo.ImporteEnergia = 752.30m; // |751.30 − 752.30| = 1.

        Assert.Empty(ReciboCoherencia.Comprobar(recibo));
    }

    [Fact]
    public void Comprobar_TarifaQueNoTienes_Avisa()
    {
        Assert.Equal(
            [CodigoAvisoRecibo.TarifaNoRegistrada],
            ReciboCoherencia.Comprobar(Completo(), ["1F", "PDBT"]).Select(a => a.Codigo));
        Assert.Empty(ReciboCoherencia.Comprobar(Completo(), [" 1c "]));
    }

    [Fact]
    public void Comprobar_SinDatosParaComparar_SinAvisos()
    {
        Assert.Empty(ReciboCoherencia.Comprobar(ReciboAConfirmar.Desde(ReciboParser.Analizar(["nada"])), ["1C"]));
    }

    [Fact]
    public void Comprobar_ReciboLeido_EsLoMismoQueSuVersionAConfirmar()
    {
        var leido = ReciboParser.Analizar(ReciboParserTests.ReciboCompleto.Select(l => l.Replace("CONSUMO 422", "CONSUMO 430")).ToList());

        Assert.Equal(
            ReciboCoherencia.Comprobar(ReciboAConfirmar.Desde(leido)).Select(a => a.Codigo),
            ReciboCoherencia.Comprobar(leido).Select(a => a.Codigo));
    }

    [Fact]
    public void ANuevaLectura_FechaDelFinDelPeriodoOrigenOcrYTotalDelRecibo()
    {
        var resultado = Completo().ANuevaLectura(lugarId: 7, new DateOnly(2026, 11, 5));

        Assert.Empty(resultado.Errores);
        Assert.Equal(new NuevaLectura(7, new DateOnly(2026, 9, 12), 48_623m, OrigenLectura.Ocr, 871.51m), resultado.Lectura);
    }

    [Fact]
    public void ANuevaLectura_SinFechaNiLectura_Errores()
    {
        var recibo = Completo();
        recibo.FechaLectura = null;
        recibo.LecturaActual = null;

        var resultado = recibo.ANuevaLectura(7, new DateOnly(2026, 11, 5));

        Assert.Null(resultado.Lectura);
        Assert.Equal([ErrorConfirmacionRecibo.FaltaFecha, ErrorConfirmacionRecibo.FaltaLecturaActual], resultado.Errores);
    }

    [Fact]
    public void ANuevaLectura_LecturaNegativa_Error()
    {
        var recibo = Completo();
        recibo.LecturaActual = -5m;

        Assert.Equal([ErrorConfirmacionRecibo.LecturaNegativa], recibo.ANuevaLectura(7, new DateOnly(2026, 11, 5)).Errores);
    }

    [Fact]
    public void ANuevaLectura_SinTotal_SinMontoDeRecibo()
    {
        var recibo = Completo();
        recibo.Total = null;

        Assert.Null(recibo.ANuevaLectura(7, new DateOnly(2026, 11, 5)).Lectura!.MontoRecibo);
    }

    [Fact]
    public void Desde_SinFinDelPeriodo_SinFecha()
    {
        Assert.Null(ReciboAConfirmar.Desde(ReciboParser.Analizar(["LECTURA ACTUAL 48,623"])).FechaLectura);
    }
}
