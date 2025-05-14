// FleetManager/FuelRecordEndpoints.cs
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
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using System.Collections.Generic; // Required for Dictionary

namespace FleetManager;

public static class FuelRecordEndpoints
{
    public static void AddFuelRecordApi(this WebApplication app)
    {
        var fuelGroup = app.MapGroup("/api/vehicles/{vehicleId:int}/fuelRecords")
            .AddFluentValidationAutoValidation();

        // GET / (List fuel records for a vehicle)
        fuelGroup
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

                    var queryable = dbContext.FuelRecords
                        .Where(f => f.VehicleId == vehicleId)
                        .AsNoTracking()
                        .OrderByDescending(f => f.Date);
                    var pagedList = await PagedList<FuelRecord>.CreateAsync(
                        queryable,
                        searchParams.PageNumber!.Value,
                        searchParams.PageSize!.Value
                    );
                    var resources = pagedList
                        .Select(
                            record =>
                                new ResourceDto<FuelRecordDto>(
                                    record.ToDto(),
                                    CreateLinksForSingleFuelRecord(
                                            vehicleId,
                                            record.Id,
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
                        "GetFuelRecords"
                    );
                    var collectionLinks = CreateLinksForFuelRecords(
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
                        new ResourceDto<ResourceDto<FuelRecordDto>[]>(resources, collectionLinks)
                    );
                }
            )
            .WithName("GetFuelRecords");

        // GET /{fuelRecordId}
        fuelGroup
            .MapGet(
                "/{fuelRecordId}",
                [Authorize]
                async (
                    int vehicleId,
                    int fuelRecordId,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isTeenager = httpContext.User.IsInRole(FleetRoles.Teenager);

                    var record = await dbContext.FuelRecords
                        .Include(f => f.Vehicle)
                        .ThenInclude(v => v.User)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(f => f.VehicleId == vehicleId && f.Id == fuelRecordId);

                    if (record == null)
                        return Results.NotFound("Fuel record not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isVehicleOwner = record.Vehicle.UserId == userId;
                    var isFamilyMember =
                        (isParent || isTeenager)
                        && record.Vehicle.User != null
                        && record.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isVehicleOwner && !isFamilyMember)
                        return Results.Forbid();

                    return TypedResults.Ok(record.ToDto());
                }
            )
            .WithName("GetFuelRecord");

        // POST /
        fuelGroup
            .MapPost(
                "/",
                [Authorize(
                    Roles = $"{FleetRoles.Admin},{FleetRoles.Parent},{FleetRoles.FleetUser},{FleetRoles.Teenager}"
                )]
                async (
                    [FromRoute] int vehicleId,
                    [FromBody] CreateFuelRecordDto dto,
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

                    var lastFuelRecord = await dbContext.FuelRecords
                        .Where(f => f.VehicleId == vehicleId)
                        .OrderByDescending(f => f.Mileage)
                        .FirstOrDefaultAsync();
                    if (lastFuelRecord != null && dto.Mileage < lastFuelRecord.Mileage)
                        return Results.UnprocessableEntity(
                            new
                            {
                                errors = new
                                {
                                    mileage = new[]
                                    {
                                        "New fuel record mileage cannot be less than previous record"
                                    }
                                }
                            }
                        );
                    if (dto.Mileage < 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "mileage", new[] { "Mileage cannot be negative." } }
                            }
                        );
                    if (dto.Gallons <= 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "gallons", new[] { "Gallons must be positive." } }
                            }
                        );
                    if (dto.CostPerGallon <= 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "costPerGallon", new[] { "Cost per gallon must be positive." } }
                            }
                        );
                    if (dto.TotalCost < 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "totalCost", new[] { "Total cost cannot be negative." } }
                            }
                        );

                    var record = new FuelRecord
                    {
                        Date = dto.Date.ToUniversalTime(),
                        Gallons = dto.Gallons,
                        CostPerGallon = dto.CostPerGallon,
                        TotalCost = dto.TotalCost,
                        Mileage = dto.Mileage,
                        Station = dto.Station ?? string.Empty,
                        FullTank = dto.FullTank,
                        VehicleId = vehicleId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UserId = userId
                    };

                    if (vehicle.CurrentMileage < dto.Mileage)
                    {
                        vehicle.CurrentMileage = dto.Mileage;
                    }

                    dbContext.FuelRecords.Add(record);
                    await dbContext.SaveChangesAsync();

                    var links = CreateLinksForSingleFuelRecord(
                            vehicleId,
                            record.Id,
                            linkGenerator,
                            httpContext
                        )
                        .ToArray();
                    var recordDto = record.ToDto();
                    var resource = new ResourceDto<FuelRecordDto>(recordDto, links);
                    var locationUrl = linkGenerator.GetUriByName(
                        httpContext,
                        "GetFuelRecord",
                        new { vehicleId, fuelRecordId = record.Id }
                    );

                    return TypedResults.Created(locationUrl, resource);
                }
            )
            .WithName("CreateFuelRecord");

        // PUT /{fuelRecordId}
        fuelGroup
            .MapPut(
                "/{fuelRecordId}",
                [Authorize]
                async (
                    int vehicleId,
                    int fuelRecordId,
                    UpdateFuelRecordDto dto,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);

                    var record = await dbContext.FuelRecords
                        .Include(f => f.Vehicle)
                        .ThenInclude(v => v.User)
                        .Include(f => f.User)
                        .FirstOrDefaultAsync(f => f.VehicleId == vehicleId && f.Id == fuelRecordId);

                    if (record == null)
                        return Results.NotFound("Fuel record not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isRecordCreator = record.UserId == userId;
                    var isParentInFamily =
                        isParent
                        && record.Vehicle.User != null
                        && record.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isRecordCreator && !isParentInFamily)
                        return Results.Forbid();

                    // --- Start Filled-in Update Logic ---
                    // Update record properties if provided in DTO
                    bool recalculateCostPerGallon = false;
                    if (dto.Gallons.HasValue)
                    {
                        if (dto.Gallons.Value <= 0)
                            return Results.ValidationProblem(
                                new Dictionary<string, string[]>
                                {
                                    { "gallons", new[] { "Gallons must be positive." } }
                                }
                            );
                        record.Gallons = dto.Gallons.Value;
                        recalculateCostPerGallon = true; // Need to recalculate if gallons changed
                    }
                    if (dto.TotalCost.HasValue)
                    {
                        if (dto.TotalCost.Value < 0)
                            return Results.ValidationProblem(
                                new Dictionary<string, string[]>
                                {
                                    { "totalCost", new[] { "Total cost cannot be negative." } }
                                }
                            );
                        record.TotalCost = dto.TotalCost.Value;
                        recalculateCostPerGallon = true; // Need to recalculate if total cost changed
                    }

                    // Recalculate CostPerGallon if Gallons and TotalCost are valid and either changed
                    if (recalculateCostPerGallon && record.Gallons > 0 && record.TotalCost >= 0)
                    {
                        record.CostPerGallon = record.TotalCost / (decimal)record.Gallons;
                    }

                    if (dto.Station != null)
                        record.Station = dto.Station; // Allow empty string
                    if (dto.FullTank.HasValue)
                        record.FullTank = dto.FullTank.Value;
                    // --- End Filled-in Update Logic ---

                    await dbContext.SaveChangesAsync();
                    return TypedResults.Ok(record.ToDto());
                }
            )
            .WithName("UpdateFuelRecord");

        // DELETE /{fuelRecordId}
        fuelGroup
            .MapDelete(
                "/{fuelRecordId}",
                [Authorize]
                async (
                    int vehicleId,
                    int fuelRecordId,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);

                    var record = await dbContext.FuelRecords
                        .Include(f => f.Vehicle)
                        .ThenInclude(v => v.User)
                        .Include(f => f.User)
                        .FirstOrDefaultAsync(f => f.VehicleId == vehicleId && f.Id == fuelRecordId);

                    if (record == null)
                        return Results.NotFound("Fuel record not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isRecordCreator = record.UserId == userId;
                    var isParentInFamily =
                        isParent
                        && record.Vehicle.User != null
                        && record.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isRecordCreator && !isParentInFamily)
                        return Results.Forbid();

                    dbContext.FuelRecords.Remove(record);
                    await dbContext.SaveChangesAsync();
                    return Results.NoContent();
                }
            )
            .WithName("DeleteFuelRecord");
    }

    // --- Helper Methods for Links ---
    private static IEnumerable<LinkDto> CreateLinksForSingleFuelRecord(
        int vehicleId,
        int fuelRecordId,
        LinkGenerator linkGenerator,
        HttpContext httpContext
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "GetFuelRecord",
                new { vehicleId, fuelRecordId }
            ),
            "self",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "UpdateFuelRecord",
                new { vehicleId, fuelRecordId }
            ),
            "edit",
            "PUT"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "DeleteFuelRecord",
                new { vehicleId, fuelRecordId }
            ),
            "delete",
            "DELETE"
        );
    }

    private static IEnumerable<LinkDto> CreateLinksForFuelRecords(
        int vehicleId,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        string? previousPageLink,
        string? nextPageLink
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetFuelRecords", new { vehicleId }),
            "self",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "CreateFuelRecord", new { vehicleId }),
            "create",
            "POST"
        );
        if (!string.IsNullOrWhiteSpace(previousPageLink))
            yield return new LinkDto(previousPageLink, "previousPage", "GET");
        if (!string.IsNullOrWhiteSpace(nextPageLink))
            yield return new LinkDto(nextPageLink, "nextPage", "GET");
    }
}
