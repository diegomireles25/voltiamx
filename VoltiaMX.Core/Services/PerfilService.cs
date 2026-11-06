using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Data;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Services;

public sealed class PerfilService(IDbContextFactory<VoltiaDbContext> fabrica) : IPerfilService
{
    /// <summary>
    /// Tarifa que se usa cuando la persona no sabe la suya; queda marcada como por confirmar.
    /// </summary>
    /// <summary>
    /// Tarifa que se asigna cuando la persona no sabe la suya (y con la que el onboarding estima un recibo por monto).
    /// </summary>
    public const string TarifaPorDefecto = "1";
    private const decimal PresupuestoMaximoPermitido = 1_000_000m;

    public async Task<PerfilUsuario?> ObtenerPerfilAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await ConsultarPerfil(db).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ModuloApp>> ObtenerModulosActivosAsync(CancellationToken cancellationToken = default)
    {
        // Se calculan con la regla vigente y los usos guardados, no con las filas de PerfilModulos:
        // así un perfil creado antes de un módulo nuevo (p. ej. Metas) lo ve sin repetir el onboarding.
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var perfil = await db.Perfiles
            .AsNoTracking()
            .Include(p => p.Usos)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (perfil is null)
        {
            return [];
        }

        return ModulosPorUso.Calcular(perfil.Usos.Select(u => u.Uso), perfil.ModoAvanzado).Order().ToList();
    }

