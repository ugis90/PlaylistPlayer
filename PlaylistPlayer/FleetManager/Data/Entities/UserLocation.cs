using FleetManager.Auth.Model;

namespace FleetManager.Data.Entities;

public class UserLocation
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int VehicleId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public DateTime Timestamp { get; set; }
    public int? TripId { get; set; }

    // Navigation properties
    public FleetUser User { get; set; } = null!;
    public Vehicle Vehicle { get; set; } = null!;
    public Trip? Trip { get; set; }
}
