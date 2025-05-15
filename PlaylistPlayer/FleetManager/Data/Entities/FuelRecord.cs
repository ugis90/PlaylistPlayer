using System.ComponentModel.DataAnnotations;
using FleetManager.Auth.Model;

namespace FleetManager.Data.Entities;

public class FuelRecord
{
    public int Id { get; set; }
    public required DateTimeOffset Date { get; set; }
    public required double Liters { get; set; }
    public required decimal CostPerLiter { get; set; }
    public required decimal TotalCost { get; set; }
    public required int Mileage { get; set; }
    public string Station { get; set; } = string.Empty;
    public bool FullTank { get; set; }
    public required DateTimeOffset CreatedAt { get; set; }

    public int VehicleId { get; set; }
    public Vehicle Vehicle { get; set; } = null!;

    [Required]
    public required string UserId { get; set; }
    public FleetUser User { get; set; } = null!;

    public FuelRecordDto ToDto()
    {
        return new FuelRecordDto(
            Id,
            Date,
            Liters,
            CostPerLiter,
            TotalCost,
            Mileage,
            Station,
            FullTank,
            CreatedAt,
            VehicleId
        );
    }
}
