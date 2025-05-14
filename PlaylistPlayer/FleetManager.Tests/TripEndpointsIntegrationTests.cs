using FleetManager.Data.Entities;
using Microsoft.Extensions.DependencyInjection;
using FleetManager.Data;
using System.Text.Json;
using System.Text;
using System.Net;
using Microsoft.EntityFrameworkCore;
using FleetManager.Auth.Model;

namespace FleetManager.Tests
{
    public class TripEndpointsIntegrationTests(CustomWebApplicationFactory factory)
        : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly HttpClient _client = factory.CreateClient();

        private string _testUserId = $"testuser-{Guid.NewGuid()}";

        private async Task SeedDatabaseWithVehicleAndUser(
            int vehicleId,
            int initialMileage,
            string userId,
            string userName,
            string familyGroupId
        )
        {
            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();

            var testUser = await context.Users.FindAsync(userId);
            if (testUser == null)
            {
                testUser = new FleetUser
                {
                    Id = userId,
                    UserName = userName,
                    Email = $"{userName}@test.com",
                    FamilyGroupId = familyGroupId
                };
                context.Users.Add(testUser);
                await context.SaveChangesAsync();
            }
            _testUserId = testUser.Id;

            var vehicle = new Vehicle
            {
                Id = vehicleId,
                Make = "TestMake",
                Model = "TestModel",
                Year = 2023,
                LicensePlate = "TEST1",
                Description = "Test Vehicle",
                CurrentMileage = initialMileage,
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = _testUserId // Use the stored/created user ID
            };
            context.Vehicles.Add(vehicle);
            await context.SaveChangesAsync();
            Console.WriteLine(
                $"Seeded User {_testUserId} and Vehicle {vehicleId} with mileage {initialMileage}"
            );
        }

        [Fact]
        public async Task CreateTrip_ValidData_ReturnsCreatedAndUpdatesMileage()
        {
            // Arrange
            const int testVehicleId = 1;
            const int initialMileage = 10000;
            const double tripDistance = 150.5;

            // Define user details for this test
            string currentTestUserId = $"user-{Guid.NewGuid()}";
            const string currentTestUserName = "tripcreator";
            const string currentTestFamilyId = "family1";
            var userRoles = new List<string> { FleetRoles.FleetUser }; // User needs a role that can create trips

            await SeedDatabaseWithVehicleAndUser(
                testVehicleId,
                initialMileage,
                currentTestUserId,
                currentTestUserName,
                currentTestFamilyId
            );

            TestAuthHandler.SetTestUser(
                currentTestUserId,
                currentTestUserName,
                currentTestFamilyId,
                userRoles
            );

            var createTripDto = new CreateTripDto(
                StartLocation: "Start Test",
                EndLocation: "End Test",
                Distance: tripDistance,
                StartTime: DateTimeOffset.UtcNow.AddHours(-1),
                EndTime: DateTimeOffset.UtcNow,
                Purpose: "Integration Test",
                FuelUsed: null
            );
            var content = new StringContent(
                JsonSerializer.Serialize(createTripDto),
                Encoding.UTF8,
                "application/json"
            );

            // Act
            var response = await _client.PostAsync($"/api/vehicles/{testVehicleId}/trips", content);

            TestAuthHandler.ClearTestUser();

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            using var scope = factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<FleetDbContext>();

            var createdTrip = await context.Trips.FirstOrDefaultAsync(
                t => t.VehicleId == testVehicleId && t.Purpose == "Integration Test"
            );
            Assert.NotNull(createdTrip);
            Assert.Equal(tripDistance, createdTrip.Distance);
            Assert.Equal(currentTestUserId, createdTrip.UserId);

            var updatedVehicle = await context.Vehicles.FindAsync(testVehicleId);
            Assert.NotNull(updatedVehicle);
            var expectedMileage = initialMileage + (int)Math.Round(tripDistance);
            Assert.Equal(expectedMileage, updatedVehicle.CurrentMileage);
        }
    }
}
