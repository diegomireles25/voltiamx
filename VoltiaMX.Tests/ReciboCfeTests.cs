using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

/// <summary>
/// Fase 9k: el recibo de CFE como se ve hoy. Texto SINTÉTICO (ningún dato es de una persona): etiquetas pegadas a su
/// valor con ":", meses abreviados en mayúsculas, año de 2 dígitos, cuatro tramos y el desglose del importe.
/// </summary>
public sealed class ReciboCfeTests
{
    // Recibo A. Cuentas a mano:
    //   Consumo: (11,000 − 10,000) × 1 = 1,000 kWh = 300 + 300 + 300 + 100.
    //   Tramos: 300 × 1.000 = 300.00; 300 × 1.200 = 360.00; 300 × 1.500 = 450.00; 100 × 4.000 = 400.00; suma 1,510.00.
    //   Importe del periodo: 1,510.00 + IVA 16 % (241.60) = 1,751.60 ("Fac. del Periodo").
    //   Periodo: 12 jun → 10 ago 2026 = 59 días. La fecha del recibo es el FIN: 10 de agosto, no el "corte a partir" (28 ago).
    internal static readonly string[] ReciboA =
    [
        "LÍMITE DE PAGO:27 AGO 26",
        "CORTE A PARTIR:28 AGO 26",
        "TARIFA:1CNO. MEDIDOR:123ABC",
        "MULTIPLICADOR:1",
        "PERIODO FACTURADO:12 JUN 26-10 AGO 26",
        "Concepto | Lectura actual | Lectura anterior | Total periodo | Precio (MXN) | Subtotal (MXN)",
        "Energía (kWh) 11,000 10,000 1,000",
        "Básico 300 1.000 300.00",
        "Intermedio1 300 1.200 360.00",
        "Intermedio2 300 1.500 450.00",
        "Excedente 100 4.000 400.00",
        "Desglose del importe a pagar",
        "Energía 1,510.00",
        "IVA 16% 241.60",
        "Fac. del Periodo 1,751.60",
        "Adeudo Anterior 1,500.00",
        "Su Pago -1,500.00",
        "Total 1,751.60",
        "TOTAL A PAGAR $1,752",
    ];

    // Variante B: con adeudo anterior sin pagar; el importe del recibo sigue siendo 1,751.60.
    internal static readonly string[] ReciboB = ReciboA
        .Select(l => l switch
        {
            "Adeudo Anterior 1,500.00" => "Adeudo Anterior 1,751.60",
            "Su Pago -1,500.00" => "Su Pago 0.00",
            "Total 1,751.60" => "Total 3,503.20",
            "TOTAL A PAGAR $1,752" => "TOTAL A PAGAR $3,503",
            _ => l,
        })
        .ToArray();

    private static string[] Cambiar(string[] recibo, string antes, params string[] despues)
        => recibo.SelectMany(l => l == antes ? despues : [l]).ToArray();

    [Fact]
    public void ReciboA_TodoLoQueImporta()
    {
        var r = ReciboParser.Analizar(ReciboA);

        Assert.Equal(new CampoFecha(new DateOnly(2026, 6, 12), Confianza.Alta), r.PeriodoInicio);
        Assert.Equal(new CampoFecha(new DateOnly(2026, 8, 10), Confianza.Alta), r.PeriodoFin);
        Assert.Equal(new CampoFecha(new DateOnly(2026, 8, 10), Confianza.Alta), r.FechaCorte);
        Assert.Equal(new CampoTexto("1C", Confianza.Alta), r.Tarifa);
        Assert.Equal(new CampoNumero(1m, Confianza.Alta), r.Multiplicador);
        Assert.Equal(new CampoNumero(11_000m, Confianza.Alta), r.LecturaActual);
        Assert.Equal(new CampoNumero(10_000m, Confianza.Alta), r.LecturaAnterior);
        Assert.Equal(new CampoNumero(1_000m, Confianza.Alta), r.ConsumoKwh);

        Assert.Equal(["Básico", "Intermedio 1", "Intermedio 2", "Excedente"], r.Rangos.Select(x => x.Nombre));
        Assert.Equal([300m, 300m, 300m, 100m], r.Rangos.Select(x => x.Kwh.Valor));
        Assert.Equal([1.000m, 1.200m, 1.500m, 4.000m], r.Rangos.Select(x => x.Precio.Valor));
        Assert.Equal([300.00m, 360.00m, 450.00m, 400.00m], r.Rangos.Select(x => x.Importe.Valor));

        Assert.Equal(new CampoNumero(1_510.00m, Confianza.Alta), r.ImporteEnergia);
        Assert.Equal(new CampoNumero(241.60m, Confianza.Alta), r.Iva);
        Assert.Equal(new CampoNumero(1_751.60m, Confianza.Alta), r.FacturacionPeriodo);
        Assert.Equal(new CampoNumero(1_500.00m, Confianza.Alta), r.AdeudoAnterior);
        Assert.Equal(new CampoNumero(1_500.00m, Confianza.Alta), r.SuPago);
        Assert.Equal(new CampoNumero(1_751.60m, Confianza.Alta), r.TotalDesglose);
        Assert.Equal(new CampoNumero(1_752m, Confianza.Alta), r.Total);
        Assert.Equal(new CampoNumero(1_751.60m, Confianza.Alta), r.ImporteRecibo);
        // 1,752 − 1,751.60 = 0.40: menos de $2, no se avisa.
        Assert.False(r.TotalIncluyeSaldos);
        Assert.Empty(ReciboCoherencia.Comprobar(r));
    }

