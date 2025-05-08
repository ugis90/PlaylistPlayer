// FleetManager/Data/FleetDbContext.cs
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using FleetManager.Data.Entities;
using Microsoft.EntityFrameworkCore;
using FleetManager.Auth.Model;

namespace FleetManager.Data;

public class FleetDbContext(IConfiguration configuration) : IdentityDbContext<FleetUser>
{
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Trip> Trips { get; set; }
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
    public DbSet<FuelRecord> FuelRecords { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<UserLocation> UserLocations { get; set; } // CHANGE THIS LINE (or add if missing)

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Ensure connection string is correctly fetched
        var connectionString = configuration.GetConnectionString("PostgreSQL");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("PostgreSQL connection string is not configured.");
        }
        optionsBuilder.UseNpgsql(connectionString);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Vehicle to Trips relationship
        builder
            .Entity<Vehicle>()
            .HasMany(v => v.Trips)
            .WithOne(t => t.Vehicle)
            .HasForeignKey(t => t.VehicleId);

        // Vehicle to FuelRecords relationship
        builder
            .Entity<Vehicle>()
            .HasMany(v => v.FuelRecords)
            .WithOne(f => f.Vehicle)
            .HasForeignKey(f => f.VehicleId);

        // --- ADD Vehicle to MaintenanceRecords relationship ---
        builder
            .Entity<Vehicle>()
            .HasMany(v => v.MaintenanceRecords)
            .WithOne(m => m.Vehicle)
            .HasForeignKey(m => m.VehicleId);

        // --- ADD MaintenanceRecord to User relationship ---
        builder
            .Entity<MaintenanceRecord>()
            .HasOne(m => m.User)
            .WithMany() // User can have many maintenance records they logged
            .HasForeignKey(m => m.UserId);

        // --- Add configuration for UserLocation ---
        builder.Entity<UserLocation>(entity =>
        {
            entity.HasKey(e => e.Id); // Define primary key

            // Relationship to User
            entity
                .HasOne(ul => ul.User)
                .WithMany(u => u.Locations) // Assumes FleetUser has ICollection<UserLocation> Locations
                .HasForeignKey(ul => ul.UserId)
                .IsRequired();

            // Relationship to Vehicle
            entity
                .HasOne(ul => ul.Vehicle)
                .WithMany() // A vehicle can have many location points, but UserLocation doesn't need a collection back to Vehicle directly here.
                .HasForeignKey(ul => ul.VehicleId)
                .IsRequired();

            // Relationship to Trip (Optional)
            entity
                .HasOne(ul => ul.Trip)
                .WithMany() // A trip can have many location points
                .HasForeignKey(ul => ul.TripId)
                .IsRequired(false); // TripId can be null
        });
        // --- End configuration for UserLocation ---
    }
}
