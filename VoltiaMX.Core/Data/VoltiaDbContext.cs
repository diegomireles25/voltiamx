using Microsoft.EntityFrameworkCore;
using VoltiaMX.Core.Models;

namespace VoltiaMX.Core.Data;

public class VoltiaDbContext(DbContextOptions<VoltiaDbContext> options) : DbContext(options)
{
    public DbSet<Lugar> Lugares => Set<Lugar>();

    public DbSet<Tarifa> Tarifas => Set<Tarifa>();

    public DbSet<TarifaRango> TarifaRangos => Set<TarifaRango>();

    public DbSet<Lectura> Lecturas => Set<Lectura>();

    public DbSet<Periodo> Periodos => Set<Periodo>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Precios, montos y kWh.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lugar>(lugar =>
        {
            lugar.Property(l => l.Nombre).HasMaxLength(100);
            lugar.Property(l => l.Tipo).HasConversion<string>().HasMaxLength(20);
            lugar.Property(l => l.Estado).HasMaxLength(60);
            lugar.Property(l => l.Municipio).HasMaxLength(100);

            // Una tarifa en uso no se puede borrar.
            lugar.HasOne(l => l.Tarifa)
                .WithMany()
                .HasForeignKey(l => l.TarifaId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Tarifa>(tarifa =>
        {
            tarifa.Property(t => t.Nombre).HasMaxLength(20);
            tarifa.HasIndex(t => t.Nombre).IsUnique();
            tarifa.Property(t => t.Tipo).HasConversion<string>().HasMaxLength(20);

            tarifa.HasMany(t => t.Rangos)
                .WithOne(r => r.Tarifa)
                .HasForeignKey(r => r.TarifaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TarifaRango>(rango =>
        {
            rango.Property(r => r.Nombre).HasMaxLength(40);
            rango.HasIndex(r => new { r.TarifaId, r.Orden }).IsUnique();
        });

        modelBuilder.Entity<Lectura>(lectura =>
        {
            lectura.Property(l => l.Origen).HasConversion<string>().HasMaxLength(20);
            lectura.HasIndex(l => new { l.LugarId, l.Fecha });

            lectura.HasOne(l => l.Lugar)
                .WithMany(l => l.Lecturas)
                .HasForeignKey(l => l.LugarId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Periodo>(periodo =>
        {
            periodo.HasIndex(p => new { p.LugarId, p.Inicio });

            periodo.HasOne(p => p.Lugar)
                .WithMany(l => l.Periodos)
                .HasForeignKey(p => p.LugarId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
