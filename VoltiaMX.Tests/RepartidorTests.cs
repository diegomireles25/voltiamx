using VoltiaMX.Core.Models;
using VoltiaMX.Core.Services;

namespace VoltiaMX.Tests;

public sealed class RepartidorTests
{
    private static LecturasSubmedidor[] Consumos(params decimal[] kwh)
        => kwh.Select(k => new LecturasSubmedidor(1_000m, 1_000m + k)).ToArray();

    [Fact]
    public void Proporcional_TresInquilinos_LaSumaEsExactamenteElTotal()
    {
        // S = 100 + 100 + 150 = 350. Cuota_i = 3000 x kWh_i / 350:
        //   100 -> 857.142857... -> piso 857.14 (resto 0.2857 centavos)
        //   150 -> 1285.714285... -> piso 1285.71 (resto 0.4285 centavos)
        // Pisos: 857.14 + 857.14 + 1285.71 = 2999.99; falta 1 centavo -> al mayor resto (el de 150).
        var r = Repartidor.Calcular(3_000m, ModoReparto.ProporcionalAlMedido, 400m, Consumos(100m, 100m, 150m));

        Assert.Equal([857.14m, 857.14m, 1_285.72m], r.Cuotas);
        Assert.Equal(3_000m, r.TotalCobrado);
        Assert.Equal(0m, r.AbsorbePropietario);
        Assert.Equal(350m, r.KwhRepartidos);
        Assert.Empty(r.Avisos);
        Assert.True(r.PuedeGuardar);
    }

