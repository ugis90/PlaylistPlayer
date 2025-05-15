using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using FleetManager.Auth.Model;
using FleetManager.Data;
using FleetManager.Services;

namespace FleetManager;

public static class AnalyticsEndpoints
{
    public static void AddAnalyticsApi(this WebApplication app)
    {
        var analyticsGroup = app.MapGroup("/api");

        analyticsGroup
            .MapGet(
                "/vehicles/{vehicleId}/analytics",
                [Authorize]
                async (
                    int vehicleId,
                    [FromQuery] DateTimeOffset? startDate,
                    [FromQuery] DateTimeOffset? endDate,
                    HttpContext httpContext,
                    FleetDbContext dbContext,
                    AnalyticsService analyticsService
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);

                    // Check if vehicle exists and belongs to user
                    var vehicle = await dbContext.Vehicles.FindAsync(vehicleId);
                    if (vehicle == null || (!isAdmin && vehicle.UserId != userId))
                        return Results.NotFound("Vehicle not found");

                    try
                    {
                        // Get analytics for this vehicle
                        var analytics = await analyticsService.GetVehicleAnalyticsAsync(
                            vehicleId,
                            startDate,
                            endDate
                        );

                        return Results.Ok(analytics);
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(
                            detail: ex.Message,
                            title: "Error generating vehicle analytics",
                            statusCode: 500
                        );
                    }
                }
            )
            .WithName("GetVehicleAnalytics");

        analyticsGroup
            .MapGet(
                "/vehicles/analytics",
                [Authorize]
                async (
                    [FromQuery] DateTimeOffset? startDate,
                    [FromQuery] DateTimeOffset? endDate,
                    HttpContext httpContext,
                    FleetDbContext dbContext,
                    AnalyticsService analyticsService
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

                    try
                    {
                        // Get analytics for fleet (all vehicles for this user)
                        var fleetAnalytics = await analyticsService.GetFleetAnalyticsAsync(
                            userId,
                            startDate,
                            endDate
                        );

                        return Results.Ok(fleetAnalytics);
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem(
                            detail: ex.Message,
                            title: "Error generating fleet analytics",
                            statusCode: 500
                        );
                    }
                }
            )
            .WithName("GetFleetAnalytics");
    }
}