    [Fact]
    public void ReciboA_ConLosErroresDelOcr_SeLeeIgual()
    {
        // Lo que devolvió el OCR de Windows con una imagen SINTÉTICA del recibo A: 1 → I / l, o → 0, 0 → O, un
        // espacio antes de la coma de miles y el "16%" del IVA perdido.
        string[] ocr =
        [
            "LIMITE DE PAGO:27 AGO 26",
            "CORTE A PARTIR:28 AGO 26",
            "TARIFA:ICNO. MEDIDOR:123ABC",
            "MULTIPLICADOR:I",
            "PERIODO FACTURADO:12 JUN 26-10 AGO 26",
            "Energia (kWh) 11,000 10,000 1,000",
            "Basico 300 1.000 300.00",
            "Intermediol 300 1.200 360.00",
            "Intermedi02 300 1.500 450.00",
            "Excedente 100 4.000 400.00",
            "Energia 1,510.OO",
            "IVA 241.60",
            "Fac. del Periodo 1,751.60",
            "Adeudo Anterior 1 ,500.00",
            "su Pago -1 ,500.OO",
            "Total 1,751.60",
            "TOTAL A PAGAR $1 ,752",
        ];

        var r = ReciboParser.Analizar(ocr);

        Assert.Equal(new CampoTexto("1C", Confianza.Alta), r.Tarifa);
        Assert.Equal(["Básico", "Intermedio 1", "Intermedio 2", "Excedente"], r.Rangos.Select(x => x.Nombre));
        Assert.Equal([300.00m, 360.00m, 450.00m, 400.00m], r.Rangos.Select(x => x.Importe.Valor));
        Assert.Equal((1_510.00m, 241.60m, 1_500.00m, 1_752m), (r.ImporteEnergia.Valor, r.Iva.Valor, r.AdeudoAnterior.Valor, r.Total.Valor));
        Assert.Equal(new CampoNumero(1_751.60m, Confianza.Alta), r.ImporteRecibo);
        Assert.Empty(ReciboCoherencia.Comprobar(r));
        Assert.Equal(1_000m, ReciboCoherencia.ConsumoConfiable(r));
    }

    [Theory]
    [InlineData("TARIFA: I", "1")]
    [InlineData("TARIFA: lA", "1A")]
    [InlineData("TARIFA: DAC", "DAC")]
    public void Tarifa_ConElUnoLeidoComoLetra(string linea, string esperada)
    {
        Assert.Equal(esperada, ReciboParser.Analizar([linea]).Tarifa.Valor);
    }

    [Fact]
    public void VarianteB_ElImporteEsElDelPeriodo_YSeAvisaDelTotal()
    {
        var r = ReciboParser.Analizar(ReciboB);

        Assert.Equal(1_751.60m, r.ImporteRecibo.Valor);
        Assert.Equal(3_503m, r.Total.Valor);
        Assert.Equal(1_751.60m, r.AdeudoAnterior.Valor);
        Assert.True(r.TotalIncluyeSaldos);
        var aviso = Assert.Single(ReciboCoherencia.Comprobar(r));
        Assert.Equal((CodigoAvisoRecibo.TotalIncluyeSaldos, 1_751.60m, 3_503m), (aviso.Codigo, aviso.Calculado, aviso.Leido));
    }

