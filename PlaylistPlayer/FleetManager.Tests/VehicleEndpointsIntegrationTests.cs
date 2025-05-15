using System.Net;
using System.Text;
using System.Text.Json;
using FleetManager.Auth.Model;
using FleetManager.Data;
using FleetManager.Data.Entities;
using FleetManager.Helpers;
using FleetManager.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace FleetManager.Tests;

public class VehicleEndpointsIntegrationTests(
    CustomWebApplicationFactory factory,
    ITestOutputHelper output
) : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly HttpClient _client = factory.CreateClient();
    private IServiceScope _scope;
    private FleetDbContext _context;

    public async Task InitializeAsync()
    {
        output.WriteLine($"InitializeAsync for test method in {GetType().Name}");
        _scope = factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        await _context.Database.EnsureDeletedAsync();
        await _context.Database.EnsureCreatedAsync();
        output.WriteLine("InitializeAsync: DB cleaned and context ready.");
        await SeedSpecificUsersAndVehiclesForTestingAsync();
        output.WriteLine("InitializeAsync: Seeding complete.");
    }

    public Task DisposeAsync()
    {
        output.WriteLine($"DisposeAsync for test method in {GetType().Name}");
        _scope?.Dispose();
        return Task.CompletedTask;
    }

    private async Task SeedSpecificUsersAndVehiclesForTestingAsync()
    {
        output.WriteLine("SeedSpecificUsersAndVehiclesForTestingAsync: Seeding data...");
        var user1 = new FleetUser
        {
            Id = "user1-parent-fam1",
            UserName = "parent1",
            Email = "p1@test.com",
            FamilyGroupId = "family1"
        };
        var user2 = new FleetUser
        {
            Id = "user2-youngdriver-fam1",
            UserName = "young1",
            Email = "y1@test.com",
            FamilyGroupId = "family1"
        };
        var user3 = new FleetUser
        {
            Id = "user3-parent-fam2",
            UserName = "parent2",
            Email = "p2@test.com",
            FamilyGroupId = "family2"
        };
        var user4 = new FleetUser
        {
            Id = "user4-fleetuser",
            UserName = "fleetuser1",
            Email = "fu1@test.com",
            FamilyGroupId = "family4"
        };
        var user5Admin = new FleetUser
        {
            Id = "user5-admin",
            UserName = "adminuser",
            Email = "admin@test.com",
            FamilyGroupId = "adminFamily"
        };

        _context.Users.AddRange(user1, user2, user3, user4, user5Admin);
        await _context.SaveChangesAsync();

        _context.Vehicles.AddRange(
            new Vehicle
            {
                Id = 1,
                Make = "Toyota",
                Model = "Camry",
                Year = 2020,
                LicensePlate = "FAM1A",
                UserId = user1.Id,
                Description = "d1",
                CreatedAt = DateTimeOffset.UtcNow,
                CurrentMileage = 1000
            },
            new Vehicle
            {
                Id = 2,
                Make = "Honda",
                Model = "Civic",
                Year = 2021,
                LicensePlate = "FAM1B",
                UserId = user2.Id,
                Description = "d2",
                CreatedAt = DateTimeOffset.UtcNow,
                CurrentMileage = 5000
            },
            new Vehicle
            {
                Id = 3,
                Make = "Ford",
                Model = "Focus",
                Year = 2019,
                LicensePlate = "FAM2A",
                UserId = user3.Id,
                Description = "d3",
                CreatedAt = DateTimeOffset.UtcNow,
                CurrentMileage = 20000
            },
            new Vehicle
            {
                Id = 4,
                Make = "Mazda",
                Model = "3",
                Year = 2022,
                LicensePlate = "FLEET1",
                UserId = user4.Id,
                Description = "d4",
                CreatedAt = DateTimeOffset.UtcNow,
                CurrentMileage = 100
            }
        );
        await _context.SaveChangesAsync();
        output.WriteLine("SeedSpecificUsersAndVehiclesForTestingAsync: Seeding complete.");
    }

    private HttpRequestMessage CreateAuthedRequest(
        HttpMethod method,
        string requestUri,
        string userId,
        string userName,
        string? familyGroupId,
        IEnumerable<string> roles
    )
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add(TestHeaderAuthenticationMiddleware.TestUserIdHeader, userId);
        request.Headers.Add(TestHeaderAuthenticationMiddleware.TestUserNameHeader, userName);
        if (!string.IsNullOrEmpty(familyGroupId))
        {
            request.Headers.Add(
                TestHeaderAuthenticationMiddleware.TestUserFamilyHeader,
                familyGroupId
            );
        }
        request.Headers.Add(
            TestHeaderAuthenticationMiddleware.TestUserRolesHeader,
            string.Join(",", roles)
        );
        return request;
    }

    [Fact]
    public async Task GetVehicles_AsParentInFamily1_ReturnsOnlyFamily1VehiclesAndOwn()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles",
            "user1-parent-fam1",
            "parent1",
            "family1",
            new[] { FleetRoles.Parent }
        );
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(result?.resource);
        var vehicles = result.resource.Select(r_dto => r_dto.resource).ToList();
        Assert.Equal(2, vehicles.Count);
        Assert.Contains(vehicles, v => v.Id == 1);
        Assert.Contains(vehicles, v => v.Id == 2);
    }

    [Fact]
    public async Task GetVehicles_AsYoungDriverInFamily1_ReturnsFamily1Vehicles()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles",
            "user2-youngdriver-fam1",
            "young1",
            "family1",
            new[] { FleetRoles.YoungDriver }
        );
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(result?.resource);
        var vehicles = result.resource.Select(r_dto => r_dto.resource).ToList();
        Assert.Equal(2, vehicles.Count);
        Assert.Contains(vehicles, v => v.Id == 1);
        Assert.Contains(vehicles, v => v.Id == 2);
    }

    [Fact]
    public async Task GetVehicles_AsAdmin_ReturnsAllVehicles()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Equal(4, result.resource.Length);
    }

    [Fact]
    public async Task GetVehicles_AsFleetUser_ReturnsOnlyOwnVehicle()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles",
            "user4-fleetuser",
            "fleetuser1",
            "family4",
            new[] { FleetRoles.FleetUser }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        var vehicles = result.resource.Select(r => r.resource).ToList();
        Assert.Single(vehicles);
        Assert.Contains(vehicles, v => v.Id == 4 && v.LicensePlate == "FLEET1");
    }

    [Fact]
    public async Task GetVehicle_ExistingVehicle_AsOwner_ReturnsOkAndVehicleData()
    {
        // Assumes vehicle Id=1 is owned by user1-parent-fam1 from SeedUsersAndVehiclesAsync
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles/1",
            "user1-parent-fam1",
            "parent1",
            "family1",
            new[] { FleetRoles.Parent }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        // Note: GetVehicle returns VehicleDto directly, not wrapped in ResourceDto
        var vehicle = JsonSerializer.Deserialize<VehicleDto>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(vehicle);
        Assert.Equal(1, vehicle.Id);
        Assert.Equal("Toyota", vehicle.Make);
    }

    [Fact]
    public async Task GetVehicle_NonExistentVehicle_ReturnsNotFound()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles/9999",
            "user1-parent-fam1",
            "parent1",
            "family1",
            new[] { FleetRoles.Parent }
        );
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_ValidData_AsParent_ReturnsCreated()
    {
        var createVehicleDto = new CreateVehicleDto(
            "Nissan",
            "Rogue",
            2022,
            "NEW123",
            "New family SUV",
            10
        );
        var content = new StringContent(
            JsonSerializer.Serialize(createVehicleDto),
            Encoding.UTF8,
            "application/json"
        );

        var request = CreateAuthedRequest(
            HttpMethod.Post,
            "/api/vehicles",
            "user1-parent-fam1",
            "parent1",
            "family1",
            new[] { FleetRoles.Parent }
        );
        request.Content = content;
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<VehicleDto>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Equal("Nissan", result.resource.Make);

        var vehicleInDb = await _context.Vehicles.FirstOrDefaultAsync(
            v => v.LicensePlate == "NEW123"
        );
        Assert.NotNull(vehicleInDb);
        Assert.Equal("user1-parent-fam1", vehicleInDb.UserId);
    }

    [Fact]
    public async Task GetVehicles_WithSearchTerm_ReturnsFilteredVehicles()
    {
        // Assumes SeedUsersAndVehiclesAsync has run from InitializeAsync
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles?searchTerm=Camry",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(result?.resource);
        var vehicles = result.resource.Select(r => r.resource).ToList();
        Assert.Single(vehicles);
        Assert.Equal("Camry", vehicles.First().Model);
    }

    [Fact]
    public async Task UpdateVehicle_AsNonOwnerNonAdmin_ReturnsForbidden()
    {
        // Vehicle 1 is owned by user1-parent-fam1
        // user3-parent-fam2 tries to update it
        var updateDto = new UpdateVehicleDto(
            null,
            null,
            null,
            null,
            "Attempted Unauthorized Update",
            15000
        );
        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json"
        );

        var request = CreateAuthedRequest(
            HttpMethod.Put,
            "/api/vehicles/1",
            "user3-parent-fam2",
            "parent2",
            "family2",
            new[] { FleetRoles.Parent }
        );
        request.Content = content;
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteVehicle_AsNonOwnerNonAdmin_ReturnsForbidden()
    {
        // Vehicle 1 is owned by user1-parent-fam1
        var request = CreateAuthedRequest(
            HttpMethod.Delete,
            "/api/vehicles/1",
            "user3-parent-fam2",
            "parent2",
            "family2",
            new[] { FleetRoles.Parent }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateVehicle_MissingMake_ReturnsUnprocessableEntity() // Or BadRequest depending on FluentValidation setup
    {
        // Make is required by CreateVehicleDtoValidator
        var createVehicleDto = new CreateVehicleDto(
            null,
            "Rogue",
            2022,
            "BAD123",
            "New family SUV",
            10
        ); // Make is null
        var content = new StringContent(
            JsonSerializer.Serialize(createVehicleDto),
            Encoding.UTF8,
            "application/json"
        );

        var request = CreateAuthedRequest(
            HttpMethod.Post,
            "/api/vehicles",
            "user1-parent-fam1",
            "parent1",
            "family1",
            new[] { FleetRoles.Parent }
        );
        request.Content = content;
        var response = await _client.SendAsync(request);

        // FluentValidation.AutoValidation typically returns 422 for validation errors
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        output.WriteLine($"CreateVehicle_MissingMake Response: {responseString}");
        Assert.Contains(
            "\"Make\":[\"'Make' must not be empty.\"]",
            responseString,
            StringComparison.OrdinalIgnoreCase
        ); // Check for specific error
    }

    [Fact]
    public async Task GetVehicle_AsOtherUser_NotFamily_NotAdmin_ReturnsForbidden()
    {
        // Vehicle 1 is owned by user1-parent-fam1 (family1)
        // user3-parent-fam2 (family2) tries to access it.
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles/1",
            "user3-parent-fam2",
            "parent2",
            "family2",
            new[] { FleetRoles.Parent }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicles_WithSearchTerm_NoResults_ReturnsOkAndEmptyList()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles?searchTerm=NonExistentSearchTerm123",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(result?.resource);
        Assert.Empty(result.resource);
    }

    [Fact]
    public async Task CreateVehicle_AsYoungDriver_ReturnsCreated()
    {
        // Assuming YoungDriver role is allowed to create vehicles based on your endpoint's Authorize attribute
        var createVehicleDto = new CreateVehicleDto(
            "Subaru",
            "WRX",
            2021,
            "YOUNG1",
            "Young Driver Car",
            50
        );
        var content = new StringContent(
            JsonSerializer.Serialize(createVehicleDto),
            Encoding.UTF8,
            "application/json"
        );

        // user2-youngdriver-fam1 is part of family1
        var request = CreateAuthedRequest(
            HttpMethod.Post,
            "/api/vehicles",
            "user2-youngdriver-fam1",
            "young1",
            "family1",
            new[] { FleetRoles.YoungDriver }
        );
        request.Content = content;
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<VehicleDto>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Equal("Subaru", result.resource.Make);

        var vehicleInDb = await _context.Vehicles.FirstOrDefaultAsync(
            v => v.LicensePlate == "YOUNG1"
        );
        Assert.NotNull(vehicleInDb);
        Assert.Equal("user2-youngdriver-fam1", vehicleInDb.UserId);
    }

    [Fact]
    public async Task GetVehicles_AdminWithPagination_ReturnsCorrectPage()
    {
        var adminUser = await _context.Users.FirstAsync(u => u.Id == "user5-admin");
        for (int i = 5; i <= 12; i++) // Add 8 more vehicles
        {
            _context.Vehicles.Add(
                new Vehicle
                {
                    Id = i,
                    Make = "GenericMake",
                    Model = $"Model{i}",
                    Year = 2020,
                    LicensePlate = $"ADM{i}",
                    UserId = adminUser.Id, // Assign to admin or any user admin can see
                    Description = $"Admin vehicle {i}",
                    CreatedAt = DateTimeOffset.UtcNow,
                    CurrentMileage = 100 * i
                }
            );
        }
        await _context.SaveChangesAsync();

        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles?pageNumber=2&pageSize=5",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        var vehicles = result.resource.Select(r => r.resource).ToList();

        Assert.Equal(5, vehicles.Count); // Should get 5 vehicles for page 2

        // Check pagination header
        Assert.True(response.Headers.Contains("Pagination"));
        var paginationHeader = JsonSerializer.Deserialize<PaginationMetadata>(
            response.Headers.GetValues("Pagination").First(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(paginationHeader);
        Assert.Equal(4 + 8, paginationHeader.TotalCount); // Initial 4 + 8 new ones
        Assert.Equal(5, paginationHeader.PageSize);
        Assert.Equal(2, paginationHeader.CurrentPage);
        Assert.Equal(3, paginationHeader.TotalPages); // 12 items / 5 per page = 2.4 -> 3 pages
    }

    [Fact]
    public async Task GetVehicles_AdminWithPagination_LessThanPageSize_ReturnsAll()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles?pageNumber=1&pageSize=10",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Equal(4, result.resource.Length); // Should return all 4 seeded vehicles

        var paginationHeader = JsonSerializer.Deserialize<PaginationMetadata>(
            response.Headers.GetValues("Pagination").First(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(paginationHeader);
        Assert.Equal(4, paginationHeader.TotalCount);
        Assert.Equal(10, paginationHeader.PageSize);
        Assert.Equal(1, paginationHeader.CurrentPage);
        Assert.Equal(1, paginationHeader.TotalPages);
    }

    [Theory]
    [InlineData("Toyota", 1)] // Vehicle 1 is Toyota Camry
    [InlineData("CAMRY", 1)] // Case-insensitive model search
    [InlineData("FAM1A", 1)] // License plate
    [InlineData("2020", 1)] // Year (Vehicle 1 is 2020)
    [InlineData("d1", 1)] // Description of Vehicle 1
    public async Task GetVehicles_AdminWithSpecificSearchTerm_ReturnsMatchingVehicle(
        string searchTerm,
        int expectedVehicleId
    )
    {
        // Assumes SeedSpecificUsersAndVehiclesForTestingAsync has run from InitializeAsync
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            $"/api/vehicles?searchTerm={searchTerm}",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var responseString = await response.Content.ReadAsStringAsync();
        output.WriteLine($"SearchTerm '{searchTerm}' Response: {responseString}");
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        Assert.NotNull(result?.resource);
        var vehicles = result.resource.Select(r => r.resource).ToList();
        Assert.Single(vehicles); // Expecting only one match for these specific terms
        Assert.Equal(expectedVehicleId, vehicles.First().Id);
    }

    [Fact]
    public async Task GetVehicle_AsParent_CanAccessYoungDriversVehicleInSameFamily()
    {
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles/2",
            "user1-parent-fam1",
            "parent1",
            "family1",
            new[] { FleetRoles.Parent }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vehicle = JsonSerializer.Deserialize<VehicleDto>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(vehicle);
        Assert.Equal(2, vehicle.Id);
    }

    [Fact]
    public async Task GetVehicle_AsYoungDriver_CanAccessOwnVehicle()
    {
        // Vehicle 2 is owned by user2-youngdriver-fam1 (family1)
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles/2",
            "user2-youngdriver-fam1",
            "young1",
            "family1",
            new[] { FleetRoles.YoungDriver }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vehicle = JsonSerializer.Deserialize<VehicleDto>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(vehicle);
        Assert.Equal(2, vehicle.Id);
    }

    [Fact]
    public async Task GetVehicle_AsAdmin_CanAccessAnyVehicle()
    {
        // Vehicle 3 is owned by user3-parent-fam2 (family2)
        // user5-admin (Admin) tries to access it.
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles/3",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var vehicle = JsonSerializer.Deserialize<VehicleDto>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(vehicle);
        Assert.Equal(3, vehicle.Id);
        Assert.Equal("Ford", vehicle.Make); // From SeedSpecificUsersAndVehiclesForTestingAsync
    }

    [Fact]
    public async Task CreateVehicle_AsFleetUser_VehicleAssignedToSelf()
    {
        var createVehicleDto = new CreateVehicleDto(
            "Kia",
            "Sportage",
            2023,
            "FLEET2",
            "FleetUser personal car",
            5
        );
        var content = new StringContent(
            JsonSerializer.Serialize(createVehicleDto),
            Encoding.UTF8,
            "application/json"
        );

        // user4-fleetuser
        var request = CreateAuthedRequest(
            HttpMethod.Post,
            "/api/vehicles",
            "user4-fleetuser",
            "fleetuser1",
            "family4",
            new[] { FleetRoles.FleetUser }
        );
        request.Content = content;
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<VehicleDto>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Equal("Kia", result.resource.Make);

        var vehicleInDb = await _context.Vehicles.FirstOrDefaultAsync(
            v => v.LicensePlate == "FLEET2"
        );
        Assert.NotNull(vehicleInDb);
        Assert.Equal("user4-fleetuser", vehicleInDb.UserId); // Check ownership
    }

    [Fact]
    public async Task CreateVehicle_MinimalValidData_ReturnsCreated()
    {
        var createVehicleDto = new CreateVehicleDto(
            "Mini",
            "Cooper",
            2023,
            "MINI01",
            "Basic car",
            null
        ); // CurrentMileage is nullable
        var content = new StringContent(
            JsonSerializer.Serialize(createVehicleDto),
            Encoding.UTF8,
            "application/json"
        );

        var request = CreateAuthedRequest(
            HttpMethod.Post,
            "/api/vehicles",
            "user1-parent-fam1",
            "parent1",
            "family1",
            new[] { FleetRoles.Parent }
        );
        request.Content = content;
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<VehicleDto>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Equal("Mini", result.resource.Make);
        Assert.Equal(0, result.resource.CurrentMileage); // Should default to 0 if null was passed
    }

    [Fact]
    public async Task UpdateVehicle_PartialUpdate_OnlyOneField_ReturnsOk()
    {
        // Vehicle 4: Mazda 3, CurrentMileage=100, Owner=user4-fleetuser
        var originalVehicle = await _context.Vehicles.AsNoTracking().FirstAsync(v => v.Id == 4);

        var updateDto = new UpdateVehicleDto(
            null,
            null,
            null,
            null,
            "Only description updated",
            null
        ); // Only description
        var content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json"
        );

        var request = CreateAuthedRequest(
            HttpMethod.Put,
            "/api/vehicles/4",
            "user4-fleetuser",
            "fleetuser1",
            "family4",
            new[] { FleetRoles.FleetUser }
        );
        request.Content = content;
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updatedVehicleDto = JsonSerializer.Deserialize<VehicleDto>(
            await response.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(updatedVehicleDto);
        Assert.Equal("Only description updated", updatedVehicleDto.Description);
        Assert.Equal(originalVehicle.Make, updatedVehicleDto.Make); // Make should be unchanged
        Assert.Equal(originalVehicle.CurrentMileage, updatedVehicleDto.CurrentMileage); // Mileage should be unchanged
    }

    [Fact]
    public async Task GetVehicles_AdminSearchTermMatchesMultipleFieldsInOneVehicle_ReturnsThatVehicle()
    {
        var searchUser = await _context.Users.FirstAsync(u => u.Id == "user5-admin");
        var searchableVehicle = new Vehicle
        {
            Id = 99,
            Make = "SearchMake",
            Model = "SearchModel",
            Year = 2020,
            LicensePlate = "SRCH99",
            UserId = searchUser.Id,
            Description = "This Make is very searchable",
            CreatedAt = DateTimeOffset.UtcNow,
            CurrentMileage = 100
        };
        _context.Vehicles.Add(searchableVehicle);
        await _context.SaveChangesAsync();

        var request = CreateAuthedRequest(
            HttpMethod.Get,
            "/api/vehicles?searchTerm=Make",
            "user5-admin",
            "adminuser",
            "adminFamily",
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        var vehicles = result.resource.Select(r => r.resource).ToList();

        Assert.Contains(vehicles, v => v.Id == 99);
    }
}
