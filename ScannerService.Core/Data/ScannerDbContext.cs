using Microsoft.EntityFrameworkCore;
using ScannerService.Core.Models;

namespace ScannerService.Core.Data;

public class ScannerDbContext : DbContext
{
    public ScannerDbContext(DbContextOptions<ScannerDbContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles { get; set; }
    public DbSet<AppSettings> AppSettings { get; set; }
    public DbSet<ExportSettings> ExportSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ExportSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<AppSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        // Seed default output settings
        modelBuilder.Entity<ExportSettings>().HasData(new ExportSettings
        {
            Id = 1,
            OutputFormat = "PDF",
            OutputDirectory = "",
            FileName = "scan_{datetime}"
        });
    }
}