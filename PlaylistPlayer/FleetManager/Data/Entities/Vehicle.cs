using System.ComponentModel.DataAnnotations;
using FleetManager.Auth.Model;

namespace FleetManager.Data.Entities;

public class Vehicle
{
    public int Id { get; set; }
    public required string Make { get; set; }
    public required string Model { get; set; }
    public required int Year { get; set; }
    public required string LicensePlate { get; set; }
    public required string Description { get; set; }
    public int CurrentMileage { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }
    public bool IsBlocked { get; set; }

    public ICollection<Trip> Trips { get; set; } = new List<Trip>();
    public ICollection<FuelRecord> FuelRecords { get; set; } = new List<FuelRecord>();

    public ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } =
        new List<MaintenanceRecord>();

    [Required]
    public required string UserId { get; set; }
    public FleetUser User { get; set; } = null!;

    public VehicleDto ToDto()
    {
        return new VehicleDto(
            Id,
            Make,
            Model,
            Year,
            LicensePlate,
            Description,
            CurrentMileage,
            CreatedAt
        );
    }
}