    public async Task<Lugar?> ObtenerLugarPrincipalAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Lugares
            .AsNoTracking()
            .Include(l => l.Tarifa)
            .Where(l => !l.EsDemo)
            .OrderBy(l => l.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task GuardarOnboardingAsync(DatosOnboarding datos, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(datos);
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);

        var perfil = await ConsultarPerfil(db).FirstOrDefaultAsync(cancellationToken);
        if (perfil is null)
        {
            perfil = new PerfilUsuario();
            db.Perfiles.Add(perfil);
        }

        perfil.OnboardingCompletado = true;
        perfil.NotificacionesActivas = datos.NotificacionesActivas;
        ReemplazarUsos(perfil, datos.Usos);
        ActualizarModulos(perfil);
        ActualizarObjetivo(db, perfil, datos);
        await GuardarLugarPrincipalAsync(db, datos, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task GuardarPresupuestoAsync(decimal? monto, CancellationToken cancellationToken = default)
    {
        if (monto is decimal valor)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valor, nameof(monto));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(valor, PresupuestoMaximoPermitido, nameof(monto));
        }

        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var perfil = await ConsultarPerfil(db).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No hay perfil: el onboarding no se ha completado.");

        if (perfil.Objetivo is null)
        {
            if (monto is null)
            {
                return;
            }

            perfil.Objetivo = new Objetivo { Tipo = TipoObjetivo.NoPasarmeDePresupuesto };
        }

        // Si ya había otro objetivo (p. ej. pagar menos) se conserva; solo cambia el tope.
        perfil.Objetivo.PresupuestoMax = monto;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> CambiarNotificacionesAsync(bool activas, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        return await db.Perfiles.ExecuteUpdateAsync(p => p.SetProperty(x => x.NotificacionesActivas, activas), cancellationToken) > 0;
    }

    public async Task CambiarModoAvanzadoAsync(bool modoAvanzado, CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var perfil = await ConsultarPerfil(db).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Todavía no hay un perfil: falta el onboarding.");

        perfil.ModoAvanzado = modoAvanzado;
        ActualizarModulos(perfil);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReiniciarOnboardingAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await fabrica.CreateDbContextAsync(cancellationToken);
        var perfil = await db.Perfiles.OrderBy(p => p.Id).FirstOrDefaultAsync(cancellationToken);
        if (perfil is null)
        {
            return;
        }

        perfil.OnboardingCompletado = false;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<PerfilUsuario> ConsultarPerfil(VoltiaDbContext db)
        => db.Perfiles
            .Include(p => p.Usos)
            .Include(p => p.Modulos)
            .Include(p => p.Objetivo)
            .AsSplitQuery()
            .OrderBy(p => p.Id);

    // Quita y agrega solo lo que cambió para no chocar con el índice único (PerfilId, Uso).
    private static void ReemplazarUsos(PerfilUsuario perfil, IEnumerable<UsoApp> usos)
    {
        var elegidos = usos.ToHashSet();
        perfil.Usos.RemoveAll(u => !elegidos.Contains(u.Uso));
        foreach (var uso in elegidos.Where(uso => perfil.Usos.All(u => u.Uso != uso)))
        {
            perfil.Usos.Add(new PerfilUso { Uso = uso });
        }
    }

    // Una fila por módulo; solo cambia Activo.
    private static void ActualizarModulos(PerfilUsuario perfil)
    {
        var activos = ModulosPorUso.Calcular(perfil.Usos.Select(u => u.Uso), perfil.ModoAvanzado);
        foreach (var modulo in Enum.GetValues<ModuloApp>())
        {
            var fila = perfil.Modulos.FirstOrDefault(m => m.Modulo == modulo);
            if (fila is null)
            {
                fila = new PerfilModulo { Modulo = modulo };
                perfil.Modulos.Add(fila);
            }

            fila.Activo = activos.Contains(modulo);
        }
    }

    private static void ActualizarObjetivo(VoltiaDbContext db, PerfilUsuario perfil, DatosOnboarding datos)
    {
        if (datos.Objetivo is null && datos.PresupuestoMax is null)
        {
            if (perfil.Objetivo is not null)
            {
                db.Objetivos.Remove(perfil.Objetivo);
                perfil.Objetivo = null;
            }

            return;
        }

        perfil.Objetivo ??= new Objetivo();
        // Un presupuesto sin objetivo elegido se entiende como "no pasarme de mi presupuesto".
        perfil.Objetivo.Tipo = datos.Objetivo ?? TipoObjetivo.NoPasarmeDePresupuesto;
        perfil.Objetivo.PresupuestoMax = datos.PresupuestoMax;
    }

    private static async Task GuardarLugarPrincipalAsync(
        VoltiaDbContext db, DatosOnboarding datos, CancellationToken cancellationToken)
    {
        var lugar = await db.Lugares
            .Include(l => l.Periodos)
            .Include(l => l.Lecturas)
            .Where(l => !l.EsDemo)
            .OrderBy(l => l.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (lugar is null)
        {
            lugar = new Lugar { DiaCorte = 1 };
            db.Lugares.Add(lugar);
        }

        var tipo = TipoDeLugar(datos.Usos);
        lugar.Tipo = tipo;
        lugar.Nombre = string.IsNullOrWhiteSpace(datos.NombreLugar) ? NombrePorTipo(tipo) : datos.NombreLugar.Trim();
        lugar.Estado = datos.Estado;
        lugar.Municipio = datos.Municipio;

        if (datos.TarifaId is int tarifaId)
        {
            lugar.TarifaId = tarifaId;
            lugar.TarifaPorConfirmar = false;
        }
        else
        {
            lugar.TarifaId = await IdTarifaPorDefectoAsync(db, cancellationToken);
            lugar.TarifaPorConfirmar = true;
        }

        // Mensual si la tarifa es comercial o si el recibo es mensual; si no, bimestral. Con recibos guardados ya se
        // contestó "¿Cada cuánto te llega el recibo?" (Fase 9c, D3): repetir el onboarding no la cambia.
        if (lugar.Periodos.Count == 0)
        {
            var tipoTarifa = await db.Tarifas
                .Where(t => t.Id == lugar.TarifaId)
                .Select(t => t.Tipo)
                .SingleAsync(cancellationToken);
            lugar.MesesPorPeriodo = tipoTarifa == TipoTarifa.Comercial || datos.Recibo?.MesesDelPeriodo == 1 ? 1 : 2;
        }

        if (datos.Recibo is { } recibo)
        {
            GuardarPeriodoDelRecibo(lugar, recibo);
        }
        else if (datos.FechaCorteRecibo is DateOnly fechaCorte)
        {
            lugar.DiaCorte = fechaCorte.Day;
        }
    }

    private static void GuardarPeriodoDelRecibo(Lugar lugar, ReciboCapturado recibo)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(recibo.MesesDelPeriodo, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(recibo.MesesDelPeriodo, 2);
        ArgumentOutOfRangeException.ThrowIfNegative(recibo.Kwh);
        ArgumentOutOfRangeException.ThrowIfNegative(recibo.Monto);

        lugar.DiaCorte = recibo.FechaCorte.Day;

        // El recibo cubre los meses del periodo que terminan en la fecha de corte.
        var periodo = lugar.Periodos.FirstOrDefault(p => p.Fin == recibo.FechaCorte);
        if (periodo is null)
        {
            periodo = new Periodo { Fin = recibo.FechaCorte };
            lugar.Periodos.Add(periodo);
        }

        periodo.Inicio = recibo.FechaCorte.AddMonths(-recibo.MesesDelPeriodo).AddDays(1);
        periodo.KwhTotales = recibo.Kwh;
        periodo.Monto = recibo.Monto;
        // El monto real del recibo también va en MontoRecibo: "Recalcular mis periodos" no lo toca.
        periodo.MontoRecibo = recibo.MontoRecibo ?? recibo.Monto;
        periodo.Estimado = recibo.Estimado;

        // La "lectura actual" del recibo es la lectura de corte: base real del siguiente periodo.
        if (recibo.LecturaMedidor is decimal valor)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(valor);
            var lectura = lugar.Lecturas.FirstOrDefault(l => l.Fecha == recibo.FechaCorte);
            if (lectura is null)
            {
                lectura = new Lectura { Fecha = recibo.FechaCorte };
                lugar.Lecturas.Add(lectura);
            }

            lectura.ValorKwh = valor;
            lectura.Origen = OrigenLectura.Manual;
            lectura.EsEstimada = false;
        }
    }

    private static async Task<int> IdTarifaPorDefectoAsync(VoltiaDbContext db, CancellationToken cancellationToken)
    {
        var id = await db.Tarifas
            .Where(t => t.Nombre == TarifaPorDefecto)
            .Select(t => (int?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id
            ?? await db.Tarifas.OrderBy(t => t.Id).Select(t => (int?)t.Id).FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No hay tarifas en la base de datos.");
    }

    // Prioridad: casa, negocio, rentas; quien solo cotiza paneles mide su casa.
    private static TipoLugar TipoDeLugar(IReadOnlyCollection<UsoApp> usos)
        => usos.Contains(UsoApp.Casa) ? TipoLugar.Casa
            : usos.Contains(UsoApp.Negocio) ? TipoLugar.Negocio
            : usos.Contains(UsoApp.Rentas) ? TipoLugar.Propiedad
            : TipoLugar.Casa;

    private static string NombrePorTipo(TipoLugar tipo) => tipo switch
    {
        TipoLugar.Negocio => "Mi negocio",
        TipoLugar.Propiedad => "Mi propiedad",
        _ => "Mi casa",
    };
}
