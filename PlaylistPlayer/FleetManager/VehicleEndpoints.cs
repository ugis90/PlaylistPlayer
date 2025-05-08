// FleetManager/VehicleEndpoints.cs
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Ensure this is included
using Microsoft.IdentityModel.JsonWebTokens;
using FleetManager.Auth.Model;
using FleetManager.Data;
using FleetManager.Data.DTOs;
using FleetManager.Data.Entities;
using FleetManager.Helpers;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;

namespace FleetManager;

public static class VehicleEndpoints
{
    public static void AddVehicleApi(this WebApplication app)
    {
        var vehiclesGroup = app.MapGroup("/api").AddFluentValidationAutoValidation();

        // GET /vehicles
        vehiclesGroup
            .MapGet(
                "/vehicles",
                [Authorize]
                async (
                    [AsParameters] SearchParameters searchParams,
                    [FromQuery] string? searchTerm, // <-- Add searchTerm parameter
                    LinkGenerator linkGenerator,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isTeenager = httpContext.User.IsInRole(FleetRoles.Teenager);

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;

                    var queryable = dbContext.Vehicles.Include(v => v.User).AsQueryable();

                    // Apply Role Filtering
                    if (!isAdmin)
                    {
                        if (isParent || isTeenager)
                        {
                            queryable = queryable.Where(
                                v =>
                                    v.UserId == userId
                                    || (v.User != null && v.User.FamilyGroupId == familyGroupId)
                            );
                        }
                        else
                        {
                            queryable = queryable.Where(v => v.UserId == userId);
                        }
                    }

                    // --- Apply Search Term Filtering ---
                    if (!string.IsNullOrWhiteSpace(searchTerm))
                    {
                        var term = searchTerm.Trim().ToLower();
                        queryable = queryable.Where(
                            v =>
                                (v.Make != null && v.Make.ToLower().Contains(term))
                                || (v.Model != null && v.Model.ToLower().Contains(term))
                                || (
                                    v.LicensePlate != null
                                    && v.LicensePlate.ToLower().Contains(term)
                                )
                                || (v.Description != null && v.Description.ToLower().Contains(term))
                                || (v.Year.ToString().Contains(term)) // Search year as string
                        );
                    }
                    // --- End Search Term Filtering ---

                    queryable = queryable.OrderBy(v => v.CreatedAt); // Apply ordering *after* filtering

                    var pagedList = await PagedList<Vehicle>.CreateAsync(
                        queryable, // Pass the filtered queryable
                        searchParams.PageNumber!.Value,
                        searchParams.PageSize!.Value
                    );

                    // ... rest of the mapping, pagination header, and return logic ...
                    var resources = pagedList
                        .Select(vehicle =>
                        {
                            var links = CreateLinksForSingleVehicle(
                                    vehicle.Id,
                                    linkGenerator,
                                    httpContext
                                )
                                .ToArray();
                            return new ResourceDto<VehicleDto>(vehicle.ToDto(), links);
                        })
                        .ToArray();
                    var paginationMetadata = pagedList.CreatePaginationMetadata(
                        linkGenerator,
                        httpContext,
                        "GetVehicles"
                    );
                    var collectionLinks = CreateLinksForVehicles(
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
                    return Results.Ok(
                        new ResourceDto<ResourceDto<VehicleDto>[]>(resources, collectionLinks)
                    );
                }
            )
            .WithName("GetVehicles");

        // GET /vehicles/{vehicleId} (Authorization logic seems okay)
        vehiclesGroup
            .MapGet(
                "/vehicles/{vehicleId}",
                [Authorize] // Ensure authorized
                async (int vehicleId, HttpContext httpContext, FleetDbContext dbContext) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isTeenager = httpContext.User.IsInRole(FleetRoles.Teenager); // Check Teenager

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User) // Include User to check FamilyGroupId
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);

                    if (vehicle == null)
                        return Results.NotFound("Vehicle not found");

                    // Check permissions
                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isOwner = vehicle.UserId == userId;
                    var isFamilyMember =
                        (isParent || isTeenager) // Parent or Teenager
                        && vehicle.User != null
                        && vehicle.User.FamilyGroupId == familyGroupId; // In the same family

                    if (!isAdmin && !isOwner && !isFamilyMember)
                        return Results.Forbid(); // Use Forbid for permissions issue

                    return Results.Ok(vehicle.ToDto());
                }
            )
            .WithName("GetVehicle")
            .AddEndpointFilter<ETagFilter>();

        // POST /vehicles (Allow Admin, Parent, FleetUser, and Teenager)
        vehiclesGroup
            .MapPost(
                "/vehicles",
                [Authorize(
                    Roles = $"{FleetRoles.Admin},{FleetRoles.Parent},{FleetRoles.FleetUser},{FleetRoles.Teenager}"
                )]
                async (
                    CreateVehicleDto dto,
                    LinkGenerator linkGenerator,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    Console.WriteLine(
                        $"--- POST /vehicles Check --- User: {userId}, Role Claim: {httpContext.User.FindFirstValue(ClaimTypes.Role)}"
                    ); // Log role claim from token
                    if (string.IsNullOrEmpty(userId))
                    {
                        return Results.Unauthorized(); // Should not happen if [Authorize] works
                    }

                    // Fetch the user to potentially assign FamilyGroupId if needed later
                    var user = await dbContext.Users.FindAsync(userId);
                    if (user == null)
                    {
                        return Results.Unauthorized(); // User must exist
                    }
                    Console.WriteLine($"--- POST /vehicles Passed Initial Checks ---"); // Add this log

                    var vehicle = new Vehicle
                    {
                        Make = dto.Make,
                        Model = dto.Model,
                        Year = dto.Year,
                        LicensePlate = dto.LicensePlate,
                        Description = dto.Description,
                        CurrentMileage = dto.CurrentMileage ?? 0,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UserId = userId // Assign to the user creating it
                        // FamilyGroupId is implicitly linked via the User relationship
                    };

                    dbContext.Vehicles.Add(vehicle);
                    await dbContext.SaveChangesAsync();

                    var links = CreateLinksForSingleVehicle(vehicle.Id, linkGenerator, httpContext)
                        .ToArray();

                    var vehicleDto = vehicle.ToDto();
                    var resource = new ResourceDto<VehicleDto>(vehicleDto, links);

                    var locationUrl = linkGenerator.GetUriByName(
                        httpContext,
                        "GetVehicle",
                        new { vehicleId = vehicle.Id }
                    );

                    return TypedResults.Created(locationUrl, resource);
                }
            )
            .WithName("CreateVehicle");

        // PUT /vehicles/{vehicleId} (Allow Admin, Parent, or Owner)
        vehiclesGroup
            .MapPut(
                "/vehicles/{vehicleId}",
                [Authorize] // General authorization needed
                async (
                    UpdateVehicleDto dto, // Use a specific DTO for updates
                    int vehicleId,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var currentUserId = httpContext.User.FindFirstValue(
                        JwtRegisteredClaimNames.Sub
                    );
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    // Teenagers generally shouldn't update family vehicles unless it's their own

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User) // Include User for permission check
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);
                    if (vehicle == null)
                        return Results.NotFound("Vehicle not found");

                    // Check permissions: Admin, Owner, or Parent in the same family group
                    var currentUser = await dbContext.Users.FindAsync(currentUserId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isOwner = vehicle.UserId == currentUserId;
                    var isParentInFamily =
                        isParent
                        && vehicle.User != null
                        && vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isOwner && !isParentInFamily)
                    {
                        return Results.Forbid();
                    }

                    // Update fields from DTO
                    // Note: UpdateVehicleDto needs to include all fields if you want them editable here
                    if (!string.IsNullOrEmpty(dto.Make))
                        vehicle.Make = dto.Make;
                    if (!string.IsNullOrEmpty(dto.Model))
                        vehicle.Model = dto.Model;
                    if (dto.Year.HasValue)
                        vehicle.Year = dto.Year.Value;
                    if (!string.IsNullOrEmpty(dto.LicensePlate))
                        vehicle.LicensePlate = dto.LicensePlate;
                    if (!string.IsNullOrWhiteSpace(dto.Description))
                        vehicle.Description = dto.Description;

                    if (dto.CurrentMileage.HasValue)
                    {
                        if (dto.CurrentMileage.Value < vehicle.CurrentMileage)
                        {
                            return Results.ValidationProblem(
                                new Dictionary<string, string[]>
                                {
                                    { "currentMileage", new[] { "Mileage cannot be decreased." } }
                                }
                            );
                        }
                        vehicle.CurrentMileage = dto.CurrentMileage.Value;
                    }

                    dbContext.Vehicles.Update(vehicle);
                    await dbContext.SaveChangesAsync();

                    return Results.Ok(vehicle.ToDto());
                }
            )
            .WithName("UpdateVehicle");

        // DELETE /vehicles/{vehicleId} (Allow Admin, Parent, or Owner)
        vehiclesGroup
            .MapDelete(
                "/vehicles/{vehicleId}",
                [Authorize] // General authorization needed
                async (int vehicleId, HttpContext httpContext, FleetDbContext dbContext) =>
                {
                    var currentUserId = httpContext.User.FindFirstValue(
                        JwtRegisteredClaimNames.Sub
                    );
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    // Teenagers generally shouldn't delete family vehicles unless it's their own

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User) // Include User for permission check
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);
                    if (vehicle == null)
                    {
                        return Results.NotFound();
                    }

                    // Check permissions: Admin, Owner, or Parent in the same family group
                    var currentUser = await dbContext.Users.FindAsync(currentUserId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isOwner = vehicle.UserId == currentUserId;
                    var isParentInFamily =
                        isParent
                        && vehicle.User != null
                        && vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isOwner && !isParentInFamily)
                    {
                        return Results.Forbid();
                    }

                    dbContext.Vehicles.Remove(vehicle);
                    await dbContext.SaveChangesAsync();

                    return Results.NoContent();
                }
            )
            .WithName("RemoveVehicle");

        // Removed the duplicate location update endpoint here.
        // It should be handled by LocationController.
    }

    // --- Helper methods for links ---

    private static IEnumerable<LinkDto> CreateLinksForSingleVehicle(
        int vehicleId,
        LinkGenerator linkGenerator,
        HttpContext httpContext
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetVehicle", new { vehicleId }),
            "self",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "UpdateVehicle", new { vehicleId }),
            "edit",
            "PUT"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "RemoveVehicle", new { vehicleId }),
            "remove", // Changed from "delete" to "remove" to match endpoint name
            "DELETE"
        );
        // Link to trips for this vehicle
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetTrips", new { vehicleId }), // Assumes GetTrips endpoint exists and is named
            "trips",
            "GET"
        );
        // Add links to fuel records, maintenance (if applicable at vehicle level)
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetFuelRecords", new { vehicleId }), // Assumes GetFuelRecords endpoint exists
            "fuelRecords",
            "GET"
        );
        // Link to analytics for this vehicle
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetVehicleAnalytics", new { vehicleId }), // Assumes GetVehicleAnalytics endpoint exists
            "analytics",
            "GET"
        );
    }

    private static IEnumerable<LinkDto> CreateLinksForVehicles(
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        string? previousPageLink,
        string? nextPageLink
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "GetVehicles", // Use the correct endpoint name
                new
                { /* Add current query params if needed, e.g., pageNumber, pageSize */
                }
            ),
            "self",
            "GET"
        );

        // Link to create vehicle
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "CreateVehicle"),
            "create",
            "POST"
        );

        if (!string.IsNullOrWhiteSpace(previousPageLink))
            yield return new LinkDto(previousPageLink, "previousPage", "GET");

        if (!string.IsNullOrWhiteSpace(nextPageLink))
            yield return new LinkDto(nextPageLink, "nextPage", "GET");
    }
}

// Removed the LocationData class definition from here.
// Use the UserLocation entity defined elsewhere (e.g., in Data/Entities).
