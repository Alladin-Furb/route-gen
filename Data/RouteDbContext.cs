using Microsoft.EntityFrameworkCore;
using RouteGen.Data.Entities;

namespace RouteGen.Data;

public class RouteDbContext : DbContext
{
    public RouteDbContext(DbContextOptions<RouteDbContext> options) : base(options)
    {
    }

    public DbSet<PontoEmbarque> PontosEmbarque => Set<PontoEmbarque>();

    public DbSet<Rota> Rotas => Set<Rota>();

    public DbSet<ParadaRota> ParadasRota => Set<ParadaRota>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PontoEmbarque>(e =>
        {
            e.ToTable("pontos_embarque");
            e.HasKey(p => p.Id);
            e.HasIndex(p => p.AlunoId).IsUnique();
            e.Property(p => p.Matricula).HasMaxLength(50);
            e.Property(p => p.Nome).HasMaxLength(255);
            e.Property(p => p.Endereco).HasMaxLength(255);
        });

        modelBuilder.Entity<Rota>(e =>
        {
            e.ToTable("rotas");
            e.HasKey(r => r.Id);
            e.Property(r => r.RotaTransporte).HasMaxLength(100);
            e.Property(r => r.DestinoNome).HasMaxLength(255);
            e.HasIndex(r => new { r.VeiculoId, r.Data });
            e.HasMany(r => r.Paradas)
                .WithOne(p => p.Rota!)
                .HasForeignKey(p => p.RotaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ParadaRota>(e =>
        {
            e.ToTable("paradas_rota");
            e.HasKey(p => p.Id);
            e.Property(p => p.Matricula).HasMaxLength(50);
            e.Property(p => p.Nome).HasMaxLength(255);
            // Índice para confirmação rápida e idempotente por (rota, aluno).
            e.HasIndex(p => new { p.RotaId, p.AlunoId }).IsUnique();
        });
    }
}
