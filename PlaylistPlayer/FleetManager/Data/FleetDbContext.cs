using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using FleetManager.Data.Entities;
using Microsoft.EntityFrameworkCore;
using FleetManager.Auth.Model;

namespace FleetManager.Data;

public class FleetDbContext(DbContextOptions<FleetDbContext> options)
    : IdentityDbContext<FleetUser>(options)
{
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
    public DbSet<FuelRecord> FuelRecords { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<UserLocation> UserLocations { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder
            .Entity<Vehicle>()
            .HasMany(v => v.Trips)
            .WithOne(t => t.Vehicle)
            .HasForeignKey(t => t.VehicleId);

        builder
            .Entity<Vehicle>()
            .HasMany(v => v.FuelRecords)
            .WithOne(f => f.Vehicle)
            .HasForeignKey(f => f.VehicleId);

        builder
            .Entity<Vehicle>()
            .HasMany(v => v.MaintenanceRecords)
            .WithOne(m => m.Vehicle)
            .HasForeignKey(m => m.VehicleId);

        builder
            .Entity<MaintenanceRecord>()
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId);

        builder.Entity<UserLocation>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity
                .HasOne(ul => ul.User)
                .WithMany(u => u.Locations)
                .HasForeignKey(ul => ul.UserId)
                .IsRequired();

            entity
                .HasOne(ul => ul.Vehicle)
                .WithMany()
                .HasForeignKey(ul => ul.VehicleId)
                .IsRequired();

            entity
                .HasOne(ul => ul.Trip)
                .WithMany()
                .HasForeignKey(ul => ul.TripId)
                .IsRequired(false);
        });
    }
}
