using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class DatosDemoService(IDbContextFactory<VoltiaDbContext> fabrica, ITarifaCalculator calculadora)
    : IDatosDemoService
{
    private const int MesesDelPeriodo = 2; // EJEMPLO: verificar con tarifas vigentes de CFE
    private const int DiaDelPeriodoHoy = 24; // EJEMPLO: hoy es el día 24 del bimestre
    private const decimal LecturaEnElUltimoCorte = 48_210m; // EJEMPLO
    private const decimal KwhPorDia = 17.2m; // EJEMPLO

    // kWh por bimestre, del más antiguo al más reciente: doce meses, lo que pide el cálculo DAC.
    private static readonly decimal[] KwhPorBimestre = [820m, 764m, 912m, 1_046m, 871m, 718m]; // EJEMPLO

    // Calibrados para que, contra el consumo medido de la demo (17.2 kWh al día ≈ 525 kWh al mes),
    // el ranking quede cerca de 46 %, 19 %, 9 % y 8 % y ~18 % sin identificar.
    private static List<Aparato> Aparatos() =>
    [
        new() { Nombre = "Aire acondicionado", Categoria = CategoriaAparato.Climatizacion, Cantidad = 1, Watts = 1_100m, HorasPorDia = 8m, DiasPorSemana = 7, CicloDeTrabajo = 0.9m }, // EJEMPLO
        new() { Nombre = "Refrigerador", Categoria = CategoriaAparato.Refrigeracion, Cantidad = 1, Watts = 250m, HorasPorDia = 24m, DiasPorSemana = 7, CicloDeTrabajo = 0.55m }, // EJEMPLO
        new() { Nombre = "Lavadora", Categoria = CategoriaAparato.Lavado, Cantidad = 1, Watts = 2_200m, HorasPorDia = 1m, DiasPorSemana = 5, CicloDeTrabajo = 1m }, // EJEMPLO
        new() { Nombre = "Iluminación", Categoria = CategoriaAparato.Iluminacion, Cantidad = 20, Watts = 10m, HorasPorDia = 7m, DiasPorSemana = 7, CicloDeTrabajo = 1m }, // EJEMPLO
    ];

    public async Task<bool> HayDemoAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Lugares.AnyAsync(l => l.EsDemo, cancellationToken);
    }

    public async Task CargarAsync(DateOnly hoy, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        if (await db.Lugares.AnyAsync(l => l.EsDemo, cancellationToken))
        {
            return;
        }

        var tarifa = await db.Tarifas
            .Include(t => t.Rangos)
            .OrderBy(t => t.Nombre == "1C" ? 0 : 1) // EJEMPLO: verificar con tarifas vigentes de CFE
            .ThenBy(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No hay tarifas en la base de datos.");

        var ultimoCorte = hoy.AddDays(-DiaDelPeriodoHoy);
        var cortes = Cortes(ultimoCorte.Day, ultimoCorte);
        var lugar = new Lugar
        {
            Nombre = "Casa de ejemplo", // EJEMPLO
            Tipo = TipoLugar.Casa,
            TarifaId = tarifa.Id,
            DiaCorte = ultimoCorte.Day,
            MesesPorPeriodo = MesesDelPeriodo,
            Estado = "Sonora", // EJEMPLO
            Municipio = "Hermosillo", // EJEMPLO
            EsDemo = true,
            Periodos = PeriodosCerrados(cortes, tarifa),
            Lecturas = [.. LecturasDeCorte(cortes), .. LecturasDelPeriodoActual(ultimoCorte, hoy)],
            Aparatos = Aparatos(),
            Meta = new Meta { PorcentajeObjetivo = 10, FechaInicio = hoy.AddMonths(-7), Activa = true }, // EJEMPLO: así se ve el historial
        };

        db.Lugares.Add(lugar);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task QuitarAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        await db.Lugares.Where(l => l.EsDemo).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Fechas de corte con las mismas reglas de <see cref="VentanaPeriodo"/>, de la más antigua a
    /// <paramref name="ultimoCorte"/>. La primera es la base del bimestre más antiguo.
    /// </summary>
    private static List<DateOnly> Cortes(int diaCorte, DateOnly ultimoCorte)
    {
        var cortes = new List<DateOnly> { ultimoCorte };
        foreach (var _ in KwhPorBimestre)
        {
            cortes.Insert(0, VentanaPeriodo.CorteDelMes(diaCorte, cortes[0].AddMonths(-MesesDelPeriodo)));
        }

        return cortes;
    }

    private List<Periodo> PeriodosCerrados(List<DateOnly> cortes, Tarifa tarifa)
        => KwhPorBimestre
            .Select((kwh, i) => new Periodo
            {
                Inicio = cortes[i].AddDays(1),
                Fin = cortes[i + 1],
                KwhTotales = kwh,
                Monto = calculadora.Calcular(kwh, tarifa, MesesDelPeriodo).Total,
            })
            .ToList();

    /// <summary>
    /// Lo que marcaba el medidor en cada corte: de la lectura del último corte hacia atrás,
    /// restando el consumo de cada bimestre.
    /// </summary>
    private static List<Lectura> LecturasDeCorte(List<DateOnly> cortes)
    {
        var lecturas = new List<Lectura>();
        var valor = LecturaEnElUltimoCorte;
        for (var i = cortes.Count - 1; i >= 0; i--)
        {
            lecturas.Insert(0, new Lectura { Fecha = cortes[i], ValorKwh = valor, Origen = OrigenLectura.Manual });
            if (i > 0)
            {
                valor -= KwhPorBimestre[i - 1];
            }
        }

        return lecturas;
    }

    /// <summary>
    /// Una lectura cada tres días después del último corte y la de hoy.
    /// </summary>
    private static List<Lectura> LecturasDelPeriodoActual(DateOnly ultimoCorte, DateOnly hoy)
    {
        var lecturas = new List<Lectura>();
        for (var fecha = ultimoCorte.AddDays(3); fecha < hoy; fecha = fecha.AddDays(3))
        {
            lecturas.Add(Leer(fecha));
        }

        lecturas.Add(Leer(hoy));
        return lecturas;

        Lectura Leer(DateOnly fecha) => new()
        {
            Fecha = fecha,
            ValorKwh = LecturaEnElUltimoCorte + KwhPorDia * (fecha.DayNumber - ultimoCorte.DayNumber),
            Origen = OrigenLectura.Manual,
        };
    }
}
