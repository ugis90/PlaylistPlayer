using System.ComponentModel.DataAnnotations;
using FleetManager.Auth.Model;
using FleetManager.Data.DTOs;

namespace FleetManager.Data.Entities;

public class FuelRecord
{
    public int Id { get; set; }
    public required DateTimeOffset Date { get; set; }
    public required double Gallons { get; set; }
    public required decimal CostPerGallon { get; set; }
    public required decimal TotalCost { get; set; }
    public required int Mileage { get; set; }
    public string Station { get; set; } = string.Empty;
    public bool FullTank { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    [Required]
    public required string UserId { get; set; }
    public FleetUser User { get; set; }

    public FuelRecordDto ToDto()
    {
        return new FuelRecordDto(
            Id,
            Date,
            Gallons,
            CostPerGallon,
            TotalCost,
            Mileage,
            Station,
            FullTank,
            CreatedAt,
            VehicleId
        );
    }
}
