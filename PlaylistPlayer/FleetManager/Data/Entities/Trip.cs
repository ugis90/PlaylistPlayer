// FleetManager/Data/Entities/Trip.cs
using System.ComponentModel.DataAnnotations;
using FleetManager.Auth.Model;
using FleetManager.Data.DTOs;
using System.Collections.Generic; // Keep if other collections exist

namespace FleetManager.Data.Entities;

public class Trip
{
    public int Id { get; set; }
    public required string StartLocation { get; set; }
    public required string EndLocation { get; set; }
    public required double Distance { get; set; }
    public required DateTimeOffset StartTime { get; set; }
    public required DateTimeOffset EndTime { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public double? FuelUsed { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    // --- Removed Maintenance Records Relationship ---
    // public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();

    [Required]
    public required string UserId { get; set; }
    public FleetUser User { get; set; } = null!; // Ensure User is not null

    public TripDto ToDto()
    {
        return new TripDto(
            Id,
            StartLocation,
            EndLocation,
            Distance,
            StartTime,
            EndTime,
            Purpose,
            FuelUsed,
            CreatedAt,
            VehicleId,
            UserId
        );
    }
}
