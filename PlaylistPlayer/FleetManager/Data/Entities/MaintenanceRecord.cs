using System.ComponentModel.DataAnnotations;
using FleetManager.Auth.Model;

namespace FleetManager.Data.Entities;

public class MaintenanceRecord
{
    public int Id { get; set; }
    public required string ServiceType { get; set; }
    public required string Description { get; set; }
    public required decimal Cost { get; set; }
    public required int Mileage { get; set; }
    public required DateTimeOffset Date { get; set; }
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset? NextServiceDue { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    [Required]
    public required int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    [Required]
    public required string UserId { get; set; }
    public FleetUser User { get; set; } = null!;

    public MaintenanceRecordDto ToDto()
    {
        return new MaintenanceRecordDto(
            Id,
            ServiceType,
            Description,
            Cost,
            Mileage,
            Date,
            Provider,
            NextServiceDue,
            CreatedAt,
            VehicleId
        );
    }
}
