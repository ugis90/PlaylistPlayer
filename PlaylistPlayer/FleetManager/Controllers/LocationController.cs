using System.Security.Claims;
using FleetManager.Auth.Model;
using FleetManager.Data;
using FleetManager.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FleetManager.Controllers;

[ApiController]
[Route("api/locations")]
[Authorize]
public class LocationController(FleetDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UpdateLocation([FromBody] LocationUpdateDto dto)
    {
        try
        {
            // Get current user ID
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            // Create location record
            var location = new UserLocation
            {
                UserId = userId,
                VehicleId = dto.VehicleId,
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Speed = dto.Speed,
                Heading = dto.Heading,
                Timestamp = DateTime.UtcNow,
                TripId = dto.TripId
            };

            dbContext.UserLocations.Add(location);
            await dbContext.SaveChangesAsync();

            return Ok(new { message = "Location updated successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentLocation()
    {
        try
        {
            // Get current user ID
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            // Get the most recent location for the current user
            var location = await dbContext.UserLocations
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync();

            if (location == null)
            {
                return NotFound("No location data found");
            }

            return Ok(location);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpGet("vehicle/{vehicleId}")]
    public async Task<IActionResult> GetVehicleLocation(int vehicleId)
    {
        try
        {
            // Verify user has access to this vehicle
            var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var isAdmin = User.IsInRole(FleetRoles.Admin);
            var isParent = User.IsInRole(FleetRoles.Parent);

            var vehicle = await dbContext.Vehicles.FindAsync(vehicleId);
            if (vehicle == null)
            {
                return NotFound("Vehicle not found");
            }

            // Check permissions - user is owner or admin/parent in same family
            if (vehicle.UserId != userId && !(isAdmin || isParent))
            {
                return Forbid();
            }

            // Get the most recent location for the vehicle
            var location = await dbContext.UserLocations
                .Where(l => l.VehicleId == vehicleId)
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync();

            if (location == null)
            {
                return NotFound("No location data found for this vehicle");
            }

            return Ok(location);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}

public class LocationUpdateDto
{
    public int VehicleId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Speed { get; set; }
    public double? Heading { get; set; }
    public int? TripId { get; set; }
}
