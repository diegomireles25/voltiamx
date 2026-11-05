using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Data;

/// <summary>
/// Prepara la base al iniciar la app: aplica las migraciones pendientes y, si no hay tarifas,
/// carga las de ejemplo. Las tarifas son datos editables, así que nunca se sobrescriben.
/// </summary>
public sealed class DbInicializador(VoltiaDbContext db)
{
    public async Task InicializarAsync(CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (await db.Tarifas.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Tarifas.AddRange(TarifasDeEjemplo());
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Rangos mensuales; los servicios los escalan según los meses del periodo.
    /// </summary>
    private static List<Tarifa> TarifasDeEjemplo() =>
    [
        new()
        {
            Nombre = "1",
            Tipo = TipoTarifa.Domestica,
            EsDeEjemplo = true,
            LimiteDacKwhMensual = 250m, // EJEMPLO: verificar con tarifas vigentes de CFE
            Vigencia = new DateOnly(2026, 1, 1), // EJEMPLO: verificar con tarifas vigentes de CFE
            Rangos =
            [
                Rango(1, "Básico", 0m, 75m, 1.00m), // EJEMPLO: verificar con tarifas vigentes de CFE
                Rango(2, "Intermedio", 75m, 140m, 1.25m), // EJEMPLO: verificar con tarifas vigentes de CFE
                Rango(3, "Excedente", 140m, null, 3.50m), // EJEMPLO: verificar con tarifas vigentes de CFE
            ]
        },
        new()
        {
            Nombre = "1C",
            Tipo = TipoTarifa.Domestica,
            EsDeEjemplo = true,
            LimiteDacKwhMensual = 850m, // EJEMPLO: verificar con tarifas vigentes de CFE
            Vigencia = new DateOnly(2026, 1, 1), // EJEMPLO: verificar con tarifas vigentes de CFE
            Rangos =
            [
                Rango(1, "Básico", 0m, 150m, 1.00m), // EJEMPLO: verificar con tarifas vigentes de CFE
                Rango(2, "Intermedio", 150m, 450m, 1.25m), // EJEMPLO: verificar con tarifas vigentes de CFE
                Rango(3, "Excedente", 450m, null, 3.50m), // EJEMPLO: verificar con tarifas vigentes de CFE
            ]
        },
        new()
        {
            Nombre = "1F",
            Tipo = TipoTarifa.Domestica,
            EsDeEjemplo = true,
            LimiteDacKwhMensual = 2500m, // EJEMPLO: verificar con tarifas vigentes de CFE
            Vigencia = new DateOnly(2026, 1, 1), // EJEMPLO: verificar con tarifas vigentes de CFE
            Rangos =
            [
                Rango(1, "Básico", 0m, 300m, 1.00m), // EJEMPLO: verificar con tarifas vigentes de CFE
                Rango(2, "Intermedio", 300m, 1200m, 1.25m), // EJEMPLO: verificar con tarifas vigentes de CFE
                Rango(3, "Excedente", 1200m, null, 3.50m), // EJEMPLO: verificar con tarifas vigentes de CFE
            ]
        },
        new()
        {
            Nombre = "DAC",
            Tipo = TipoTarifa.Dac,
            EsDeEjemplo = true,
            CargoFijoMensual = 100m, // EJEMPLO: verificar con tarifas vigentes de CFE
            Vigencia = new DateOnly(2026, 1, 1), // EJEMPLO: verificar con tarifas vigentes de CFE
            Rangos =
            [
                Rango(1, "Único", 0m, null, 6.00m), // EJEMPLO: verificar con tarifas vigentes de CFE
            ]
        },
        new()
        {
            Nombre = "PDBT",
            Tipo = TipoTarifa.Comercial,
            EsDeEjemplo = true,
            CargoFijoMensual = 60m, // EJEMPLO: verificar con tarifas vigentes de CFE
            Vigencia = new DateOnly(2026, 1, 1), // EJEMPLO: verificar con tarifas vigentes de CFE
            Rangos =
            [
                Rango(1, "Único", 0m, null, 4.00m), // EJEMPLO: verificar con tarifas vigentes de CFE
            ]
        },
    ];

    private static TarifaRango Rango(int orden, string nombre, decimal kwhDesde, decimal? kwhHasta, decimal precioPorKwh)
        => new()
        {
            Orden = orden,
            Nombre = nombre,
            KwhDesde = kwhDesde,
            KwhHasta = kwhHasta,
            PrecioPorKwh = precioPorKwh,
        };
}
