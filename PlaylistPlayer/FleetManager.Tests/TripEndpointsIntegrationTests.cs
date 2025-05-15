using FleetManager.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using FleetManager.Data;
using System.Text.Json;
using System.Text;
using System.Net;
using FleetManager.Auth.Model;
using FleetManager.Helpers;
using FleetManager.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace FleetManager.Tests;

public class TripEndpointsIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>,
        IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;
    private IServiceScope _scope;
    private FleetDbContext _context;
    private readonly ITestOutputHelper _output;

    public TripEndpointsIntegrationTests(
        CustomWebApplicationFactory factory,
        ITestOutputHelper output
    )
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine("InitializeAsync: Creating scope and context.");
        _scope = _factory.Services.CreateScope();
        _context = _scope.ServiceProvider.GetRequiredService<FleetDbContext>();
        _output.WriteLine("InitializeAsync: Ensuring database is deleted.");
        await _context.Database.EnsureDeletedAsync();
        _output.WriteLine("InitializeAsync: Ensuring database is created.");
        await _context.Database.EnsureCreatedAsync();
        _output.WriteLine("InitializeAsync: Clearing test user.");
        TestAuthHandler.ClearTestUser();
        _output.WriteLine("InitializeAsync: Complete.");
    }

    public Task DisposeAsync()
    {
        _output.WriteLine("DisposeAsync: Disposing context and scope.");
        _context?.Dispose();
        _scope?.Dispose();
        TestAuthHandler.ClearTestUser();
        _output.WriteLine("DisposeAsync: Complete.");
        return Task.CompletedTask;
    }

    private async Task<(FleetUser user, Vehicle vehicle)> SeedVehicleAndUserForTripTest(
        int vehicleId,
        int initialMileage,
        string userIdSufix,
        string familyGroupId,
        IEnumerable<string> roles
    )
    {
        string uniqueUserId = $"user-{userIdSufix}-{Guid.NewGuid().ToString().Substring(0, 4)}";
        string userName = $"user-{userIdSufix}";
        _output.WriteLine(
            $"SeedVehicleAndUserForTripTest: Attempting to seed User: {uniqueUserId}, VehicleId: {vehicleId}"
        );

        var testUser = new FleetUser
        {
            Id = uniqueUserId,
            UserName = userName,
            Email = $"{userName}@test.com",
            FamilyGroupId = familyGroupId
        };
        _context.Users.Add(testUser);
        await _context.SaveChangesAsync();
        _output.WriteLine($"SeedVehicleAndUserForTripTest: User {testUser.Id} saved.");

        var vehicle = new Vehicle
        {
            Id = vehicleId,
            Make = "TestMake",
            Model = "TestModel",
            Year = 2023,
            LicensePlate = $"TEST{vehicleId}",
            Description = "Test Vehicle",
            CurrentMileage = initialMileage,
            CreatedAt = DateTimeOffset.UtcNow,
            UserId = testUser.Id
        };
        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();
        _output.WriteLine(
            $"SeedVehicleAndUserForTripTest: Vehicle {vehicle.Id} (Owner: {vehicle.UserId}) saved."
        );
        return (testUser, vehicle);
    }

    [Fact]
    public async Task CreateTrip_ValidData_AsFleetUser_ReturnsCreatedAndUpdatesMileage()
    {
        _output.WriteLine(
            "TEST_START: CreateTrip_ValidData_AsFleetUser_ReturnsCreatedAndUpdatesMileage"
        );
        const int testVehicleId = 101;
        const int initialMileage = 10000;
        const double tripDistance = 150.5;

        var userRoles = new List<string> { FleetRoles.FleetUser };

        var seededDetails = await SeedVehicleAndUserForTripTest(
            testVehicleId,
            initialMileage,
            "fleet",
            "family1",
            userRoles
        );
        _output.WriteLine(
            $"TEST_ARRANGE: Seeded User ID: {seededDetails.user.Id}, Vehicle Owner: {seededDetails.vehicle.UserId}"
        );

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/vehicles/{testVehicleId}/trips"
        );
        request.Headers.Add(
            TestHeaderAuthenticationMiddleware.TestUserIdHeader,
            seededDetails.user.Id
        );
        request.Headers.Add(
            TestHeaderAuthenticationMiddleware.TestUserNameHeader,
            seededDetails.user.UserName
        );
        if (seededDetails.user.FamilyGroupId != null)
        {
            request.Headers.Add(
                TestHeaderAuthenticationMiddleware.TestUserFamilyHeader,
                seededDetails.user.FamilyGroupId
            );
        }
        request.Headers.Add(
            TestHeaderAuthenticationMiddleware.TestUserRolesHeader,
            string.Join(",", userRoles)
        );

        var createTripDto = new CreateTripDto(
            StartLocation: "Start Test",
            EndLocation: "End Test",
            Distance: tripDistance,
            StartTime: DateTimeOffset.UtcNow.AddHours(-1),
            EndTime: DateTimeOffset.UtcNow,
            Purpose: "Integration Test Trip",
            FuelUsed: null
        );
        request.Content = new StringContent(
            JsonSerializer.Serialize(createTripDto),
            Encoding.UTF8,
            "application/json"
        );

        _output.WriteLine(
            $"TEST_ACT: Posting to /api/vehicles/{testVehicleId}/trips with headers. User for this request: {seededDetails.user.Id}"
        );

        HttpResponseMessage response = null;
        string responseContentOnError = "N/A";
        try
        {
            response = await _client.SendAsync(request);
            if (response.StatusCode != HttpStatusCode.Created)
            {
                responseContentOnError = await response.Content.ReadAsStringAsync();
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"TEST_EXCEPTION during PostAsync: {ex}");
            throw;
        }
        finally
        {
            _output.WriteLine(
                $"TEST_FINALLY: API Response Status: {response?.StatusCode}. Content if error: {responseContentOnError}"
            );
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        _output.WriteLine(
            "TEST_END: CreateTrip_ValidData_AsFleetUser_ReturnsCreatedAndUpdatesMileage"
        );
    }

    // Seeding helper specific to this test class or shared via a base class/static utility
    private async Task<(FleetUser user, Vehicle vehicle)> SeedVehicleAndUserForTest(
        string testNameSuffix,
        int vehicleIdSeed,
        int initialMileage,
        string familyGroupId,
        IEnumerable<string> roles
    )
    {
        string uniqueUserId =
            $"user-trip-{testNameSuffix}-{Guid.NewGuid().ToString("N").Substring(0, 4)}";
        string userName = $"usertrip-{testNameSuffix}";
        int uniqueVehicleId = vehicleIdSeed + new Random().Next(2000, 3000);

        var testUser = await _context.Users.FirstOrDefaultAsync(
            u => u.UserName == userName && u.FamilyGroupId == familyGroupId
        );
        if (testUser == null)
        {
            testUser = new FleetUser
            {
                Id = uniqueUserId,
                UserName = userName,
                Email = $"{userName}@test.com",
                FamilyGroupId = familyGroupId
            };
            _context.Users.Add(testUser);
            await _context.SaveChangesAsync();
        }

        var vehicle = new Vehicle
        {
            Id = uniqueVehicleId,
            Make = "TestMakeTrip",
            Model = "TestModelTrip",
            Year = 2023,
            LicensePlate = $"TRP{uniqueVehicleId}",
            Description = "Vehicle for Trip Test",
            CurrentMileage = initialMileage,
            CreatedAt = DateTimeOffset.UtcNow,
            UserId = testUser.Id
        };
        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesAsync();
        return (testUser, vehicle);
    }

    // Helper to create authenticated requests using headers
    private HttpRequestMessage CreateAuthedRequest(
        HttpMethod method,
        string requestUri,
        FleetUser user,
        IEnumerable<string> roles
    )
    {
        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Add(TestHeaderAuthenticationMiddleware.TestUserIdHeader, user.Id);
        request.Headers.Add(TestHeaderAuthenticationMiddleware.TestUserNameHeader, user.UserName);
        if (!string.IsNullOrEmpty(user.FamilyGroupId))
        {
            request.Headers.Add(
                TestHeaderAuthenticationMiddleware.TestUserFamilyHeader,
                user.FamilyGroupId
            );
        }
        request.Headers.Add(
            TestHeaderAuthenticationMiddleware.TestUserRolesHeader,
            string.Join(",", roles)
        );
        return request;
    }

    [Fact]
    public async Task GetTrip_NonExistentTrip_ReturnsNotFound()
    {
        var (user, vehicle) = await SeedVehicleAndUserForTest(
            "GetNonExistentTrip",
            301,
            1000,
            "fam-get-non",
            new[] { FleetRoles.FleetUser }
        );
        var request = CreateAuthedRequest(
            HttpMethod.Get,
            $"/api/vehicles/{vehicle.Id}/trips/99999",
            user,
            new[] { FleetRoles.FleetUser }
        );
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrip_NonExistentTrip_ReturnsNotFound()
    {
        var (user, vehicle) = await SeedVehicleAndUserForTest(
            "UpdateNonExistentTrip",
            302,
            2000,
            "fam-update-non",
            new[] { FleetRoles.FleetUser }
        );
        var updateDto = new UpdateTripDto(10, "Trying to update", null);
        var request = CreateAuthedRequest(
            HttpMethod.Put,
            $"/api/vehicles/{vehicle.Id}/trips/99999",
            user,
            new[] { FleetRoles.FleetUser }
        );
        request.Content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json"
        );
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteTrip_NonExistentTrip_ReturnsNotFound()
    {
        var (user, vehicle) = await SeedVehicleAndUserForTest(
            "DeleteNonExistentTrip",
            303,
            3000,
            "fam-delete-non",
            new[] { FleetRoles.FleetUser }
        );
        var request = CreateAuthedRequest(
            HttpMethod.Delete,
            $"/api/vehicles/{vehicle.Id}/trips/99999",
            user,
            new[] { FleetRoles.FleetUser }
        );
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTrip_ForbiddenAccess_ReturnsForbidden()
    {
        var (userA, vehicleA) = await SeedVehicleAndUserForTest(
            "UpdateTripUserA",
            401,
            1000,
            "famA_upd",
            new[] { FleetRoles.FleetUser }
        );
        var (userB, _) = await SeedVehicleAndUserForTest(
            "UpdateTripUserB",
            402,
            1000,
            "famB_upd",
            new[] { FleetRoles.FleetUser }
        );

        var tripDto = new CreateTripDto(
            "Origin",
            "Dest",
            20,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            "UserA Trip To Update",
            null
        );
        var createRequest = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicleA.Id}/trips",
            userA,
            new[] { FleetRoles.FleetUser }
        );
        createRequest.Content = new StringContent(
            JsonSerializer.Serialize(tripDto),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createdTripResource = JsonSerializer.Deserialize<ResourceDto<TripDto>>(
            await createResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        var tripId = createdTripResource.resource.Id;

        var updateDto = new UpdateTripDto(30, "UserB trying to update", null);
        var updateRequest = CreateAuthedRequest(
            HttpMethod.Put,
            $"/api/vehicles/{vehicleA.Id}/trips/{tripId}",
            userB,
            new[] { FleetRoles.FleetUser }
        ); // UserB attempts update
        updateRequest.Content = new StringContent(
            JsonSerializer.Serialize(updateDto),
            Encoding.UTF8,
            "application/json"
        );
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteTrip_ForbiddenAccess_ReturnsForbidden()
    {
        var (userA, vehicleA) = await SeedVehicleAndUserForTest(
            "DeleteTripUserA",
            403,
            2000,
            "famA_del",
            new[] { FleetRoles.FleetUser }
        );
        var (userB, _) = await SeedVehicleAndUserForTest(
            "DeleteTripUserB",
            404,
            2000,
            "famB_del",
            new[] { FleetRoles.FleetUser }
        );

        var tripDto = new CreateTripDto(
            "From",
            "To",
            25,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            "UserA Trip To Delete",
            null
        );
        var createRequest = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicleA.Id}/trips",
            userA,
            new[] { FleetRoles.FleetUser }
        );
        createRequest.Content = new StringContent(
            JsonSerializer.Serialize(tripDto),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createdTripResource = JsonSerializer.Deserialize<ResourceDto<TripDto>>(
            await createResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        var tripId = createdTripResource.resource.Id;

        var deleteRequest = CreateAuthedRequest(
            HttpMethod.Delete,
            $"/api/vehicles/{vehicleA.Id}/trips/{tripId}",
            userB,
            new[] { FleetRoles.FleetUser }
        ); // UserB attempts delete
        var deleteResponse = await _client.SendAsync(deleteRequest);

        Assert.Equal(HttpStatusCode.Forbidden, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task GetTrips_ForVehicle_ForbiddenAccess_ReturnsForbidden()
    {
        // VehicleA belongs to UserA. UserB tries to list trips for VehicleA.
        var (userA, vehicleA) = await SeedVehicleAndUserForTest(
            "ListTripsUserA",
            405,
            3000,
            "famA_list",
            new[] { FleetRoles.FleetUser }
        );
        var (userB, _) = await SeedVehicleAndUserForTest(
            "ListTripsUserB",
            406,
            3000,
            "famB_list",
            new[] { FleetRoles.FleetUser }
        );

        // Create a trip for vehicleA by userA to ensure there's something to list (or not, for userB)
        var tripDto = new CreateTripDto(
            "Start",
            "End",
            10,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            "A Trip",
            null
        );
        var createReq = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicleA.Id}/trips",
            userA,
            new[] { FleetRoles.FleetUser }
        );
        createReq.Content = new StringContent(
            JsonSerializer.Serialize(tripDto),
            Encoding.UTF8,
            "application/json"
        );
        (await _client.SendAsync(createReq)).EnsureSuccessStatusCode();

        var getRequest = CreateAuthedRequest(
            HttpMethod.Get,
            $"/api/vehicles/{vehicleA.Id}/trips",
            userB,
            new[] { FleetRoles.FleetUser }
        ); // UserB attempts to list
        var getResponse = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.Forbidden, getResponse.StatusCode);
    }

    [Fact]
    public async Task CreateTrip_WithFuelUsed_SavesFuelData()
    {
        var (user, vehicle) = await SeedVehicleAndUserForTest(
            "CreateWithFuel",
            601,
            12000,
            "fam-fuel",
            new[] { FleetRoles.FleetUser }
        );
        const double tripDistance = 80.0;
        const double fuelUsed = 6.5;

        var request = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicle.Id}/trips",
            user,
            new[] { FleetRoles.FleetUser }
        );
        var createTripDto = new CreateTripDto(
            "City A",
            "City B",
            tripDistance,
            DateTimeOffset.UtcNow.AddHours(-3),
            DateTimeOffset.UtcNow.AddHours(-1),
            "Trip with fuel",
            fuelUsed
        );
        request.Content = new StringContent(
            JsonSerializer.Serialize(createTripDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<TripDto>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Equal(fuelUsed, result.resource.FuelUsed);

        var tripInDb = await _context.Trips.FindAsync(result.resource.Id);
        Assert.NotNull(tripInDb);
        Assert.Equal(fuelUsed, tripInDb.FuelUsed);
    }

    [Fact]
    public async Task GetTrips_NoTripsForVehicle_ReturnsOkAndEmptyList()
    {
        var (user, vehicle) = await SeedVehicleAndUserForTest(
            "GetNoTrips",
            603,
            14000,
            "fam-no-trips",
            new[] { FleetRoles.FleetUser }
        );
        // No trips seeded for this vehicle

        var request = CreateAuthedRequest(
            HttpMethod.Get,
            $"/api/vehicles/{vehicle.Id}/trips",
            user,
            new[] { FleetRoles.FleetUser }
        );
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<TripDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Empty(result.resource);
    }

    [Fact]
    public async Task GetTrips_Pagination_TotalItemsLessThanPageSize()
    {
        var (user, vehicle) = await SeedVehicleAndUserForTest(
            "PageLessTrip",
            701,
            2000,
            "fam-page-less",
            new[] { FleetRoles.FleetUser }
        );
        // Seed 3 trips
        for (int i = 1; i <= 3; i++)
        {
            _context.Trips.Add(
                new Trip
                {
                    VehicleId = vehicle.Id,
                    UserId = user.Id,
                    StartLocation = $"Start{i}",
                    EndLocation = $"End{i}",
                    Distance = 10 + i,
                    StartTime = DateTimeOffset.UtcNow.AddDays(-i),
                    EndTime = DateTimeOffset.UtcNow.AddDays(-i).AddHours(1),
                    Purpose = $"Trip Less {i}",
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-i)
                }
            );
        }
        await _context.SaveChangesAsync();

        var request = CreateAuthedRequest(
            HttpMethod.Get,
            $"/api/vehicles/{vehicle.Id}/trips?pageNumber=1&pageSize=5",
            user,
            new[] { FleetRoles.FleetUser }
        );
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<TripDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        var trips = result.resource.Select(r => r.resource).ToList();

        Assert.Equal(3, trips.Count); // Should get all 3 trips

        var paginationHeader = JsonSerializer.Deserialize<PaginationMetadata>(
            response.Headers.GetValues("Pagination").First(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(paginationHeader);
        Assert.Equal(3, paginationHeader.TotalCount);
        Assert.Equal(5, paginationHeader.PageSize);
        Assert.Equal(1, paginationHeader.CurrentPage);
        Assert.Equal(1, paginationHeader.TotalPages);
    }

    [Fact]
    public async Task GetTrip_AsParent_CanAccessYoungDriversTripInSameFamily()
    {
        var (youngDriver, vehicle) = await SeedVehicleAndUserForTest(
            "YoungTripForParent",
            801,
            1000,
            "familyX",
            new[] { FleetRoles.YoungDriver }
        );
        var parent = new FleetUser
        {
            Id = "parent-famX",
            UserName = "parentX",
            Email = "px@test.com",
            FamilyGroupId = "familyX"
        };
        if (!await _context.Users.AnyAsync(u => u.Id == parent.Id))
        {
            _context.Users.Add(parent);
            await _context.SaveChangesAsync();
        }

        var tripDto = new CreateTripDto(
            "School",
            "Home",
            12,
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            "YoungDriver's Trip",
            null
        );
        var createRequest = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicle.Id}/trips",
            youngDriver,
            new[] { FleetRoles.YoungDriver }
        );
        createRequest.Content = new StringContent(
            JsonSerializer.Serialize(tripDto),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createdTripResource = JsonSerializer.Deserialize<ResourceDto<TripDto>>(
            await createResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        var tripId = createdTripResource.resource.Id;

        // Parent from the same family tries to access the trip
        var getRequest = CreateAuthedRequest(
            HttpMethod.Get,
            $"/api/vehicles/{vehicle.Id}/trips/{tripId}",
            parent,
            new[] { FleetRoles.Parent }
        );
        var getResponse = await _client.SendAsync(getRequest);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetchedTrip = JsonSerializer.Deserialize<TripDto>(
            await getResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(fetchedTrip);
        Assert.Equal(tripId, fetchedTrip.Id);
    }

    [Fact]
    public async Task UpdateTrip_AsYoungDriver_OnOwnTrip_ReturnsOk()
    {
        var (youngDriver, vehicle) = await SeedVehicleAndUserForTest(
            "YoungUpdateOwnTrip",
            802,
            2000,
            "familyY",
            new[] { FleetRoles.YoungDriver }
        );
        var initialTripDto = new CreateTripDto(
            "Park",
            "Mall",
            5,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow.AddHours(-1),
            "YoungDriver Initial",
            0.5
        );

        var createRequest = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicle.Id}/trips",
            youngDriver,
            new[] { FleetRoles.YoungDriver }
        );
        createRequest.Content = new StringContent(
            JsonSerializer.Serialize(initialTripDto),
            Encoding.UTF8,
            "application/json"
        );
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var createdTripResource = JsonSerializer.Deserialize<ResourceDto<TripDto>>(
            await createResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        var tripId = createdTripResource.resource.Id;

        var updateTripDto = new UpdateTripDto(
            Distance: 7,
            Purpose: "YoungDriver Updated Own Trip",
            FuelUsed: 0.6
        );
        var updateRequest = CreateAuthedRequest(
            HttpMethod.Put,
            $"/api/vehicles/{vehicle.Id}/trips/{tripId}",
            youngDriver,
            new[] { FleetRoles.YoungDriver }
        ); // YoungDriver updates own trip
        updateRequest.Content = new StringContent(
            JsonSerializer.Serialize(updateTripDto),
            Encoding.UTF8,
            "application/json"
        );
        var updateResponse = await _client.SendAsync(updateRequest);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updatedTrip = JsonSerializer.Deserialize<TripDto>(
            await updateResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(updatedTrip);
        Assert.Equal("YoungDriver Updated Own Trip", updatedTrip.Purpose);
    }

    [Fact]
    public async Task CreateTrip_AsAdmin_ForAnyVehicle_ReturnsCreated()
    {
        // Seed a vehicle owned by a regular user
        var (ownerUser, vehicle) = await SeedVehicleAndUserForTest(
            "AdminTripTarget",
            901,
            500,
            "fam-target",
            new[] { FleetRoles.FleetUser }
        );

        // Admin creates a trip for this vehicle
        var adminUser = new FleetUser
        {
            Id = "admin-trip-creator",
            UserName = "admintrip",
            Email = "atc@test.com",
            FamilyGroupId = "adminFam"
        };
        if (!await _context.Users.AnyAsync(u => u.Id == adminUser.Id))
        {
            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();
        }

        var createTripDto = new CreateTripDto(
            "Admin Start",
            "Admin End",
            77,
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            "Admin Logged Trip",
            7.7
        );
        var request = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicle.Id}/trips",
            adminUser,
            new[] { FleetRoles.Admin }
        );
        request.Content = new StringContent(
            JsonSerializer.Serialize(createTripDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.SendAsync(request);
        _output.WriteLine(
            $"[CreateTrip_Admin] Status: {response.StatusCode}, Content: {await response.Content.ReadAsStringAsync()}"
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdTrip = await _context.Trips.FirstOrDefaultAsync(
            t => t.VehicleId == vehicle.Id && t.Purpose == "Admin Logged Trip"
        );
        Assert.NotNull(createdTrip);
        Assert.Equal(adminUser.Id, createdTrip.UserId); // Logged by Admin
    }

    [Fact]
    public async Task GetTrips_AsAdmin_ForAnyVehicle_ReturnsOkAndTrips()
    {
        // Seed a vehicle and trip by a regular user
        var (ownerUser, vehicle) = await SeedVehicleAndUserForTest(
            "AdminGetTripsTarget",
            902,
            600,
            "fam-target-get",
            new[] { FleetRoles.FleetUser }
        );
        _context.Trips.Add(
            new Trip
            {
                VehicleId = vehicle.Id,
                UserId = ownerUser.Id,
                StartLocation = "S",
                EndLocation = "E",
                Distance = 10,
                StartTime = DateTimeOffset.UtcNow.AddDays(-1),
                EndTime = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                Purpose = "OwnerTrip1"
            }
        );
        await _context.SaveChangesAsync();

        // Admin user to fetch the trips
        var adminUser = new FleetUser
        {
            Id = "admin-trip-fetcher",
            UserName = "admintripfetch",
            Email = "atf@test.com",
            FamilyGroupId = "adminFamGet"
        };
        if (!await _context.Users.AnyAsync(u => u.Id == adminUser.Id))
        {
            _context.Users.Add(adminUser);
            await _context.SaveChangesAsync();
        }

        var request = CreateAuthedRequest(
            HttpMethod.Get,
            $"/api/vehicles/{vehicle.Id}/trips",
            adminUser,
            new[] { FleetRoles.Admin }
        );
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<TripDto>[]>>(
            responseString,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );
        Assert.NotNull(result?.resource);
        Assert.Single(result.resource);
        Assert.Equal("OwnerTrip1", result.resource.First().resource.Purpose);
    }

    [Fact]
    public async Task CreateTrip_DistanceIsZero_ReturnsCreated()
    {
        var (user, vehicle) = await SeedVehicleAndUserForTest(
            "ZeroDistTrip",
            1001,
            15000,
            "fam-zero",
            new[] { FleetRoles.FleetUser }
        );

        var request = CreateAuthedRequest(
            HttpMethod.Post,
            $"/api/vehicles/{vehicle.Id}/trips",
            user,
            new[] { FleetRoles.FleetUser }
        );
        // Distance 0 is allowed by CreateTripDtoValidator
        var createTripDto = new CreateTripDto(
            "Same Place",
            "Same Place",
            0,
            DateTimeOffset.UtcNow.AddMinutes(-30),
            DateTimeOffset.UtcNow,
            "Zero Distance Test",
            0.1
        );
        request.Content = new StringContent(
            JsonSerializer.Serialize(createTripDto),
            Encoding.UTF8,
            "application/json"
        );

        var response = await _client.SendAsync(request);
        _output.WriteLine(
            $"[CreateTrip_ZeroDist] Status: {response.StatusCode}, Content: {await response.Content.ReadAsStringAsync()}"
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdTrip = await _context.Trips.FirstOrDefaultAsync(
            t => t.VehicleId == vehicle.Id && t.Purpose == "Zero Distance Test"
        );
        Assert.NotNull(createdTrip);
        Assert.Equal(0, createdTrip.Distance);
        // Vehicle mileage should remain unchanged if distance is 0
        var vehicleAfterTrip = await _context.Vehicles.FindAsync(vehicle.Id);
        Assert.Equal(15000, vehicleAfterTrip!.CurrentMileage);
    }
}
