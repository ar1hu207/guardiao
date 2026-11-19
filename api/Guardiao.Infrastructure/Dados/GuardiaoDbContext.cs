using Guardiao.Domain;
using Guardiao.Domain.Entidades;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Guardiao.Infrastructure.Dados;

public class GuardiaoDbContext(DbContextOptions<GuardiaoDbContext> options) : DbContext(options)
{
    public DbSet<Motoboy> Motoboys => Set<Motoboy>();
    public DbSet<ContatoEmergencia> Contatos => Set<ContatoEmergencia>();
    public DbSet<Ocorrencia> Ocorrencias => Set<Ocorrencia>();
    public DbSet<ZonaDeRisco> ZonasDeRisco => Set<ZonaDeRisco>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Pessoa é abstrata e não vira tabela: Motoboy e ContatoEmergencia herdam Nome e Telefone.
        mb.Entity<Motoboy>(e =>
        {
            e.ToTable("Motoboys");
            e.Property(m => m.Nome).HasMaxLength(120).IsRequired();
            e.Property(m => m.Telefone).HasMaxLength(11).IsRequired();
            e.Property(m => m.Placa).HasMaxLength(7).IsRequired();
            e.Property(m => m.DispositivoId).HasMaxLength(40).IsRequired();
            e.HasIndex(m => m.DispositivoId).IsUnique();
            e.HasIndex(m => m.Placa).IsUnique();

            // Motoboy 1:N Contatos e 1:N Ocorrências, pelas listas privadas (encapsulamento preservado)
            e.HasMany(m => m.Contatos).WithOne().HasForeignKey(c => c.MotoboyId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(m => m.Ocorrencias).WithOne().HasForeignKey(o => o.MotoboyId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(m => m.Contatos).UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(m => m.Ocorrencias).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        mb.Entity<ContatoEmergencia>(e =>
        {
            e.ToTable("ContatosEmergencia");
            e.Property(c => c.Nome).HasMaxLength(120).IsRequired();
            e.Property(c => c.Telefone).HasMaxLength(11).IsRequired();
            e.Property(c => c.Parentesco).HasMaxLength(40).IsRequired();
        });

        mb.Entity<Ocorrencia>(e =>
        {
            e.ToTable("Ocorrencias");
            e.Property(o => o.Tipo).HasConversion<string>().HasMaxLength(12);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(10);
            e.HasIndex(o => new { o.MotoboyId, o.DataHora });
            e.Ignore(o => o.TempoAteAviso);
        });

        mb.Entity<ZonaDeRisco>(e =>
        {
            e.ToTable("ZonasDeRisco");
            e.Property(z => z.Nome).HasMaxLength(200).IsRequired();
            e.Property(z => z.FaixaHoraria).HasConversion<string>().HasMaxLength(10);
            e.Property(z => z.Nivel).HasConversion<string>().HasMaxLength(5);
            e.HasIndex(z => new { z.ZonaId, z.FaixaHoraria }).IsUnique();
            e.HasIndex(z => new { z.FaixaHoraria, z.Latitude, z.Longitude });
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        // SQLite não ordena nem compara DateTimeOffset: grava como texto ISO em UTC (ordenável).
        cb.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetParaTextoUtc>();
        cb.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetParaTextoUtc>();
    }

    private sealed class DateTimeOffsetParaTextoUtc() : ValueConverter<DateTimeOffset, string>(
        v => v.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'"),
        v => DateTimeOffset.Parse(v, System.Globalization.CultureInfo.InvariantCulture,
                                  System.Globalization.DateTimeStyles.AssumeUniversal));
}
