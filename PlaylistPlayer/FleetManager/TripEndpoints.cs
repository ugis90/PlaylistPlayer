﻿using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using FleetManager.Auth.Model;
using FleetManager.Data;
using FleetManager.Data.Entities;
using FleetManager.Helpers;
using System.Text.Json;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

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
                    var isYoungDriver = httpContext.User.IsInRole(FleetRoles.YoungDriver);

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
                        (isParent || isYoungDriver)
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
                    var isYoungDriver = httpContext.User.IsInRole(FleetRoles.YoungDriver);

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
                        (isParent || isYoungDriver)
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
                    Roles = $"{FleetRoles.Admin},{FleetRoles.Parent},{FleetRoles.FleetUser},{FleetRoles.YoungDriver}"
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
                    Console.WriteLine($"--- Endpoint Hit ---");
                    Console.WriteLine(
                        $"User Authenticated: {httpContext.User.Identity?.IsAuthenticated}"
                    );
                    foreach (var claim in httpContext.User.Claims)
                    {
                        Console.WriteLine($"Claim: {claim.Type} = {claim.Value}");
                    }
                    Console.WriteLine($"User ID from Claim: {userId}");
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isYoungDriver = httpContext.User.IsInRole(FleetRoles.YoungDriver);

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User)
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);
                    if (vehicle == null)
                        return Results.NotFound("Vehicle not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isOwner = vehicle.UserId == userId;
                    var isFamilyMember =
                        (isParent || isYoungDriver)
                        && vehicle.User != null
                        && vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isOwner && !isFamilyMember)
                    {
                        Console.WriteLine(
                            $"FORBIDDEN: isAdmin={isAdmin}, isOwner={isOwner}, isFamilyMember={isFamilyMember}"
                        );
                        Console.WriteLine(
                            $"   userIdFromClaims: {userId}, vehicle.UserId: {vehicle.UserId}"
                        );
                        Console.WriteLine($"   isParent: {isParent}, isTeenager: {isYoungDriver}");
                        Console.WriteLine(
                            $"   vehicle.User.FamilyGroupId: {vehicle.User?.FamilyGroupId}, familyGroupIdFromClaimsUser: {familyGroupId}"
                        );
                        return Results.Forbid();
                    }

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

                    var newMileage = vehicle.CurrentMileage + (int)Math.Round(dto.Distance); // Use Math.Round before casting for consistency
                    if (newMileage > vehicle.CurrentMileage)
                    {
                        vehicle.CurrentMileage = newMileage;
                        dbContext.Entry(vehicle).State = EntityState.Modified;
                        Console.WriteLine(
                            $"ENDPOINT_DEBUG: Vehicle {vehicle.Id} mileage set to {vehicle.CurrentMileage}. EntityState: {dbContext.Entry(vehicle).State}"
                        );
                    }
                    dbContext.Trips.Add(trip);
                    Console.WriteLine(
                        $"ENDPOINT_DEBUG: Saving changes. Trip ID (before save): {trip.Id}"
                    );
                    await dbContext.SaveChangesAsync();
                    Console.WriteLine(
                        $"ENDPOINT_DEBUG: SaveChangesAsync complete. Trip ID (after save): {trip.Id}"
                    );

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

                    decimal originalDistance = (decimal)trip.Distance;

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

                    if (dto.Purpose != null)
                    {
                        trip.Purpose = dto.Purpose;
                    }

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
                        trip.FuelUsed = null;
                    }

                    if (dto.Distance.HasValue && (decimal)trip.Distance != originalDistance)
                    {
                        var mileageDifference = (int)trip.Distance - (int)originalDistance;
                        var newVehicleMileage = trip.Vehicle.CurrentMileage + mileageDifference;
                        trip.Vehicle.CurrentMileage = Math.Max(0, newVehicleMileage);
                    }

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
