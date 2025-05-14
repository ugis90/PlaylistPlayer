using FleetManager.Services;
using FleetManager.Data.Entities;
using FleetManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FleetManager.Auth.Model;
using Xunit.Abstractions;

namespace FleetManager.Tests;

public class AnalyticsServiceTests(ITestOutputHelper testOutputHelper)
{
    private FleetDbContext CreateDbContext(string dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();

        var serviceProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

        var options = new DbContextOptionsBuilder<FleetDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .UseInternalServiceProvider(serviceProvider)
            .Options;

        return new FleetDbContext(options);
    }

    // --- CalculateAverageFuelEfficiency Tests ---

    [Fact]
    public void CalculateAverageFuelEfficiency_WithSufficientFullTankData_CalculatesCorrectly()
    {
        // Arrange
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Gallons = 10,
                FullTank = true,
                Date = DateTimeOffset.UtcNow.AddDays(-10),
                CostPerGallon = 3.5m,
                TotalCost = 35.0m,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Mileage = 10300,
                Gallons = 12,
                FullTank = true,
                Date = DateTimeOffset.UtcNow.AddDays(-5),
                CostPerGallon = 3.6m,
                TotalCost = 43.2m,
                Station = "S2",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Mileage = 10650,
                Gallons = 10,
                FullTank = true,
                Date = DateTimeOffset.UtcNow,
                CostPerGallon = 3.7m,
                TotalCost = 37.0m,
                Station = "S3",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };
        // Act
        var actualMpg = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);
        const double calculatedExpectedMpg = (double)(10650 - 10000) / (12 + 10); // ~29.54545
        double roundedExpectedMpg = Math.Round(calculatedExpectedMpg, 1); // 29.5
        // Assert
        Assert.Equal(roundedExpectedMpg, actualMpg, precision: 1);
    }

    [Fact]
    public void CalculateAverageFuelEfficiency_WithInsufficientData_ReturnsZero()
    {
        // Arrange
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Gallons = 10,
                FullTank = true,
                Date = DateTimeOffset.UtcNow,
                CostPerGallon = 3.5m,
                TotalCost = 35.0m,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };

        // Act
        var actualMpg = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);

        // Assert
        Assert.Equal(0, actualMpg);
    }

    [Fact]
    public void CalculateAverageFuelEfficiency_MissingFullTank_UsesFallbackLogic()
    {
        // Arrange
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Gallons = 10,
                FullTank = false,
                Date = DateTimeOffset.UtcNow.AddDays(-10),
                CostPerGallon = 3.5m,
                TotalCost = 35.0m,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Mileage = 10300,
                Gallons = 12,
                FullTank = false,
                Date = DateTimeOffset.UtcNow.AddDays(-5),
                CostPerGallon = 3.6m,
                TotalCost = 43.2m,
                Station = "S2",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Mileage = 10650,
                Gallons = 10,
                FullTank = false,
                Date = DateTimeOffset.UtcNow,
                CostPerGallon = 3.7m,
                TotalCost = 37.0m,
                Station = "S3",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };
        var actualMpg = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);
        const double calculatedExpectedMpgFallback = (double)(10650 - 10000) / (12 + 10); // ~29.54545
        double roundedExpectedMpgFallback = Math.Round(calculatedExpectedMpgFallback, 1); // 29.5
        // Assert
        Assert.Equal(roundedExpectedMpgFallback, actualMpg, precision: 1);
    }

    // --- CalculateCostByMonth Tests ---
    [Fact]
    public void CalculateCostByMonth_CombinesFuelAndMaintenance_CalculatesCorrectly()
    {
        // Arrange
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero),
                TotalCost = 50.00m,
                Gallons = 10,
                CostPerGallon = 5.0m,
                Mileage = 1000,
                FullTank = false,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 4, 10, 0, 0, 0, TimeSpan.Zero),
                TotalCost = 60.50m,
                Gallons = 10,
                CostPerGallon = 6.05m,
                Mileage = 1200,
                FullTank = false,
                Station = "S2",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 4, 25, 0, 0, 0, TimeSpan.Zero),
                TotalCost = 55.25m,
                Gallons = 10,
                CostPerGallon = 5.525m,
                Mileage = 1400,
                FullTank = false,
                Station = "S3",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };
        var maintenanceRecords = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Oil",
                Description = "Oil Change",
                Date = new DateTimeOffset(2024, 3, 20, 0, 0, 0, TimeSpan.Zero),
                Cost = 120.00m,
                Mileage = 1000,
                Provider = "P1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                ServiceType = "Tire",
                Description = "Tire Change",
                Date = new DateTimeOffset(2024, 4, 5, 0, 0, 0, TimeSpan.Zero),
                Cost = 75.00m,
                Mileage = 1200,
                Provider = "P2",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };

        // Act
        var result = AnalyticsService.CalculateCostByMonth(maintenanceRecords, fuelRecords);

        // Assert
        Assert.Equal(2, result.Count);
        var march = result.FirstOrDefault(r => r.Month == "2024-03");
        var april = result.FirstOrDefault(r => r.Month == "2024-04");

        Assert.NotNull(march);
        Assert.Equal(170.00m, march.Cost);

        Assert.NotNull(april);
        Assert.Equal(190.75m, april.Cost);
    }

    [Fact]
    public void CalculateCostByMonth_WithEmptyLists_ReturnsEmptyList()
    {
        var fuelRecords = new List<FuelRecord>();
        var maintenanceRecords = new List<MaintenanceRecord>();
        var result = AnalyticsService.CalculateCostByMonth(maintenanceRecords, fuelRecords);
        Assert.Empty(result);
    }

    // --- GetVehicleAnalyticsAsync Tests ---
    [Fact]
    public async Task GetVehicleAnalyticsAsync_WithData_ReturnsCorrectAnalytics()
    {
        // Arrange
        await using var context = CreateDbContext();
        var analyticsService = new AnalyticsService(context);

        const string testUserId = "analytics-user-id";
        var testUser = new FleetUser
        {
            Id = testUserId,
            UserName = "analyticsuser",
            Email = "analytics@test.com",
            FamilyGroupId = "fam1"
        };
        context.Users.Add(testUser);

        var vehicle = new Vehicle
        {
            Id = 10,
            Make = "Honda",
            Model = "Civic",
            Year = 2020,
            LicensePlate = "ANA10",
            Description = "Test Car for Analytics",
            CurrentMileage = 15000,
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
            UserId = testUserId
        };
        context.Vehicles.Add(vehicle);

        var date1 = DateTimeOffset.UtcNow.AddMonths(-2);
        var date2 = DateTimeOffset.UtcNow.AddMonths(-1);
        var date3 = DateTimeOffset.UtcNow;

        context.FuelRecords.AddRange(
            new FuelRecord
            {
                Id = 101,
                VehicleId = 10,
                Date = date1,
                Mileage = 14000,
                Gallons = 10,
                FullTank = true,
                TotalCost = 35m,
                CostPerGallon = 3.5m,
                UserId = testUserId,
                CreatedAt = date1,
                Station = "S1"
            },
            new FuelRecord
            {
                Id = 102,
                VehicleId = 10,
                Date = date2,
                Mileage = 14300,
                Gallons = 12,
                FullTank = true,
                TotalCost = 42m,
                CostPerGallon = 3.5m,
                UserId = testUserId,
                CreatedAt = date2,
                Station = "S2"
            },
            new FuelRecord
            {
                Id = 103,
                VehicleId = 10,
                Date = date3,
                Mileage = 14650,
                Gallons = 10,
                FullTank = true,
                TotalCost = 38m,
                CostPerGallon = 3.8m,
                UserId = testUserId,
                CreatedAt = date3,
                Station = "S3"
            }
        );
        context.MaintenanceRecords.AddRange(
            new MaintenanceRecord
            {
                Id = 201,
                VehicleId = 10,
                ServiceType = "Oil Change",
                Description = "Std",
                Cost = 50m,
                Date = date1,
                Mileage = 14000,
                UserId = testUserId,
                CreatedAt = date1,
                Provider = "P1"
            },
            new MaintenanceRecord
            {
                Id = 202,
                VehicleId = 10,
                ServiceType = "Tire Rotation",
                Description = "Std",
                Cost = 30m,
                Date = date2,
                Mileage = 14300,
                UserId = testUserId,
                CreatedAt = date2,
                Provider = "P2"
            }
        );
        context.Trips.AddRange(
            new Trip
            {
                Id = 301,
                VehicleId = 10,
                StartLocation = "A",
                EndLocation = "B",
                Distance = 300,
                StartTime = date1,
                EndTime = date1.AddHours(5),
                UserId = testUserId,
                CreatedAt = date1
            },
            new Trip
            {
                Id = 302,
                VehicleId = 10,
                StartLocation = "B",
                EndLocation = "C",
                Distance = 350,
                StartTime = date2,
                EndTime = date2.AddHours(6),
                UserId = testUserId,
                CreatedAt = date2
            }
        );
        await context.SaveChangesAsync();

        // Act
        var result = await analyticsService.GetVehicleAnalyticsAsync(
            10,
            DateTimeOffset.UtcNow.AddMonths(-3),
            DateTimeOffset.UtcNow
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(35m + 42m + 38m, result.FuelCosts);
        Assert.Equal(50m + 30m, result.MaintenanceCosts);
        Assert.Equal((35m + 42m + 38m) + (50m + 30m), result.TotalCost);

        Assert.Equal(300 + 350, result.Mileage);
        Assert.Equal(2, result.TotalTrips);

        const double calculatedExpectedMpgForAnalytics = (double)(14650 - 14000) / (12 + 10); // 650 / 22
        double roundedExpectedMpgForAnalytics = Math.Round(calculatedExpectedMpgForAnalytics, 1); // 29.5
        Assert.Equal(roundedExpectedMpgForAnalytics, result.FuelEfficiency, precision: 1);

        Assert.True(result.CostByMonth.Count() >= 2);
    }

    // --- PredictUpcomingMaintenance Tests ---
    [Fact]
    public void PredictUpcomingMaintenance_WithNextServiceDue_ReturnsCorrectly()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = 2020,
            CurrentMileage = 10000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow,
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Oil Change",
                Description = "std",
                Cost = 50m,
                Mileage = 9000,
                Date = DateTimeOffset.UtcNow.AddMonths(-1),
                NextServiceDue = DateTimeOffset.UtcNow.AddMonths(2), // Due in 2 months
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-1),
                Provider = "P1"
            }
        };

        // Act
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        // Assert
        Assert.Single(upcoming);
        Assert.Equal("Oil Change", upcoming[0].Type);
        Assert.True(
            upcoming[0].DueDate > DateTimeOffset.UtcNow.AddMonths(1)
                && upcoming[0].DueDate < DateTimeOffset.UtcNow.AddMonths(3)
        );
    }

    [Fact]
    public void PredictUpcomingMaintenance_NoNextServiceDue_PredictsBasedOnStandardInterval()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = 2020,
            CurrentMileage = 10000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow,
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Tire Rotation",
                Description = "std",
                Cost = 30m,
                Mileage = 5000,
                Date = DateTimeOffset.UtcNow.AddMonths(-5), // 5 months ago
                NextServiceDue = null,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-5),
                Provider = "P1"
            }
        };

        // Act
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        // Assert
        var tireRotation = upcoming.FirstOrDefault(u => u.Type == "Tire Rotation");
        Assert.NotNull(tireRotation);
        Assert.True(
            tireRotation.DueDate > DateTimeOffset.UtcNow
                && tireRotation.DueDate < DateTimeOffset.UtcNow.AddMonths(2)
        );
    }

    [Fact]
    public void PredictUpcomingMaintenance_NoHistory_ReturnsDefaultOilChange_PrimarilyByMileage()
    {
        // Arrange
        var slightlyMoreThanAYearAgo = DateTimeOffset.UtcNow.AddYears(-1).AddMonths(-1);
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = slightlyMoreThanAYearAgo.Year,
            CurrentMileage = 3000,
            UserId = "u1",
            CreatedAt = slightlyMoreThanAYearAgo,
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var maintenanceHistory = new List<MaintenanceRecord>();

        // Act
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        // Assert
        Assert.NotEmpty(upcoming);
        Assert.Contains(upcoming, u => u.Type == "Oil Change");
    }

    [Fact]
    public void PredictUpcomingMaintenance_NoHistory_ForNewerLowMileageVehicle_ShouldSuggestSomeDefaults()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = DateTimeOffset.UtcNow.Year,
            CurrentMileage = 200,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddMonths(-2),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var maintenanceHistory = new List<MaintenanceRecord>();

        // Act
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        // Assert
        Assert.NotEmpty(upcoming);

        Assert.Contains(upcoming, u => u.Type == "Oil Change");

        testOutputHelper.WriteLine(
            "Default suggestions for newer, low-mileage vehicle: "
                + string.Join(
                    ", ",
                    upcoming.Select(s => $"{s.Type} due {s.DueDate.ToString("yyyy-MM-dd")}")
                )
        );
    }

    [Fact]
    public void PredictUpcomingMaintenance_NoHistory_VehicleOverOneYearOld_ShouldSuggestAnnualInspection()
    {
        // Arrange
        var vehicle = new Vehicle
        {
            Id = 3,
            Year = DateTimeOffset.UtcNow.Year - 2,
            CurrentMileage = 500,
            UserId = "u3",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-2),
            Description = "d3",
            LicensePlate = "lp3",
            Make = "m3",
            Model = "m3"
        };
        var maintenanceHistory = new List<MaintenanceRecord>();

        // Act
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        // Assert
        Assert.NotEmpty(upcoming);

        Assert.Contains(upcoming, u => u.Type == "Oil Change");

        int ageInMonthsForTest =
            (DateTimeOffset.UtcNow.Year - vehicle.Year) * 12 + DateTimeOffset.UtcNow.Month - 1;
        if (ageInMonthsForTest % 12 == 0)
        {
            Assert.Contains(upcoming, u => u.Type == "Annual Inspection");
            testOutputHelper.WriteLine(
                $"Annual Inspection suggested for vehicle year {vehicle.Year} (ageInMonths: {ageInMonthsForTest})"
            );
        }
        else
        {
            testOutputHelper.WriteLine(
                $"Annual Inspection NOT suggested for vehicle year {vehicle.Year} (ageInMonths: {ageInMonthsForTest}, not multiple of 12)"
            );
        }
        testOutputHelper.WriteLine(
            "Default suggestions for older vehicle (Year "
                + vehicle.Year
                + ", Mileage "
                + vehicle.CurrentMileage
                + "): "
                + string.Join(", ", upcoming.Select(s => $"{s.Type} due {s.DueDate:yyyy-MM-dd}"))
        );
    }

    [Fact]
    public void PredictUpcomingMaintenance_NoHistory_ForOlderVehicle_ReturnsSomeDefaultSuggestions()
    {
        // Arrange
        var twoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2);
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = twoYearsAgo.Year,
            CurrentMileage = 100,
            UserId = "u1",
            CreatedAt = twoYearsAgo,
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var maintenanceHistory = new List<MaintenanceRecord>();

        // Act
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        // Assert
        Assert.NotEmpty(upcoming);
        testOutputHelper.WriteLine(
            "Default suggestions for older, low-mileage vehicle with no history: "
                + string.Join(", ", upcoming.Select(s => s.Type))
        );
    }

    // --- CalculateFuelEfficiencyTrend Tests ---
    [Fact]
    public void CalculateFuelEfficiencyTrend_WithSufficientData_ReturnsTrend()
    {
        // Arrange
        var fuelRecords = new List<FuelRecord>
        {
            // Month 1
            new()
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10000,
                Gallons = 10,
                FullTank = true,
                CostPerGallon = 3,
                TotalCost = 30,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10250,
                Gallons = 10,
                FullTank = true,
                CostPerGallon = 3,
                TotalCost = 30,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            // Month 2
            new()
            {
                Id = 3,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10500,
                Gallons = 10,
                FullTank = true,
                CostPerGallon = 3,
                TotalCost = 30,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 4,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10800,
                Gallons = 10,
                FullTank = true,
                CostPerGallon = 3,
                TotalCost = 30,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            }
        };

        // Act
        var trend = AnalyticsService.CalculateFuelEfficiencyTrend(fuelRecords);

        // Assert
        Assert.Equal(2, trend.Count);
        Assert.Contains(trend, t => t.Date.Month == 1 && t.Mpg == 25.0); // (10250-10000)/10
        Assert.Contains(trend, t => t.Date.Month == 2 && t.Mpg == 30.0); // (10800-10500)/10
    }

    [Fact]
    public void CalculateFuelEfficiencyTrend_WithInsufficientDataPerMonth_ReturnsEmptyOrPartialTrend()
    {
        // Arrange
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10000,
                Gallons = 10,
                FullTank = true,
                CostPerGallon = 3,
                TotalCost = 30,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10200,
                Gallons = 10,
                FullTank = true,
                CostPerGallon = 3,
                TotalCost = 30,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10500,
                Gallons = 10,
                FullTank = true,
                CostPerGallon = 3,
                TotalCost = 30,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            }
        };

        // Act
        var trend = AnalyticsService.CalculateFuelEfficiencyTrend(fuelRecords);

        // Assert
        Assert.Single(trend);
        Assert.Contains(trend, t => t.Date.Month == 2 && t.Mpg == 30.0); // (10500-10200)/10
    }
}
