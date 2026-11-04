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

    public DbSet<PerfilUsuario> Perfiles => Set<PerfilUsuario>();

    public DbSet<PerfilUso> PerfilUsos => Set<PerfilUso>();

    public DbSet<PerfilModulo> PerfilModulos => Set<PerfilModulo>();

    public DbSet<Objetivo> Objetivos => Set<Objetivo>();

    public DbSet<Aparato> Aparatos => Set<Aparato>();

    public DbSet<Meta> Metas => Set<Meta>();

    public DbSet<AvisoEnviado> AvisosEnviados => Set<AvisoEnviado>();

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
            tarifa.Property(t => t.Nombre).HasMaxLength(40);
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

        modelBuilder.Entity<PerfilUsuario>(perfil =>
        {
            perfil.Property(p => p.Nombre).HasMaxLength(100);

            perfil.HasMany(p => p.Usos)
                .WithOne(u => u.Perfil)
                .HasForeignKey(u => u.PerfilId)
                .OnDelete(DeleteBehavior.Cascade);

            perfil.HasMany(p => p.Modulos)
                .WithOne(m => m.Perfil)
                .HasForeignKey(m => m.PerfilId)
                .OnDelete(DeleteBehavior.Cascade);

            perfil.HasOne(p => p.Objetivo)
                .WithOne(o => o.Perfil)
                .HasForeignKey<Objetivo>(o => o.PerfilId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PerfilUso>(uso =>
        {
            uso.Property(u => u.Uso).HasConversion<string>().HasMaxLength(20);
            uso.HasIndex(u => new { u.PerfilId, u.Uso }).IsUnique();
        });

        modelBuilder.Entity<PerfilModulo>(modulo =>
        {
            modulo.Property(m => m.Modulo).HasConversion<string>().HasMaxLength(20);
            modulo.HasIndex(m => new { m.PerfilId, m.Modulo }).IsUnique();
        });

        modelBuilder.Entity<Objetivo>(objetivo =>
        {
            objetivo.Property(o => o.Tipo).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<Periodo>(periodo =>
        {
            periodo.HasIndex(p => new { p.LugarId, p.Inicio });

            periodo.HasOne(p => p.Lugar)
                .WithMany(l => l.Periodos)
                .HasForeignKey(p => p.LugarId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Aparato>(aparato =>
        {
            aparato.Property(a => a.Nombre).IsRequired().HasMaxLength(40);
            aparato.Property(a => a.Categoria).HasConversion<string>().HasMaxLength(20);

            aparato.HasOne(a => a.Lugar)
                .WithMany(l => l.Aparatos)
                .HasForeignKey(a => a.LugarId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Una meta por lugar.
        modelBuilder.Entity<Meta>(meta =>
        {
            meta.HasIndex(m => m.LugarId).IsUnique();

            meta.HasOne(m => m.Lugar)
                .WithOne(l => l.Meta)
                .HasForeignKey<Meta>(m => m.LugarId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Cada clave se avisa una sola vez por lugar.
        modelBuilder.Entity<AvisoEnviado>(aviso =>
        {
            aviso.Property(a => a.Tipo).HasConversion<string>().HasMaxLength(20);
            aviso.Property(a => a.Clave).IsRequired().HasMaxLength(80);
            aviso.HasIndex(a => new { a.LugarId, a.Tipo, a.Clave }).IsUnique();

            aviso.HasOne(a => a.Lugar)
                .WithMany(l => l.AvisosEnviados)
                .HasForeignKey(a => a.LugarId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
