// FleetManager/TripEndpoints.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using FleetManager.Auth.Model;
using FleetManager.Data;
using FleetManager.Data.Entities;
using FleetManager.Helpers;
using System.Text.Json;
using FleetManager.Data.DTOs;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using System.Collections.Generic; // Required for Dictionary

namespace FleetManager;

public static class TripEndpoints
{
    public static void AddTripApi(this WebApplication app)
    {
        var tripsGroup = app.MapGroup("/api/vehicles/{vehicleId:int}/trips")
            .AddFluentValidationAutoValidation();

        // GET / (List trips for a vehicle)
        tripsGroup
            .MapGet(
                "/",
                [Authorize]
                async (
                    [FromRoute] int vehicleId,
                    [AsParameters] SearchParameters searchParams,
                    LinkGenerator linkGenerator,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isTeenager = httpContext.User.IsInRole(FleetRoles.Teenager);

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);
                    if (vehicle == null)
                        return Results.NotFound("Vehicle not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isOwner = vehicle.UserId == userId;
                    var isFamilyMember =
                        (isParent || isTeenager)
                        && vehicle.User != null
                        && vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isOwner && !isFamilyMember)
                        return Results.Forbid();

                    var queryable = dbContext.Trips
                        .Where(t => t.VehicleId == vehicleId)
                        .AsNoTracking()
                        .OrderByDescending(t => t.StartTime);
                    var pagedList = await PagedList<Trip>.CreateAsync(
                        queryable,
                        searchParams.PageNumber!.Value,
                        searchParams.PageSize!.Value
                    );
                    var resources = pagedList
                        .Select(
                            trip =>
                                new ResourceDto<TripDto>(
                                    trip.ToDto(),
                                    CreateLinksForSingleTrip(
                                            vehicleId,
                                            trip.Id,
                                            linkGenerator,
                                            httpContext
                                        )
                                        .ToArray()
                                )
                        )
                        .ToArray();
                    var paginationMetadata = pagedList.CreatePaginationMetadata(
                        linkGenerator,
                        httpContext,
                        "GetTrips"
                    );
                    var collectionLinks = CreateLinksForTrips(
                            vehicleId,
                            linkGenerator,
                            httpContext,
                            paginationMetadata.PreviousPageLink,
                            paginationMetadata.NextPageLink
                        )
                        .ToArray();

                    httpContext.Response.Headers.Append(
                        "Pagination",
                        JsonSerializer.Serialize(
                            paginationMetadata,
                            new JsonSerializerOptions
                            {
                                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                            }
                        )
                    );
                    httpContext.Response.Headers.Append(
                        "Access-Control-Expose-Headers",
                        "Pagination"
                    );

                    return TypedResults.Ok(
                        new ResourceDto<ResourceDto<TripDto>[]>(resources, collectionLinks)
                    );
                }
            )
            .WithName("GetTrips");