    [Fact]
    public void SinFacturacionDelPeriodo_EnergiaMasIva_YSinNingunoElTotalParaRevisar()
    {
        // Sin "Fac. del Periodo": 1,510.00 + 241.60 = 1,751.60, deducido (REVISAR).
        var sinFac = ReciboParser.Analizar(ReciboA.Where(l => !l.StartsWith("Fac.")).ToArray());
        Assert.Equal(new CampoNumero(1_751.60m, Confianza.Baja), sinFac.ImporteRecibo);

        // Sin desglose: el TOTAL A PAGAR, para revisar, con el aviso de que puede incluir saldos.
        var soloTotal = ReciboParser.Analizar(["PERIODO FACTURADO:12 JUN 26-10 AGO 26", "TOTAL A PAGAR $1,752"]);
        Assert.Equal(new CampoNumero(1_752m, Confianza.Baja), soloTotal.ImporteRecibo);
        Assert.Contains(ReciboCoherencia.Comprobar(soloTotal), a => a.Codigo == CodigoAvisoRecibo.TotalSinDesglose);
    }

    // ---- Fecha (D1) ----

    [Fact]
    public void SoloCorteAPartir_LaFechaQuedaNoEncontrada()
    {
        var r = ReciboParser.Analizar(["LÍMITE DE PAGO:27 AGO 26", "CORTE A PARTIR:28 AGO 26", "FECHA DE CORTE 28 AGO 26", "VENCIMIENTO 27 AGO 26"]);

        Assert.Equal(CampoFecha.NoEncontrado, r.FechaCorte);
        Assert.Equal(CampoFecha.NoEncontrado, r.PeriodoFin);
    }

    [Theory]
    [InlineData("PERIODO FACTURADO: 12 JUN 26 AL 10 AGO 26")]
    [InlineData("PERIODO FACTURADO:12 JUN 26 – 10 AGO 26")]
    [InlineData("Periodo facturado: 12 jun 2026 - 10 ago 2026")]
    [InlineData("periodo facturado del 12 jun 26 al 10 ago 26")]
    [InlineData("PERIODO FACTURADO: 12/06/2026 - 10/08/2026")]
    public void FormatosDelPeriodo(string linea)
    {
        var r = ReciboParser.Analizar([linea]);

        Assert.Equal(new CampoFecha(new DateOnly(2026, 6, 12), Confianza.Alta), r.PeriodoInicio);
        Assert.Equal(new CampoFecha(new DateOnly(2026, 8, 10), Confianza.Alta), r.FechaCorte);
    }

    [Theory]
    // 59 y 30 días: coherentes. 24 días y 71 días: para revisar.
    [InlineData("PERIODO FACTURADO:12 JUN 26-10 AGO 26", Confianza.Alta)]
    [InlineData("PERIODO FACTURADO:11 JUL 26-10 AGO 26", Confianza.Alta)]
    [InlineData("PERIODO FACTURADO:17 JUL 26-10 AGO 26", Confianza.Baja)]
    [InlineData("PERIODO FACTURADO:31 MAY 26-10 AGO 26", Confianza.Baja)]
    public void DiasDelPeriodo_FueraDe25a70_ParaRevisar(string linea, Confianza esperada)
        => Assert.Equal(esperada, ReciboParser.Analizar([linea]).FechaCorte.Confianza);

    [Fact]
    public void InicioDespuesDelFin_ParaRevisar()
    {
        var r = ReciboParser.Analizar(["PERIODO FACTURADO:10 AGO 26-12 JUN 26"]);

        Assert.Equal(Confianza.Baja, r.FechaCorte.Confianza);
    }

    [Fact]
    public void DosPeriodosDistintos_NoEncontrado()
    {
        var r = ReciboParser.Analizar(["PERIODO FACTURADO:12 JUN 26-10 AGO 26", "PERIODO FACTURADO:11 ABR 26-11 JUN 26"]);

        Assert.Equal(CampoFecha.NoEncontrado, r.FechaCorte);
    }

    [Fact]
    public void FacturacionDelPeriodo_NoSeConfundeConElPeriodoFacturado()
    {
        Assert.Equal(new DateOnly(2026, 8, 10), ReciboParser.Analizar(ReciboA).PeriodoFin.Valor);
        Assert.Equal(CampoFecha.NoEncontrado, ReciboParser.Analizar(["Fac. del Periodo 1,751.60"]).PeriodoFin);
    }

