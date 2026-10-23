using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Texto SINTÉTICO que imita un recibo; ningún dato es de una persona real.
/// </summary>
public class ReciboParserTests
{
    // 150 × 0.982 = 147.30; 150 × 1.180 = 177.00; 122 × 3.500 = 427.00; suma = 751.30.
    // 48,623 − 48,201 = 422 = 150 + 150 + 122.
    internal static readonly string[] ReciboCompleto =
    [
        "COMISION FEDERAL DE ELECTRICIDAD",
        "NOMBRE: CLIENTE DE PRUEBA SINTETICO",
        "TARIFA: 1C",
        "PERIODO FACTURADO: DEL 12 JUL 26 AL 12 SEP 26",
        "DIAS 62",
        "LECTURA ACTUAL 48,623",
        "LECTURA ANTERIOR 48,201",
        "CONSUMO 422 kWh",
        "Básico 150 0.982 147.30",
        "Intermedio 150 1.180 177.00",
        "Excedente 122 3.500 427.00",
        "ENERGÍA $751.30",
        "IVA 16% 120.21",
        "TOTAL A PAGAR $871.51",
        "FECHA DE CORTE 28 SEP 26",
    ];

    [Fact]
    public void Analizar_ReciboCompleto_TodoLeidoConConfianzaAlta()
    {
        var recibo = ReciboParser.Analizar(ReciboCompleto);

        Assert.Equal(new CampoTexto("1C", Confianza.Alta), recibo.Tarifa);
        Assert.Equal(new CampoFecha(new DateOnly(2026, 7, 12), Confianza.Alta), recibo.PeriodoInicio);
        Assert.Equal(new CampoFecha(new DateOnly(2026, 9, 12), Confianza.Alta), recibo.PeriodoFin);
        Assert.Equal(new CampoNumero(62m, Confianza.Alta), recibo.Dias);
        Assert.Equal(new CampoNumero(48_623m, Confianza.Alta), recibo.LecturaActual);
        Assert.Equal(new CampoNumero(48_201m, Confianza.Alta), recibo.LecturaAnterior);
        Assert.Equal(new CampoNumero(422m, Confianza.Alta), recibo.ConsumoKwh);
        Assert.Equal(new CampoNumero(751.30m, Confianza.Alta), recibo.ImporteEnergia);
        Assert.Equal(new CampoNumero(871.51m, Confianza.Alta), recibo.Total);
        Assert.Equal(new CampoFecha(new DateOnly(2026, 9, 28), Confianza.Alta), recibo.FechaCorte);

        Assert.Equal(["Básico", "Intermedio", "Excedente"], recibo.Rangos.Select(r => r.Nombre));
        Assert.Equal([150m, 150m, 122m], recibo.Rangos.Select(r => r.Kwh.Valor));
        Assert.Equal([0.982m, 1.180m, 3.500m], recibo.Rangos.Select(r => r.Precio.Valor));
        Assert.Equal([147.30m, 177.00m, 427.00m], recibo.Rangos.Select(r => r.Importe.Valor));
        Assert.All(recibo.Rangos, r => Assert.Equal(Confianza.Alta, r.Importe.Confianza));
    }

    [Fact]
    public void Analizar_ValorEnLaLineaSiguiente_SeLeePeroParaRevisar()
    {
        var recibo = ReciboParser.Analizar(["LECTURA ACTUAL", "48623", "TOTAL A PAGAR:", "$ 871.51"]);

        Assert.Equal(new CampoNumero(48_623m, Confianza.Baja), recibo.LecturaActual);
        Assert.Equal(new CampoNumero(871.51m, Confianza.Baja), recibo.Total);
    }

    [Fact]
    public void Analizar_MinusculasSinAcentosYEspaciosDeMas()
    {
        var recibo = ReciboParser.Analizar(
        [
            "tarifa   1c",
            "lectura    actual:   48.623",
            "basico   150   0.982   147.30",
            "energia  751.30",
            "total a   pagar  871.51",
        ]);

        Assert.Equal("1C", recibo.Tarifa.Valor);
        Assert.Equal(48_623m, recibo.LecturaActual.Valor);
        Assert.Equal("Básico", Assert.Single(recibo.Rangos).Nombre);
        Assert.Equal(751.30m, recibo.ImporteEnergia.Valor);
        Assert.Equal(871.51m, recibo.Total.Valor);
    }

