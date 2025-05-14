using FleetManager.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using FleetManager.Data;
using System.Text.Json;
using FleetManager.Auth.Model;
using FleetManager.Helpers;

namespace FleetManager.Tests
{
    public class VehicleEndpointsIntegrationTests(CustomWebApplicationFactory factory)
        : IClassFixture<CustomWebApplicationFactory>,
            IAsyncLifetime
    {
        private readonly HttpClient _client = factory.CreateClient();
        private IServiceScope _scope;
        private FleetDbContext _context;

        public async Task InitializeAsync()
        {
            _scope = factory.Services.CreateScope();
            _context = _scope.ServiceProvider.GetRequiredService<FleetDbContext>();
            await _context.Database.EnsureDeletedAsync();
            await _context.Database.EnsureCreatedAsync();
            await SeedUsersAndVehiclesAsync();
        }

        public async Task DisposeAsync()
        {
            await _context.Database.EnsureDeletedAsync();
            await _context.DisposeAsync();
            _scope.Dispose();
            TestAuthHandler.ClearTestUser();
        }

        private async Task SeedUsersAndVehiclesAsync()
        {
            var user1 = new FleetUser
            {
                Id = "user1-parent-fam1",
                UserName = "parent1",
                Email = "p1@test.com",
                FamilyGroupId = "family1"
            };
            var user2 = new FleetUser
            {
                Id = "user2-teen-fam1",
                UserName = "teen1",
                Email = "t1@test.com",
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
        }

        [Fact]
        public async Task GetVehicles_AsParentInFamily1_ReturnsOnlyFamily1VehiclesAndOwn()
        {
            TestAuthHandler.SetTestUser(
                "user1-parent-fam1",
                "parent1",
                "family1",
                [FleetRoles.Parent]
            );
            var response = await _client.GetAsync("/api/vehicles");
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
        public async Task GetVehicles_AsTeenagerInFamily1_ReturnsFamily1Vehicles()
        {
            TestAuthHandler.SetTestUser(
                "user2-teen-fam1",
                "teen1",
                "family1",
                [FleetRoles.Teenager]
            );
            var response = await _client.GetAsync("/api/vehicles");
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
            TestAuthHandler.SetTestUser(
                "user5-admin",
                "adminuser",
                "adminFamily",
                [FleetRoles.Admin]
            );
            var response = await _client.GetAsync("/api/vehicles");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
                responseString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            Assert.NotNull(result?.resource);
            Assert.Equal(4, result.resource.Length);
        }

        [Fact]
        public async Task GetVehicles_AsFleetUser_ReturnsOnlyOwnVehicle()
        {
            TestAuthHandler.SetTestUser(
                "user4-fleetuser",
                "fleetuser1",
                "family4",
                [FleetRoles.FleetUser]
            );
            var response = await _client.GetAsync("/api/vehicles");
            response.EnsureSuccessStatusCode();
            var responseString = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ResourceDto<ResourceDto<VehicleDto>[]>>(
                responseString,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );

            Assert.NotNull(result?.resource);
            var vehicles = result.resource.Select(r_dto => r_dto.resource).ToList();
            Assert.Single(vehicles);
            Assert.Contains(vehicles, v => v.Id == 4 && v.LicensePlate == "FLEET1");
        }
    }
}
