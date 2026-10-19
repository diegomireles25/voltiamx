using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class DatosDemoService(IDbContextFactory<VoltiaDbContext> fabrica, ITarifaCalculator calculadora)
    : IDatosDemoService
{
    private const int MesesDelPeriodo = 2; // EJEMPLO: verificar con tarifas vigentes de CFE

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

        var inicioActual = hoy.AddDays(-23); // EJEMPLO: hoy es el día 24 del bimestre
        var lugar = new Lugar
        {
            Nombre = "Casa de ejemplo", // EJEMPLO
            Tipo = TipoLugar.Casa,
            TarifaId = tarifa.Id,
            DiaCorte = inicioActual.AddDays(-1).Day,
            Estado = "Sonora", // EJEMPLO
            Municipio = "Hermosillo", // EJEMPLO
            EsDemo = true,
            Periodos = PeriodosCerrados(inicioActual, tarifa),
            Lecturas = LecturasDelPeriodoActual(inicioActual, hoy),
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
    /// Seis bimestres consecutivos (doce meses, lo que pide el cálculo DAC) que terminan
    /// el día antes de <paramref name="inicioActual"/>.
    /// </summary>
    private List<Periodo> PeriodosCerrados(DateOnly inicioActual, Tarifa tarifa)
    {
        // kWh por bimestre, del más antiguo al más reciente.
        decimal[] kwhPorBimestre = [820m, 764m, 912m, 1_046m, 871m, 718m]; // EJEMPLO

        var periodos = new List<Periodo>();
        var fin = inicioActual.AddDays(-1);
        for (var i = kwhPorBimestre.Length - 1; i >= 0; i--)
        {
            var inicio = fin.AddMonths(-MesesDelPeriodo).AddDays(1);
            periodos.Insert(0, new Periodo
            {
                Inicio = inicio,
                Fin = fin,
                KwhTotales = kwhPorBimestre[i],
                Monto = calculadora.Calcular(kwhPorBimestre[i], tarifa, MesesDelPeriodo).Total,
            });
            fin = inicio.AddDays(-1);
        }

        return periodos;
    }

    /// <summary>
    /// Lectura inicial el primer día del periodo y una cada tres días hasta hoy.
    /// </summary>
    private static List<Lectura> LecturasDelPeriodoActual(DateOnly inicioActual, DateOnly hoy)
    {
        const decimal lecturaInicial = 48_210m; // EJEMPLO
        const decimal kwhPorDia = 17.2m; // EJEMPLO

        var lecturas = new List<Lectura>();
        for (var fecha = inicioActual; fecha < hoy; fecha = fecha.AddDays(3))
        {
            lecturas.Add(Leer(fecha));
        }

        lecturas.Add(Leer(hoy));
        return lecturas;

        Lectura Leer(DateOnly fecha) => new()
        {
            Fecha = fecha,
            ValorKwh = lecturaInicial + kwhPorDia * (fecha.DayNumber - inicioActual.DayNumber),
            Origen = OrigenLectura.Manual,
        };
    }
}