    [Fact]
    public void SoloMedido_SubmedidoresMenoresQueElPrincipal_ElPropietarioAbsorbeElResto()
    {
        // D = max(400, 350) = 400. Cuota_i = 3000 x kWh_i / 400: 100 -> 750, 150 -> 1125.
        // Objetivo = round(3000 x 350 / 400, 2) = 2625; absorbe 3000 - 2625 = 375.
        var r = Repartidor.Calcular(3_000m, ModoReparto.SoloMedido, 400m, Consumos(100m, 100m, 150m));

        Assert.Equal([750m, 750m, 1_125m], r.Cuotas);
        Assert.Equal(2_625m, r.TotalCobrado);
        Assert.Equal(375m, r.AbsorbePropietario);
        Assert.Equal(7.5m, r.PrecioPromedioKwh);
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void SoloMedido_ConsumosQueNoDividenExacto_LaSumaEsElObjetivoRedondeado()
    {
        // D = max(500, 300) = 500. Cuota_i = 1000 x 100 / 500 = 200 exacto... usamos 1000.01:
        // 1000.01 x 100 / 500 = 200.002 -> piso 200.00 (x3) = 600.00.
        // Objetivo = round(1000.01 x 300 / 500, 2) = round(600.006, 2) = 600.01 -> falta 1 centavo,
        // los tres restos son iguales (0.2) y gana el primero por orden.
        var r = Repartidor.Calcular(1_000.01m, ModoReparto.SoloMedido, 500m, Consumos(100m, 100m, 100m));

        Assert.Equal([200.01m, 200m, 200m], r.Cuotas);
        Assert.Equal(600.01m, r.TotalCobrado);
        Assert.Equal(400m, r.AbsorbePropietario);
    }

    [Fact]
    public void Proporcional_TercioDeUnTotalImpar_LaSumaEsExacta()
    {
        // 100 / 3 = 33.333... -> pisos 33.33 x 3 = 99.99; el centavo va al primero (empate).
        var r = Repartidor.Calcular(100m, ModoReparto.ProporcionalAlMedido, 90m, Consumos(30m, 30m, 30m));

        Assert.Equal([33.34m, 33.33m, 33.33m], r.Cuotas);
        Assert.Equal(100m, r.Cuotas.Sum());
    }

    [Fact]
    public void SoloMedido_SubmedidoresIgualesAlPrincipal_SeReparteTodoElTotal()
    {
        // D = max(350, 350) = 350: igual que el proporcional, sin nada para el propietario.
        var r = Repartidor.Calcular(3_000m, ModoReparto.SoloMedido, 350m, Consumos(100m, 100m, 150m));

        Assert.Equal([857.14m, 857.14m, 1_285.72m], r.Cuotas);
        Assert.Equal(0m, r.AbsorbePropietario);
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void SubmedidoresExcedenAlPrincipal_AvisaPeroCalcula()
    {
        // S = 350 > 300 + 1. SoloMedido: D = max(300, 350) = 350, se reparte el total completo.
        var r = Repartidor.Calcular(3_000m, ModoReparto.SoloMedido, 300m, Consumos(100m, 100m, 150m));

        Assert.Equal([AvisoReparto.SubmedidoresExcedenPrincipal], r.Avisos);
        Assert.Equal([857.14m, 857.14m, 1_285.72m], r.Cuotas);
        Assert.True(r.PuedeGuardar);
    }

    [Fact]
    public void SubmedidoresConMenosDeUnKwhDeMas_NoAvisa()
    {
        // S = 300.8 <= 300 + 1: diferencia de redondeo entre medidores, sin aviso.
        var r = Repartidor.Calcular(1_000m, ModoReparto.SoloMedido, 300m, Consumos(150m, 150.8m));
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void SinConsumoEnLosSubmedidores_NoCalculaNada()
    {
        var r = Repartidor.Calcular(3_000m, ModoReparto.ProporcionalAlMedido, 400m, Consumos(0m, 0m));

        Assert.Equal([AvisoReparto.SinConsumoSubmedido], r.Avisos);
        Assert.Equal([0m, 0m], r.Cuotas);
        Assert.Equal(0m, r.TotalCobrado);
        Assert.Null(r.PrecioPromedioKwh);
        Assert.False(r.PuedeGuardar);
    }

    [Fact]
    public void InquilinoSinConsumo_PagaCero()
    {
        // 1000 x 0 / 200 = 0; 1000 x 200 / 200 = 1000.
        var r = Repartidor.Calcular(1_000m, ModoReparto.ProporcionalAlMedido, 250m, Consumos(0m, 200m));

        Assert.Equal([0m, 1_000m], r.Cuotas);
    }

    [Fact]
    public void UnSoloInquilino_PagaTodoEnProporcionalOSuParteEnSoloMedido()
    {
        // Proporcional: todo el total. SoloMedido: 1500 x 120 / 200 = 900.
        var proporcional = Repartidor.Calcular(1_500m, ModoReparto.ProporcionalAlMedido, 200m, Consumos(120m));
        var soloMedido = Repartidor.Calcular(1_500m, ModoReparto.SoloMedido, 200m, Consumos(120m));

        Assert.Equal([1_500m], proporcional.Cuotas);
        Assert.Equal([900m], soloMedido.Cuotas);
        Assert.Equal(600m, soloMedido.AbsorbePropietario);
    }

    [Fact]
    public void LecturaMenorQueLaAnterior_EsError()
    {
        var r = Repartidor.Calcular(1_000m, ModoReparto.ProporcionalAlMedido, 300m,
            [new LecturasSubmedidor(500m, 450m), new LecturasSubmedidor(100m, 200m)]);

        Assert.Contains(AvisoReparto.LecturaMenorQueAnterior, r.Avisos);
        Assert.False(r.PuedeGuardar);
        Assert.Equal(0m, r.TotalCobrado);
    }

    [Fact]
    public void LecturaNegativa_EsError()
    {
        var r = Repartidor.Calcular(1_000m, ModoReparto.ProporcionalAlMedido, 300m, [new LecturasSubmedidor(-5m, 100m)]);

        Assert.Contains(AvisoReparto.LecturaNegativa, r.Avisos);
        Assert.False(r.PuedeGuardar);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_000_000.01)]
    public void TotalInvalido_EsError(decimal total)
    {
        var r = Repartidor.Calcular(total, ModoReparto.ProporcionalAlMedido, 300m, Consumos(100m));

        Assert.Contains(AvisoReparto.TotalInvalido, r.Avisos);
        Assert.False(r.PuedeGuardar);
    }

    [Fact]
    public void SinInquilinos_NoHayConsumo()
    {
        var r = Repartidor.Calcular(1_000m, ModoReparto.ProporcionalAlMedido, 300m, []);
        Assert.Equal([AvisoReparto.SinConsumoSubmedido], r.Avisos);
    }

    [Fact]
    public void Propiedad_LaSumaDeCuotasEsExactamenteLoQueSeDebiaRepartir()
    {
        var azar = new Random(20261104);
        for (var caso = 0; caso < 500; caso++)
        {
            var total = Math.Round((decimal)azar.NextDouble() * 50_000m + 0.01m, 2);
            var consumos = Enumerable.Range(0, azar.Next(1, 31))
                .Select(_ => Math.Round((decimal)azar.NextDouble() * 900m, azar.Next(0, 3)))
                .ToArray();
            var s = consumos.Sum();
            if (s == 0)
            {
                continue;
            }

            var principal = Math.Round(s * (decimal)(0.5 + azar.NextDouble()), 1);
            foreach (var modo in new[] { ModoReparto.SoloMedido, ModoReparto.ProporcionalAlMedido })
            {
                var r = Repartidor.Calcular(total, modo, principal, Consumos(consumos));
                var esperado = modo == ModoReparto.ProporcionalAlMedido
                    ? total
                    : Math.Round(total * s / Math.Max(principal, s), 2, MidpointRounding.AwayFromZero);

                Assert.Equal(esperado, r.Cuotas.Sum());
                Assert.Equal(esperado, r.TotalCobrado);
                Assert.Equal(total - esperado, r.AbsorbePropietario);
                Assert.All(r.Cuotas, c => Assert.Equal(c, Math.Round(c, 2)));
                Assert.All(r.Cuotas, c => Assert.True(c >= 0));
            }
        }
    }
}