    [Theory]
    [InlineData("TOTAL A PAGAR $1,234.56", "1234.56")]
    [InlineData("TOTAL A PAGAR 1.234,56", "1234.56")]
    [InlineData("TOTAL A PAGAR $1,234", "1234")]
    [InlineData("TOTAL A PAGAR 147.30", "147.30")]
    [InlineData("TOTAL A PAGAR 2,345,678.90", "2345678.90")]
    public void Analizar_SeparadoresDeMilesYDecimales(string linea, string esperado)
    {
        var total = ReciboParser.Analizar([linea]).Total;

        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), total.Valor);
        Assert.Equal(Confianza.Alta, total.Confianza);
    }

    [Fact]
    public void Analizar_MontoAmbiguo_QuedaNoEncontrado()
    {
        // "1.234" puede ser mil doscientos o uno punto doscientos: no se adivina.
        Assert.Equal(new CampoNumero(null, Confianza.NoEncontrado), ReciboParser.Analizar(["TOTAL A PAGAR 1.234"]).Total);
    }

    [Theory]
    [InlineData("FECHA DE CORTE 28 SEP 26")]
    [InlineData("FECHA DE CORTE 28/09/2026")]
    [InlineData("Fecha de corte: 28-SEP-2026")]
    [InlineData("fecha de corte 28 sept 2026")]
    public void Analizar_FormatosDeFecha(string linea)
    {
        Assert.Equal(new CampoFecha(new DateOnly(2026, 9, 28), Confianza.Alta), ReciboParser.Analizar([linea]).FechaCorte);
    }

    [Fact]
    public void Analizar_PeriodoConFechasNumericas()
    {
        var recibo = ReciboParser.Analizar(["Periodo 12/07/2026 - 12/09/2026"]);

        Assert.Equal(new DateOnly(2026, 7, 12), recibo.PeriodoInicio.Valor);
        Assert.Equal(new DateOnly(2026, 9, 12), recibo.PeriodoFin.Valor);
    }

    [Fact]
    public void Analizar_FechaInvalida_NoEncontrado()
    {
        Assert.Equal(Confianza.NoEncontrado, ReciboParser.Analizar(["FECHA DE CORTE 31/02/2026"]).FechaCorte.Confianza);
    }

    [Fact]
    public void Analizar_ErroresDeOcrDentroDeNumerosEtiquetados()
    {
        // O por 0, l e I por 1.
        var recibo = ReciboParser.Analizar(["LECTURA ACTUAL 4O,62l", "LECTURA ANTERIOR 4O,2OI"]);

        Assert.Equal(40_621m, recibo.LecturaActual.Valor);
        Assert.Equal(40_201m, recibo.LecturaAnterior.Valor);
    }

    [Fact]
    public void Analizar_PalabraDespuesDeLaEtiqueta_NoSeConvierteEnNumero()
    {
        // "SIN" no es un número aunque empiece con S (que en un número sería 5).
        Assert.Equal(Confianza.NoEncontrado, ReciboParser.Analizar(["TOTAL A PAGAR SIN ADEUDO"]).Total.Confianza);
    }

    [Fact]
    public void Analizar_TextoSinNadaUtil_TodoNoEncontrado()
    {
        var recibo = ReciboParser.Analizar(["AVISO IMPORTANTE", "Gracias por su pago", "www.ejemplo.test"]);

        Assert.All(
            new[]
            {
                recibo.Tarifa.Confianza, recibo.PeriodoInicio.Confianza, recibo.PeriodoFin.Confianza, recibo.Dias.Confianza,
                recibo.LecturaAnterior.Confianza, recibo.LecturaActual.Confianza, recibo.ConsumoKwh.Confianza,
                recibo.ImporteEnergia.Confianza, recibo.Total.Confianza, recibo.FechaCorte.Confianza,
            },
            confianza => Assert.Equal(Confianza.NoEncontrado, confianza));
        Assert.Empty(recibo.Rangos);
    }

    [Fact]
    public void Analizar_DosValoresDistintosParaElMismoCampo_NoEncontrado()
    {
        var recibo = ReciboParser.Analizar(["TOTAL A PAGAR $871.51", "TOTAL A PAGAR $900.00"]);

        Assert.Equal(new CampoNumero(null, Confianza.NoEncontrado), recibo.Total);
    }

    [Fact]
    public void Analizar_ElMismoValorRepetido_Alta()
    {
        Assert.Equal(
            new CampoNumero(871.51m, Confianza.Alta),
            ReciboParser.Analizar(["TOTAL A PAGAR $871.51", "Total a pagar 871.51"]).Total);
    }

    [Fact]
    public void Analizar_MontoSueltoSinEtiqueta_NoSeToma()
    {
        Assert.Equal(Confianza.NoEncontrado, ReciboParser.Analizar(["$ 1,234.00", "871.51"]).Total.Confianza);
    }

    [Fact]
    public void Analizar_DatosDeducidos_ConConfianzaBaja()
    {
        // Sin DIAS, CONSUMO ni ENERGIA: 12 jul → 12 sep = 62 días; 48,623 − 48,201 = 422 kWh;
        // 147.30 + 177.00 + 427.00 = 751.30.
        var recibo = ReciboParser.Analizar(ReciboCompleto
            .Where(l => !l.StartsWith("DIAS") && !l.StartsWith("CONSUMO") && !l.StartsWith("ENERG"))
            .ToList());

        Assert.Equal(new CampoNumero(62m, Confianza.Baja), recibo.Dias);
        Assert.Equal(new CampoNumero(422m, Confianza.Baja), recibo.ConsumoKwh);
        Assert.Equal(new CampoNumero(751.30m, Confianza.Baja), recibo.ImporteEnergia);
    }

    [Fact]
    public void Analizar_TarifaDesconocida_NoEncontrado()
    {
        Assert.Equal(Confianza.NoEncontrado, ReciboParser.Analizar(["TARIFA: XYZ"]).Tarifa.Confianza);
        Assert.Equal("PDBT", ReciboParser.Analizar(["Tarifa PDBT"]).Tarifa.Valor);
    }
}
