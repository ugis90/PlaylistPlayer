using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using FleetManager.Auth.Model;
using FleetManager.Data;
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
                    [FromQuery] string? searchTerm,
                    LinkGenerator linkGenerator,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isYoungDriver = httpContext.User.IsInRole(FleetRoles.YoungDriver);

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;

                    var queryable = dbContext.Vehicles.Include(v => v.User).AsQueryable();

                    if (!isAdmin)
                    {
                        if (isParent || isYoungDriver)
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
                                || (v.Year.ToString().Contains(term))
                        );
                    }

                    queryable = queryable.OrderBy(v => v.CreatedAt);

                    var pagedList = await PagedList<Vehicle>.CreateAsync(
                        queryable,
                        searchParams.PageNumber!.Value,
                        searchParams.PageSize!.Value
                    );

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

        // GET /vehicles/{vehicleId}
        vehiclesGroup
            .MapGet(
                "/vehicles/{vehicleId}",
                [Authorize]
                async (int vehicleId, HttpContext httpContext, FleetDbContext dbContext) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
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
                        return Results.Forbid();

                    return Results.Ok(vehicle.ToDto());
                }
            )
            .WithName("GetVehicle")
            .AddEndpointFilter<ETagFilter>();

        // POST /vehicles
        vehiclesGroup
            .MapPost(
                "/vehicles",
                [Authorize(
                    Roles = $"{FleetRoles.Admin},{FleetRoles.Parent},{FleetRoles.FleetUser},{FleetRoles.YoungDriver}"
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
                    );
                    if (string.IsNullOrEmpty(userId))
                    {
                        return Results.Unauthorized();
                    }

                    var user = await dbContext.Users.FindAsync(userId);
                    if (user == null)
                    {
                        return Results.Unauthorized();
                    }
                    Console.WriteLine($"--- POST /vehicles Passed Initial Checks ---");

                    var vehicle = new Vehicle
                    {
                        Make = dto.Make,
                        Model = dto.Model,
                        Year = dto.Year,
                        LicensePlate = dto.LicensePlate,
                        Description = dto.Description,
                        CurrentMileage = dto.CurrentMileage ?? 0,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UserId = userId
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

        // PUT /vehicles/{vehicleId}
        vehiclesGroup
            .MapPut(
                "/vehicles/{vehicleId}",
                [Authorize]
                async (
                    UpdateVehicleDto dto,
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

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User)
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);
                    if (vehicle == null)
                        return Results.NotFound("Vehicle not found");

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

        // DELETE /vehicles/{vehicleId}
        vehiclesGroup
            .MapDelete(
                "/vehicles/{vehicleId}",
                [Authorize]
                async (int vehicleId, HttpContext httpContext, FleetDbContext dbContext) =>
                {
                    var currentUserId = httpContext.User.FindFirstValue(
                        JwtRegisteredClaimNames.Sub
                    );
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);

                    var vehicle = await dbContext.Vehicles
                        .Include(v => v.User)
                        .FirstOrDefaultAsync(v => v.Id == vehicleId);
                    if (vehicle == null)
                    {
                        return Results.NotFound();
                    }

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
    }

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
            "remove",
            "DELETE"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetTrips", new { vehicleId }),
            "trips",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetFuelRecords", new { vehicleId }),
            "fuelRecords",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetVehicleAnalytics", new { vehicleId }),
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
            linkGenerator.GetUriByName(httpContext, "GetVehicles", new { }),
            "self",
            "GET"
        );

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
