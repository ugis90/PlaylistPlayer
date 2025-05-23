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

public static class MaintenanceRecordEndpoints
{
    public static void AddMaintenanceRecordApi(this WebApplication app)
    {
        var maintenanceGroup = app.MapGroup("/api/vehicles/{vehicleId:int}/maintenanceRecords")
            .AddFluentValidationAutoValidation();

        // GET / (List maintenance records for a vehicle)
        maintenanceGroup
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

                    var queryable = dbContext.MaintenanceRecords
                        .Where(m => m.VehicleId == vehicleId)
                        .AsNoTracking()
                        .OrderByDescending(m => m.Date);

                    var pagedList = await PagedList<MaintenanceRecord>.CreateAsync(
                        queryable,
                        searchParams.PageNumber!.Value,
                        searchParams.PageSize!.Value
                    );

                    var resources = pagedList
                        .Select(
                            record =>
                                new ResourceDto<MaintenanceRecordDto>(
                                    record.ToDto(),
                                    CreateLinksForSingleMaintenanceRecord(
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
                        "GetMaintenanceRecordsForVehicle"
                    );

                    var collectionLinks = CreateLinksForMaintenanceRecords(
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
                        new ResourceDto<ResourceDto<MaintenanceRecordDto>[]>(
                            resources,
                            collectionLinks
                        )
                    );
                }
            )
            .WithName("GetMaintenanceRecordsForVehicle");

        // GET /{maintenanceRecordId}
        maintenanceGroup
            .MapGet(
                "/{maintenanceRecordId}",
                [Authorize]
                async (
                    int vehicleId,
                    int maintenanceRecordId,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);
                    var isYoungDriver = httpContext.User.IsInRole(FleetRoles.YoungDriver);

                    var record = await dbContext.MaintenanceRecords
                        .Include(m => m.Vehicle)
                        .ThenInclude(v => v.User)
                        .AsNoTracking()
                        .FirstOrDefaultAsync(
                            m => m.VehicleId == vehicleId && m.Id == maintenanceRecordId
                        );

                    if (record == null)
                        return Results.NotFound("Maintenance record not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isVehicleOwner = record.Vehicle.UserId == userId;
                    var isFamilyMember =
                        (isParent || isYoungDriver)
                        && record.Vehicle.User != null
                        && record.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isVehicleOwner && !isFamilyMember)
                        return Results.Forbid();

                    return TypedResults.Ok(record.ToDto());
                }
            )
            .WithName("GetMaintenanceRecord");

        // POST /
        maintenanceGroup
            .MapPost(
                "/",
                [Authorize(
                    Roles = $"{FleetRoles.Admin},{FleetRoles.Parent},{FleetRoles.FleetUser},{FleetRoles.YoungDriver}"
                )]
                async (
                    [FromRoute] int vehicleId,
                    [FromBody] CreateMaintenanceRecordDto dto,
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

                    // Validation
                    if (dto.Mileage > vehicle.CurrentMileage)
                        return Results.UnprocessableEntity(
                            new
                            {
                                errors = new
                                {
                                    mileage = new[]
                                    {
                                        "Maintenance mileage cannot be greater than current vehicle mileage"
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
                    if (dto.Cost < 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "cost", new[] { "Cost cannot be negative." } }
                            }
                        );
                    if (dto.NextServiceDue.HasValue && dto.NextServiceDue.Value <= dto.Date)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                {
                                    "nextServiceDue",
                                    new[]
                                    {
                                        "Next service due date must be after the service date."
                                    }
                                }
                            }
                        );

                    var record = new MaintenanceRecord
                    {
                        ServiceType = dto.ServiceType,
                        Description = dto.Description,
                        Cost = dto.Cost,
                        Mileage = dto.Mileage,
                        Date = dto.Date.ToUniversalTime(),
                        Provider = dto.Provider ?? string.Empty,
                        NextServiceDue = dto.NextServiceDue?.ToUniversalTime(),
                        VehicleId = vehicleId,
                        CreatedAt = DateTimeOffset.UtcNow,
                        UserId = userId
                    };

                    dbContext.MaintenanceRecords.Add(record);
                    await dbContext.SaveChangesAsync();

                    var links = CreateLinksForSingleMaintenanceRecord(
                            vehicleId,
                            record.Id,
                            linkGenerator,
                            httpContext
                        )
                        .ToArray();
                    var recordDto = record.ToDto();
                    var resource = new ResourceDto<MaintenanceRecordDto>(recordDto, links);
                    var locationUrl = linkGenerator.GetUriByName(
                        httpContext,
                        "GetMaintenanceRecord",
                        new { vehicleId, maintenanceRecordId = record.Id }
                    );

                    return TypedResults.Created(locationUrl, resource);
                }
            )
            .WithName("CreateMaintenanceRecordForVehicle");

