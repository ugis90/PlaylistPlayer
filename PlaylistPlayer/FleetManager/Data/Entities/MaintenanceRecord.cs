// FleetManager/Data/Entities/MaintenanceRecord.cs
using System.ComponentModel.DataAnnotations;
using FleetManager.Auth.Model;
using FleetManager.Data.DTOs; // Assuming DTOs will be updated too

namespace FleetManager.Data.Entities;

public class MaintenanceRecord
{
    public int Id { get; set; }
    public required string ServiceType { get; set; }
    public required string Description { get; set; }
    public required decimal Cost { get; set; }
    public required int Mileage { get; set; } // Mileage at time of service
    public required DateTimeOffset Date { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset? NextServiceDue { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    // --- Removed Trip Relationship ---
    // public int TripId { get; set; }
    // public Trip Trip { get; set; } = null!;

    // --- Added Vehicle Relationship ---
    [Required]
    public required int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    [Required]
    public required string UserId { get; set; } // User who logged the record
    public FleetUser User { get; set; } = null!;

    // --- Update ToDto method ---
    public MaintenanceRecordDto ToDto()
    {
        return new MaintenanceRecordDto(
            Id, ServiceType, Description, Cost, Mileage, Date,
            Provider, NextServiceDue, CreatedAt,
            VehicleId // Pass VehicleId instead of TripId
        );
    }
}