    // ---- Tarifa (D2) ----

    [Theory]
    [InlineData("TARIFA:1CNO. MEDIDOR:123ABC", "1C")]
    [InlineData("TARIFA:1 NO. MEDIDOR:ABC", "1")]
    [InlineData("Tarifa: DAC", "DAC")]
    [InlineData("TARIFA:1F", "1F")]
    public void TarifaSinArrastrarTextoPegado(string linea, string esperada)
        => Assert.Equal(new CampoTexto(esperada, Confianza.Alta), ReciboParser.Analizar([linea]).Tarifa);

    [Fact]
    public void TarifaDesconocida_NoEncontrada()
    {
        Assert.Equal(CampoTexto.NoEncontrado, ReciboParser.Analizar(["TARIFA:XYZ"]).Tarifa);
        Assert.Equal(CampoTexto.NoEncontrado, ReciboParser.Analizar(["TARIFA:2B"]).Tarifa);
    }

    // ---- Tramos y tabla (D3, D5) ----

    [Fact]
    public void TablaConLasColumnasEnLineasSeparadas_SeLeeParaRevisar()
    {
        // El OCR puede devolver cada celda en su línea.
        string[] lineas =
        [
            "PERIODO FACTURADO:12 JUN 26-10 AGO 26",
            "Energía (kWh)", "11,000", "10,000", "1,000",
            "Básico", "300", "1.000", "300.00",
            "Intermedio 1", "300", "1.200", "360.00",
            "Intermedio 2", "300", "1.500", "450.00",
            "Excedente", "100", "4.000", "400.00",
            "Energía 1,510.00",
        ];

        var r = ReciboParser.Analizar(lineas);

        Assert.Equal(new CampoNumero(1_000m, Confianza.Baja), r.ConsumoKwh);
        Assert.Equal(11_000m, r.LecturaActual.Valor);
        Assert.Equal([300m, 300m, 300m, 100m], r.Rangos.Select(x => x.Kwh.Valor));
        Assert.Equal([300.00m, 360.00m, 450.00m, 400.00m], r.Rangos.Select(x => x.Importe.Valor));
        Assert.All(r.Rangos, x => Assert.Equal(Confianza.Baja, x.Importe.Confianza));
        Assert.Equal(1_510.00m, r.ImporteEnergia.Valor);
    }

    [Fact]
    public void TramosQueNoSumanElConsumo_Aviso()
    {
        // 300 + 300 + 300 + 50 = 950 ≠ 1,000; 50 × 4 = 200 ≠ 400 (el subtotal tampoco cuadra).
        var r = ReciboParser.Analizar(Cambiar(ReciboA, "Excedente 100 4.000 400.00", "Excedente 50 4.000 400.00"));

        var avisos = ReciboCoherencia.Comprobar(r);
        Assert.Contains(avisos, a => a is { Codigo: CodigoAvisoRecibo.RangosNoSumanConsumo, Calculado: 950m, Leido: 1_000m });
        Assert.Contains(avisos, a => a is { Codigo: CodigoAvisoRecibo.SubtotalNoCuadra, Calculado: 200m, Leido: 400m });
    }

    [Fact]
    public void LecturasConMultiplicador_Coherencia()
    {
        // (11,000 − 10,000) × 2 = 2,000 ≠ 1,000: no cuadra.
        var r = ReciboParser.Analizar(Cambiar(ReciboA, "MULTIPLICADOR:1", "MULTIPLICADOR:2"));

        Assert.Contains(ReciboCoherencia.Comprobar(r),
            a => a is { Codigo: CodigoAvisoRecibo.LecturasNoCuadranConConsumo, Calculado: 2_000m, Leido: 1_000m });
    }

    [Fact]
    public void EnergiaMasIvaQueNoDaElImporte_Aviso()
    {
        // 1,510.00 + 241.60 = 1,751.60 ≠ 1,800.00.
        var r = ReciboParser.Analizar(Cambiar(ReciboA, "Fac. del Periodo 1,751.60", "Fac. del Periodo 1,800.00"));

        Assert.Contains(ReciboCoherencia.Comprobar(r),
            a => a is { Codigo: CodigoAvisoRecibo.EnergiaMasIvaNoCuadra, Calculado: 1_751.60m, Leido: 1_800.00m });
    }
}