        // GET /{tripId} (Get a specific trip)
        tripsGroup
            .MapGet(
                "/{tripId}",
                [Authorize]
                async (
                    int vehicleId,
                    int tripId,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isTeenager = httpContext.User.IsInRole(FleetRoles.Teenager);

                    var trip = await dbContext.Trips
                        .Include(t => t.Vehicle)
                        .ThenInclude(v => v.User)
                        .Include(t => t.User)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.VehicleId == vehicleId && t.Id == tripId);
                    if (trip == null)
                        return Results.NotFound("Trip not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isVehicleOwner = trip.Vehicle.UserId == userId;
                    var isFamilyMember =
                        (isParent || isTeenager)
                        && trip.Vehicle.User != null
                        && trip.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isVehicleOwner && !isFamilyMember)
                        return Results.Forbid();

                    return TypedResults.Ok(trip.ToDto());
                }
            )
            .WithName("GetTrip");

        // POST / (Create a new trip)
        tripsGroup
            .MapPost(
                "/",
                [Authorize(
                    Roles = $"{FleetRoles.Admin},{FleetRoles.Parent},{FleetRoles.FleetUser},{FleetRoles.Teenager}"
                )]
                async (
                    [FromRoute] int vehicleId,
                    [FromBody] CreateTripDto dto,
                    HttpContext httpContext,
                    LinkGenerator linkGenerator,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isTeenager = httpContext.User.IsInRole(FleetRoles.Teenager);

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User)
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);
                    if (vehicle == null)
                        return Results.NotFound("Vehicle not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isOwner = vehicle.UserId == userId;
                    var isFamilyMember =
                        (isParent || isTeenager)
                        && vehicle.User != null
                        && vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isOwner && !isFamilyMember)
                        return Results.Forbid();

                    // Basic validation for DTO values
                    if (dto.Distance < 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "distance", new[] { "Distance cannot be negative." } }
                            }
                        );
                    if (dto.EndTime <= dto.StartTime)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "endTime", new[] { "End time must be after start time." } }
                            }
                        );
                    if (dto.FuelUsed.HasValue && dto.FuelUsed < 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "fuelUsed", new[] { "Fuel used cannot be negative." } }
                            }
                        );

                    var trip = new Trip
                    {
                        StartLocation = dto.StartLocation,
                        EndLocation = dto.EndLocation,
                        Distance = dto.Distance,
                        StartTime = dto.StartTime.ToUniversalTime(),
                        EndTime = dto.EndTime.ToUniversalTime(),
                        Purpose = dto.Purpose ?? string.Empty,
                        FuelUsed = dto.FuelUsed,
                        VehicleId = vehicleId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UserId = userId
                    };

                    var newMileage = vehicle.CurrentMileage + (int)dto.Distance;
                    if (newMileage > vehicle.CurrentMileage)
                        vehicle.CurrentMileage = newMileage;

                    dbContext.Trips.Add(trip);
                    await dbContext.SaveChangesAsync();

                    var links = CreateLinksForSingleTrip(
                            vehicleId,
                            trip.Id,
                            linkGenerator,
                            httpContext
                        )
                        .ToArray();
                    var tripDto = trip.ToDto();
                    var resource = new ResourceDto<TripDto>(tripDto, links);
                    var locationUrl = linkGenerator.GetUriByName(
                        httpContext,
                        "GetTrip",
                        new { vehicleId, tripId = trip.Id }
                    );

                    return TypedResults.Created(locationUrl, resource);
                }
            )
            .WithName("CreateTrip");

        // PUT /{tripId} (Update a trip)
        tripsGroup
            .MapPut(
                "/{tripId}",
                [Authorize]
                async (
                    int vehicleId,
                    int tripId,
                    UpdateTripDto dto,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);

                    var trip = await dbContext.Trips
                        .Include(t => t.Vehicle)
                        .ThenInclude(v => v.User)
                        .Include(t => t.User)
                        .FirstOrDefaultAsync(t => t.VehicleId == vehicleId && t.Id == tripId);
                    if (trip == null)
                        return Results.NotFound("Trip not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isTripCreator = trip.UserId == userId;
                    var isParentInFamily =
                        isParent
                        && trip.Vehicle.User != null
                        && trip.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isTripCreator && !isParentInFamily)
                        return Results.Forbid();

                    // --- Start Filled-in Update Logic ---
                    decimal originalDistance = (decimal)trip.Distance; // Store original distance for mileage calc

                    // Update Distance if provided and valid
                    if (dto.Distance.HasValue)
                    {
                        if (dto.Distance.Value < 0)
                            return Results.ValidationProblem(
                                new Dictionary<string, string[]>
                                {
                                    { "distance", new[] { "Distance cannot be negative." } }
                                }
                            );
                        trip.Distance = dto.Distance.Value;
                    }

                    // Update Purpose if provided (allow empty string)
                    if (dto.Purpose != null)
                    {
                        trip.Purpose = dto.Purpose;
                    }

                    // Update FuelUsed if provided and valid
                    if (dto.FuelUsed.HasValue)
                    {
                        if (dto.FuelUsed.Value < 0)
                            return Results.ValidationProblem(
                                new Dictionary<string, string[]>
                                {
                                    { "fuelUsed", new[] { "Fuel used cannot be negative." } }
                                }
                            );
                        trip.FuelUsed = dto.FuelUsed;
                    }
                    else
                    {
                        // If explicitly set to null in DTO (or not provided), set to null in entity
                        trip.FuelUsed = null;
                    }

                    // Adjust vehicle mileage if distance changed
                    if (dto.Distance.HasValue && (decimal)trip.Distance != originalDistance)
                    {
                        var mileageDifference = (int)trip.Distance - (int)originalDistance;
                        var newVehicleMileage = trip.Vehicle.CurrentMileage + mileageDifference;
                        trip.Vehicle.CurrentMileage = Math.Max(0, newVehicleMileage); // Prevent negative mileage
                        // EF Core tracks this change on the related Vehicle entity
                    }
                    // --- End Filled-in Update Logic ---

                    await dbContext.SaveChangesAsync();
                    return TypedResults.Ok(trip.ToDto());
                }
            )
            .WithName("UpdateTrip");

        // DELETE /{tripId} (Delete a trip)
        tripsGroup
            .MapDelete(
                "/{tripId}",
                [Authorize]
                async (
                    int vehicleId,
                    int tripId,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);

                    var trip = await dbContext.Trips
                        .Include(t => t.Vehicle)
                        .ThenInclude(v => v.User)
                        .Include(t => t.User)
                        .FirstOrDefaultAsync(t => t.VehicleId == vehicleId && t.Id == tripId);
                    if (trip == null)
                        return Results.NotFound("Trip not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isTripCreator = trip.UserId == userId;
                    var isParentInFamily =
                        isParent
                        && trip.Vehicle.User != null
                        && trip.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isTripCreator && !isParentInFamily)
                        return Results.Forbid();

                    var newVehicleMileage = trip.Vehicle.CurrentMileage - (int)trip.Distance;
                    trip.Vehicle.CurrentMileage = Math.Max(0, newVehicleMileage);

                    dbContext.Trips.Remove(trip);
                    await dbContext.SaveChangesAsync();
                    return Results.NoContent();
                }
            )
            .WithName("DeleteTrip");
    }

    // --- Helper Methods for Links ---
    private static IEnumerable<LinkDto> CreateLinksForSingleTrip(
        int vehicleId,
        int tripId,
        LinkGenerator linkGenerator,
        HttpContext httpContext
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetTrip", new { vehicleId, tripId }),
            "self",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "UpdateTrip", new { vehicleId, tripId }),
            "edit",
            "PUT"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "DeleteTrip", new { vehicleId, tripId }),
            "delete",
            "DELETE"
        );
    }

    private static IEnumerable<LinkDto> CreateLinksForTrips(
        int vehicleId,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        string? previousPageLink,
        string? nextPageLink
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetTrips", new { vehicleId }),
            "self",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "CreateTrip", new { vehicleId }),
            "create",
            "POST"
        );
        if (!string.IsNullOrWhiteSpace(previousPageLink))
            yield return new LinkDto(previousPageLink, "previousPage", "GET");
        if (!string.IsNullOrWhiteSpace(nextPageLink))
            yield return new LinkDto(nextPageLink, "nextPage", "GET");
    }
}