        // PUT /{maintenanceRecordId}
        maintenanceGroup
            .MapPut(
                "/{maintenanceRecordId}",
                [Authorize]
                async (
                    int vehicleId,
                    int maintenanceRecordId,
                    UpdateMaintenanceRecordDto dto,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);

                    var record = await dbContext.MaintenanceRecords
                        .Include(m => m.Vehicle)
                        .ThenInclude(v => v.User)
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(
                            m => m.VehicleId == vehicleId && m.Id == maintenanceRecordId
                        );

                    if (record == null)
                        return Results.NotFound("Maintenance record not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isRecordCreator = record.UserId == userId;
                    var isParentInFamily =
                        isParent
                        && record.Vehicle.User != null
                        && record.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isRecordCreator && !isParentInFamily)
                        return Results.Forbid();

                    // Validation
                    if (dto.Mileage.HasValue && dto.Mileage < 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "mileage", new[] { "Mileage cannot be negative." } }
                            }
                        );
                    if (dto.Cost.HasValue && dto.Cost < 0)
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                { "cost", new[] { "Cost cannot be negative." } }
                            }
                        );
                    if (
                        dto.Mileage.HasValue
                        && record.Vehicle != null
                        && dto.Mileage > record.Vehicle.CurrentMileage
                    )
                        return Results.UnprocessableEntity(
                            new
                            {
                                errors = new
                                {
                                    mileage = new[]
                                    {
                                        "Maintenance mileage cannot be greater than current vehicle mileage"
                                    }
                                }
                            }
                        );
                    if (
                        dto.NextServiceDue.HasValue
                        && dto.Date.HasValue
                        && dto.NextServiceDue.Value <= dto.Date.Value
                    )
                        return Results.ValidationProblem(
                            new Dictionary<string, string[]>
                            {
                                {
                                    "nextServiceDue",
                                    new[]
                                    {
                                        "Next service due date must be after the service date."
                                    }
                                }
                            }
                        );

                    // Apply updates
                    if (!string.IsNullOrEmpty(dto.ServiceType))
                        record.ServiceType = dto.ServiceType;
                    if (dto.Description != null)
                        record.Description = dto.Description;
                    if (dto.Cost.HasValue)
                        record.Cost = dto.Cost.Value;
                    if (dto.Mileage.HasValue)
                        record.Mileage = dto.Mileage.Value;
                    if (dto.Date.HasValue)
                        record.Date = dto.Date.Value.ToUniversalTime();
                    if (dto.Provider != null)
                        record.Provider = dto.Provider;
                    record.NextServiceDue = dto.NextServiceDue?.ToUniversalTime();

                    await dbContext.SaveChangesAsync();
                    return TypedResults.Ok(record.ToDto());
                }
            )
            .WithName("UpdateMaintenanceRecord");

        // DELETE /{maintenanceRecordId}
        maintenanceGroup
            .MapDelete(
                "/{maintenanceRecordId}",
                [Authorize]
                async (
                    int vehicleId,
                    int maintenanceRecordId,
                    HttpContext httpContext,
                    FleetDbContext dbContext
                ) =>
                {
                    var userId = httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                    if (string.IsNullOrEmpty(userId))
                        return Results.Unauthorized();

                    var isAdmin = httpContext.User.IsInRole(FleetRoles.Admin);
                    var isParent = httpContext.User.IsInRole(FleetRoles.Parent);

                    var record = await dbContext.MaintenanceRecords
                        .Include(m => m.Vehicle)
                        .ThenInclude(v => v.User)
                        .Include(m => m.User)
                        .FirstOrDefaultAsync(
                            m => m.VehicleId == vehicleId && m.Id == maintenanceRecordId
                        );

                    if (record == null)
                        return Results.NotFound("Maintenance record not found");

                    var currentUser = await dbContext.Users.FindAsync(userId);
                    var familyGroupId = currentUser?.FamilyGroupId;
                    var isRecordCreator = record.UserId == userId;
                    var isParentInFamily =
                        isParent
                        && record.Vehicle.User != null
                        && record.Vehicle.User.FamilyGroupId == familyGroupId;

                    if (!isAdmin && !isRecordCreator && !isParentInFamily)
                        return Results.Forbid();

                    dbContext.MaintenanceRecords.Remove(record);
                    await dbContext.SaveChangesAsync();
                    return Results.NoContent();
                }
            )
            .WithName("DeleteMaintenanceRecord");
    }

    private static IEnumerable<LinkDto> CreateLinksForSingleMaintenanceRecord(
        int vehicleId,
        int maintenanceRecordId,
        LinkGenerator linkGenerator,
        HttpContext httpContext
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "GetMaintenanceRecord",
                new { vehicleId, maintenanceRecordId }
            ),
            "self",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "UpdateMaintenanceRecord",
                new { vehicleId, maintenanceRecordId }
            ),
            "edit",
            "PUT"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "DeleteMaintenanceRecord",
                new { vehicleId, maintenanceRecordId }
            ),
            "delete",
            "DELETE"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(httpContext, "GetVehicle", new { vehicleId }),
            "vehicle",
            "GET"
        );
    }

    private static IEnumerable<LinkDto> CreateLinksForMaintenanceRecords(
        int vehicleId,
        LinkGenerator linkGenerator,
        HttpContext httpContext,
        string? previousPageLink,
        string? nextPageLink
    )
    {
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "GetMaintenanceRecordsForVehicle",
                new { vehicleId }
            ),
            "self",
            "GET"
        );
        yield return new LinkDto(
            linkGenerator.GetUriByName(
                httpContext,
                "CreateMaintenanceRecordForVehicle",
                new { vehicleId }
            ),
            "create",
            "POST"
        );
        if (!string.IsNullOrWhiteSpace(previousPageLink))
            yield return new LinkDto(previousPageLink, "previousPage", "GET");
        if (!string.IsNullOrWhiteSpace(nextPageLink))
            yield return new LinkDto(nextPageLink, "nextPage", "GET");
    }
}
