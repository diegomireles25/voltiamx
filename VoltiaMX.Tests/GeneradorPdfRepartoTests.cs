using System.Text;
using System.Text.RegularExpressions;
using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class GeneradorPdfRepartoTests
{
    private static Func<string, string> Texto => TextosDeRecursos.Texto;

    private static RepartoGuardado Reparto(bool demo = false, params LineaRepartoGuardada[] lineas)
        => new(1, 1, "Edificio Centro", demo, new(2026, 9, 2), new(2026, 11, 1), 3_000m, OrigenTotal.MontoRecibo,
            ModoReparto.SoloMedido, 400m, new(2026, 11, 4, 12, 0, 0),
            lineas.Length > 0
                ? lineas
                : [
                    new(1, "Depto 1", 1_000m, 1_100m, 100m, 750m),
                    new(2, "Depto 2", 2_000m, 2_100m, 100m, 750m),
                    new(3, "Depto 3", 500m, 650m, 150m, 1_125m),
                ]);

    private static void EsPdf(byte[] pdf)
    {
        Assert.True(pdf.Length > 1_000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Recibo_YResumen_SonPdf()
    {
        var reparto = Reparto();
        EsPdf(GeneradorPdfReparto.Recibo(reparto, 2, Texto));
        EsPdf(GeneradorPdfReparto.Resumen(reparto, Texto));
    }

    [Fact]
    public void ConMarcaDeDemo_SonPdf()
    {
        var reparto = Reparto(demo: true);
        EsPdf(GeneradorPdfReparto.Recibo(reparto, 0, Texto));
        EsPdf(GeneradorPdfReparto.Resumen(reparto, Texto));
    }

    [Fact]
    public void DatosVacios_NoLanza()
    {
        var vacio = Reparto() with { Lineas = [], NombreLugar = string.Empty };
        EsPdf(GeneradorPdfReparto.Resumen(vacio, Texto));

        var enCero = Reparto(false, new LineaRepartoGuardada(null, string.Empty, 0m, 0m, 0m, 0m));
        EsPdf(GeneradorPdfReparto.Recibo(enCero, 0, Texto));
        Assert.Throws<ArgumentOutOfRangeException>(() => GeneradorPdfReparto.Recibo(vacio, 0, Texto));
    }

    [Fact]
    public void NombresMuyLargos_NoLanza()
    {
        var largo = new string('W', 300);
        var reparto = Reparto(false, new LineaRepartoGuardada(1, largo, 1m, 2m, 1m, 3_000m)) with { NombreLugar = largo };

        EsPdf(GeneradorPdfReparto.Recibo(reparto, 0, Texto));
        EsPdf(GeneradorPdfReparto.Resumen(reparto, Texto));
    }

    [Fact]
    public void TreintaInquilinos_ElResumenSale()
    {
        var lineas = Enumerable.Range(1, 30).Select(n => new LineaRepartoGuardada(n, $"Departamento {n}", 1_000m, 1_010m + n, 10m + n, 100m + n)).ToArray();

        EsPdf(GeneradorPdfReparto.Resumen(Reparto(false, lineas), Texto));
        EsPdf(GeneradorPdfReparto.Recibo(Reparto(false, lineas), 29, Texto));
    }

    [Fact]
    public void Recibo_TraeNombreLecturasYFormaDeCalculo_SinDatosDeContacto()
    {
        var (datos, calculo) = GeneradorPdfReparto.LineasRecibo(Reparto(), 2, Texto);
        var todo = string.Join("\n", datos.Concat(calculo).Select(l => $"{l.Etiqueta}: {l.Valor}"));

        Assert.Contains("Inquilino o unidad: Depto 3", todo);
        Assert.Contains("Lugar: Edificio Centro", todo);
        Assert.Contains("Periodo: 2 sep 2026 – 1 nov 2026", todo);
        Assert.Contains("500.00", todo);
        Assert.Contains("650.00", todo);
        Assert.Contains("150.00 kWh", todo);
        Assert.Contains("$3,000.00 (monto del recibo de CFE)", todo);
        Assert.Contains("350.00 kWh", todo);
        Assert.Contains("$7.50", todo);
        Assert.DoesNotMatch(new Regex("direcci|tel[eé]fono|correo|@|calle|colonia", RegexOptions.IgnoreCase), todo);

        // El reparto guardado no tiene campos de contacto ni de ubicación que se pudieran filtrar al PDF.
        var campos = typeof(RepartoGuardado).GetProperties().Concat(typeof(LineaRepartoGuardada).GetProperties()).Select(p => p.Name);
        Assert.DoesNotContain(campos, c => Regex.IsMatch(c, "Direccion|Telefono|Correo|Estado|Municipio"));
    }

    [Fact]
    public void Textos_DelReciboExistenEnElResw()
    {
        foreach (var clave in new[] { "RepartoPdf_Titulo", "RepartoPdf_Cuota", "RepartoPdf_Nota", "RepartoPdf_Modo_SoloMedido", "RepartoPdf_Modo_ProporcionalAlMedido" })
        {
            Assert.False(string.IsNullOrWhiteSpace(Texto(clave)));
        }

        Assert.Equal("Documento informativo. No es un comprobante fiscal ni sustituye tu contrato de arrendamiento.", Texto("RepartoPdf_Nota"));
        foreach (var aviso in Enum.GetValues<AvisoReparto>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Texto($"Reparto_Aviso_{aviso}")));
        }

        foreach (var origen in Enum.GetValues<OrigenTotal>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Texto($"RepartoPdf_Origen_{origen}")));
        }
    }

    [Theory]
    [InlineData("Depto 2", "Depto 2")]
    [InlineData("a/b:c", "a-b-c")]
    [InlineData("  <x>|\"y?*  ", "-x---y--")]
    [InlineData("CON", "CON_")]
    [InlineData("con", "con_")]
    [InlineData("lpt9", "lpt9_")]
    [InlineData("NUL.txt", "NUL_.txt")]
    [InlineData("COM10", "COM10")]
    [InlineData("Depto 3.", "Depto 3")]
    [InlineData("Depto . . ", "Depto")]
    [InlineData("", "inquilino")]
    [InlineData("...", "inquilino")]
    [InlineData(null, "inquilino")]
    public void NombreArchivo_Limpiar(string? texto, string esperado)
        => Assert.Equal(esperado, NombreArchivo.Limpiar(texto));

    [Fact]
    public void NombreArchivo_MuyLargoYControles()
    {
        Assert.Equal(60, NombreArchivo.Limpiar(new string('x', 200)).Length);
        Assert.Equal("a-b", NombreArchivo.Limpiar("a\tb"));
        Assert.Equal("recibo-a-b-c-2026-11-01.pdf", NombreArchivo.Recibo("a/b:c", new DateOnly(2026, 11, 1)));
        Assert.Equal("recibo-CON_-2026-11-01.pdf", NombreArchivo.Recibo("CON", new DateOnly(2026, 11, 1)));
        Assert.Equal("reparto-2026-11-01.pdf", NombreArchivo.Resumen(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void NombreArchivo_Disponible_NoSobrescribe()
    {
        var carpeta = Directory.CreateTempSubdirectory("voltiamx-nombres-").FullName;
        try
        {
            Assert.Equal(Path.Combine(carpeta, "recibo.pdf"), NombreArchivo.Disponible(carpeta, "recibo.pdf"));
            File.WriteAllText(Path.Combine(carpeta, "recibo.pdf"), "x");
            Assert.Equal(Path.Combine(carpeta, "recibo-2.pdf"), NombreArchivo.Disponible(carpeta, "recibo.pdf"));
            File.WriteAllText(Path.Combine(carpeta, "recibo-2.pdf"), "x");
            Assert.Equal(Path.Combine(carpeta, "recibo-3.pdf"), NombreArchivo.Disponible(carpeta, "recibo.pdf"));
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }
}
