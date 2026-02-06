using Microsoft.EntityFrameworkCore;
using TimescaleApi.Domain.Entities;

namespace TimescaleApi.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ValueRecord> Values => Set<ValueRecord>();
    public DbSet<ResultRecord> Results => Set<ResultRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ValueRecord>(entity =>
        {
            entity.ToTable("Values");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.FileName).IsRequired();
            entity.Property(v => v.Date).IsRequired();
            entity.Property(v => v.ExecutionTimeSeconds).IsRequired();
            entity.Property(v => v.Value).IsRequired();
            entity.HasIndex(v => v.FileName);
            entity.HasIndex(v => new { v.FileName, v.Date });
        });

        modelBuilder.Entity<ResultRecord>(entity =>
        {
            entity.ToTable("Results");
            entity.HasKey(r => r.Id);
            entity.Property(r => r.FileName).IsRequired();
            entity.Property(r => r.FirstStart).IsRequired();
            entity.HasIndex(r => r.FileName).IsUnique();
            entity.HasIndex(r => r.FirstStart);
        });
    }
}
