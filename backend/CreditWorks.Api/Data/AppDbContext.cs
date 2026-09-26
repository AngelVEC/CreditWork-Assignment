using CreditWorks.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Admin> Admins => Set<Admin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Manufacturer>(entity =>
        {
            entity.Property(m => m.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(m => m.Name).IsUnique();
        });

        modelBuilder.Entity<VehicleCategory>(entity =>
        {
            entity.Property(c => c.Name).IsRequired().HasMaxLength(50);
            entity.HasIndex(c => c.Name).IsUnique();
            entity.Property(c => c.IconKey).IsRequired().HasMaxLength(50);
            entity.Property(c => c.MinWeightKg).HasColumnType("decimal(10,2)");
            entity.Property(c => c.MaxWeightKg).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.Property(v => v.OwnerName).IsRequired().HasMaxLength(200);
            entity.Property(v => v.WeightKg).HasColumnType("decimal(10,2)");
            entity.HasOne(v => v.Manufacturer)
                  .WithMany(m => m.Vehicles)
                  .HasForeignKey(v => v.ManufacturerId)
                  .OnDelete(DeleteBehavior.Restrict);
            // Intentionally: no FK / navigation to VehicleCategory. Category
            // is a computed, logical relationship only (see Vehicle.cs).
        });

        modelBuilder.Entity<Admin>(entity =>
        {
            entity.Property(a => a.Username).IsRequired().HasMaxLength(100);
            entity.HasIndex(a => a.Username).IsUnique();
            entity.Property(a => a.Role).IsRequired().HasMaxLength(50);
        });
    }
}
