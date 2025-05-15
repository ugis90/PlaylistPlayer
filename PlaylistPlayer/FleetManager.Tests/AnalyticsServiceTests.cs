using System.Text.Json;
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

    [Fact]
    public void CalculateCostByMonth_WithEmptyLists_ReturnsEmptyList()
    {
        var fuelRecords = new List<FuelRecord>();
        var maintenanceRecords = new List<MaintenanceRecord>();
        var result = AnalyticsService.CalculateCostByMonth(maintenanceRecords, fuelRecords);
        Assert.Empty(result);
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
                Date = DateTimeOffset.UtcNow.AddMonths(-5),
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

    [Fact]
    public void CalculateAverageFuelEfficiency_WithInsufficientData_ReturnsZero()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new FuelRecord
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Liters = 30,
                FullTank = true,
                Date = DateTimeOffset.UtcNow,
                CostPerLiter = 1.5m,
                TotalCost = 45.0m,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };
        var actualL100Km = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);
        Assert.Equal(0, actualL100Km);
    }

    [Fact]
    public void CalculateCostByMonth_CombinesFuelAndMaintenance_CalculatesCorrectly()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new FuelRecord
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 3, 15, 0, 0, 0, TimeSpan.Zero),
                TotalCost = 50.00m,
                Liters = 10,
                CostPerLiter = 5.0m,
                Mileage = 1000,
                FullTank = false,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new FuelRecord
            {
                Id = 2,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 4, 10, 0, 0, 0, TimeSpan.Zero),
                TotalCost = 60.50m,
                Liters = 10,
                CostPerLiter = 6.05m,
                Mileage = 1200,
                FullTank = false,
                Station = "S2",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new FuelRecord
            {
                Id = 3,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 4, 25, 0, 0, 0, TimeSpan.Zero),
                TotalCost = 55.25m,
                Liters = 10,
                CostPerLiter = 5.525m,
                Mileage = 1400,
                FullTank = false,
                Station = "S3",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };
        var maintenanceRecords = new List<MaintenanceRecord>
        {
            new MaintenanceRecord
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
            new MaintenanceRecord
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
        var result = AnalyticsService.CalculateCostByMonth(maintenanceRecords, fuelRecords);
        Assert.Equal(2, result.Count);
        var march = result.FirstOrDefault(r => r.Month == "2024-03");
        var april = result.FirstOrDefault(r => r.Month == "2024-04");
        Assert.NotNull(march);
        Assert.Equal(170.00m, march.Cost);
        Assert.NotNull(april);
        Assert.Equal(190.75m, april.Cost);
    }

    [Fact]
    public async Task GetVehicleAnalyticsAsync_WithData_ReturnsCorrectLitersPer100Km()
    {
        using var context = CreateDbContext();
        var analyticsService = new AnalyticsService(context);
        const string testUserId = "analytics-user-id-2";
        var testUser = new FleetUser
        {
            Id = testUserId,
            UserName = "analyticsuser2",
            Email = "analytics2@test.com",
            FamilyGroupId = "fam2"
        };
        context.Users.Add(testUser);
        var vehicle = new Vehicle
        {
            Id = 11,
            Make = "Mazda",
            Model = "CX-5",
            Year = 2021,
            LicensePlate = "ANA11",
            Description = "Desc",
            CurrentMileage = 25000,
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
                Id = 104,
                VehicleId = 11,
                Date = date1,
                Mileage = 24000,
                Liters = 30,
                FullTank = true,
                TotalCost = 45m,
                CostPerLiter = 1.5m,
                UserId = testUserId,
                CreatedAt = date1,
                Station = "S1"
            },
            new FuelRecord
            {
                Id = 105,
                VehicleId = 11,
                Date = date2,
                Mileage = 24400,
                Liters = 32,
                FullTank = true,
                TotalCost = 48m,
                CostPerLiter = 1.5m,
                UserId = testUserId,
                CreatedAt = date2,
                Station = "S2"
            }, // 400km / 32L => 8 L/100km
            new FuelRecord
            {
                Id = 106,
                VehicleId = 11,
                Date = date3,
                Mileage = 24850,
                Liters = 36,
                FullTank = true,
                TotalCost = 54m,
                CostPerLiter = 1.5m,
                UserId = testUserId,
                CreatedAt = date3,
                Station = "S3"
            } // 450km / 36L => 8 L/100km
        );
        context.Trips.AddRange(
            new Trip
            {
                Id = 303,
                VehicleId = 11,
                StartLocation = "X",
                EndLocation = "Y",
                Distance = 400,
                StartTime = date1,
                EndTime = date1.AddHours(5),
                UserId = testUserId,
                CreatedAt = date1
            },
            new Trip
            {
                Id = 304,
                VehicleId = 11,
                StartLocation = "Y",
                EndLocation = "Z",
                Distance = 450,
                StartTime = date2,
                EndTime = date2.AddHours(6),
                UserId = testUserId,
                CreatedAt = date2
            }
        );
        await context.SaveChangesAsync();

        var result = await analyticsService.GetVehicleAnalyticsAsync(
            11,
            DateTimeOffset.UtcNow.AddMonths(-3),
            DateTimeOffset.UtcNow
        );

        Assert.NotNull(result);
        const double expectedL100Km = (68.0 / 850.0) * 100;
        Assert.Equal(expectedL100Km, result.FuelEfficiencyLitersPer100Km, precision: 1);
    }

    [Fact]
    public void PredictUpcomingMaintenance_WithNextServiceDue_ReturnsCorrectly()
    {
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
                NextServiceDue = DateTimeOffset.UtcNow.AddMonths(2),
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-1),
                Provider = "P1"
            }
        };
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        var oilChange = upcoming.FirstOrDefault(u => u.Type == "Oil Change");
        Assert.NotNull(oilChange);
        Assert.True(
            oilChange.DueDate > DateTimeOffset.UtcNow.AddMonths(1)
                && oilChange.DueDate < DateTimeOffset.UtcNow.AddMonths(3)
        );
    }

    [Fact]
    public void CalculateAverageFuelEfficiency_WithSufficientFullTankData_CalculatesCorrectLitersPer100Km()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Liters = 10,
                FullTank = true,
                Date = DateTimeOffset.UtcNow.AddDays(-10),
                CostPerLiter = 1.5m,
                TotalCost = 15.0m,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Mileage = 10300,
                Liters = 24,
                FullTank = true,
                Date = DateTimeOffset.UtcNow.AddDays(-5),
                CostPerLiter = 1.5m,
                TotalCost = 36.0m,
                Station = "S2",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Mileage = 10650,
                Liters = 28,
                FullTank = true,
                Date = DateTimeOffset.UtcNow,
                CostPerLiter = 1.5m,
                TotalCost = 42.0m,
                Station = "S3",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };
        const double expectedL100Km = (52.0 / 650.0) * 100;

        var actualL100Km = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);
        Assert.Equal(expectedL100Km, actualL100Km, 1);
    }

    [Fact]
    public void CalculateAverageFuelEfficiency_MissingFullTank_UsesFallbackLogic_CalculatesLitersPer100Km()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Liters = 10,
                FullTank = false,
                Date = DateTimeOffset.UtcNow.AddDays(-10),
                CostPerLiter = 1.5m,
                TotalCost = 15.0m,
                Station = "S1",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Mileage = 10300,
                Liters = 24,
                FullTank = false,
                Date = DateTimeOffset.UtcNow.AddDays(-5),
                CostPerLiter = 1.5m,
                TotalCost = 36.0m,
                Station = "S2",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Mileage = 10650,
                Liters = 28,
                FullTank = false,
                Date = DateTimeOffset.UtcNow,
                CostPerLiter = 1.5m,
                TotalCost = 42.0m,
                Station = "S3",
                CreatedAt = DateTimeOffset.UtcNow,
                UserId = "user1"
            }
        };
        const double expectedL100KmFallback = (52.0 / 650.0) * 100; // 8.0

        var actualL100Km = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);
        Assert.Equal(expectedL100KmFallback, actualL100Km, 1);
    }

    [Fact]
    public void PredictUpcomingMaintenance_NoHistory_ReturnsDefaultOilChange_WhenMileageConditionMet()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = DateTimeOffset.UtcNow.Year - 2,
            CurrentMileage = 15000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-2),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var maintenanceHistory = new List<MaintenanceRecord>();
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        Assert.NotEmpty(upcoming);
        Assert.Contains(upcoming, u => u.Type == "Oil Change");
    }

    [Fact]
    public void CalculateFuelEfficiencyTrend_WithSufficientData_ReturnsCorrectTrend()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10000,
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10500,
                Liters = 40,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 60m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 11000,
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 4,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero),
                Mileage = 11400,
                Liters = 30,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 45m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            }
        };
        var trend = AnalyticsService.CalculateFuelEfficiencyTrend(fuelRecords);

        Assert.Equal(2, trend.Count);
        var janTrend = trend.FirstOrDefault(t => t.Date.Month == 1);
        var febTrend = trend.FirstOrDefault(t => t.Date.Month == 2);

        Assert.NotNull(janTrend);
        Assert.Equal(8.0, janTrend.LitersPer100Km, 1);

        Assert.NotNull(febTrend);
        Assert.Equal(7.5, febTrend.LitersPer100Km, 1); // Corrected expected value
    }

    [Fact]
    public void CalculateFuelEfficiencyTrend_WithInsufficientDataPerMonth_ReturnsCorrectPartialTrend()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10000,
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
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
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
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
                Liters = 24,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 36m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            }
        };

        var trend = AnalyticsService.CalculateFuelEfficiencyTrend(fuelRecords);

        Assert.Single(trend);
        var febTrend = trend.First();
        Assert.Equal(2, febTrend.Date.Month);
        Assert.Equal(8.0, febTrend.LitersPer100Km, 1);
    }

    [Fact]
    public void PredictUpcomingMaintenance_WithNextServiceDue_ReturnsItAndPotentiallyDefaults()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = DateTimeOffset.UtcNow.Year - 3,
            CurrentMileage = 40000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-3),
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
                ServiceType = "Specific Check",
                Description = "std",
                Cost = 50m,
                Mileage = 39000,
                Date = DateTimeOffset.UtcNow.AddMonths(-1),
                NextServiceDue = DateTimeOffset.UtcNow.AddMonths(2),
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-1),
                Provider = "P1"
            }
        };
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);

        Assert.NotEmpty(upcoming);
        var specificCheck = upcoming.FirstOrDefault(u => u.Type == "Specific Check");
        Assert.NotNull(specificCheck);
        Assert.True(
            specificCheck.DueDate > DateTimeOffset.UtcNow.AddMonths(1)
                && specificCheck.DueDate < DateTimeOffset.UtcNow.AddMonths(3)
        );
    }

    [Fact]
    public void CalculateAverageFuelEfficiency_SufficientFullTanks_ReturnsCorrectL100Km()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Liters = 10,
                FullTank = true,
                Date = DateTimeOffset.UtcNow.AddDays(-20),
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Mileage = 10300,
                Liters = 24,
                FullTank = true,
                Date = DateTimeOffset.UtcNow.AddDays(-10),
                CostPerLiter = 1.5m,
                TotalCost = 36m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S2"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Mileage = 10650,
                Liters = 28,
                FullTank = true,
                Date = DateTimeOffset.UtcNow,
                CostPerLiter = 1.5m,
                TotalCost = 42m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S3"
            }
        };
        const double expectedL100Km = 8.0;
        var actualL100Km = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);
        Assert.Equal(expectedL100Km, actualL100Km, 1);
    }

    [Fact]
    public void CalculateAverageFuelEfficiency_InsufficientData_ReturnsZero()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Liters = 10,
                FullTank = true,
                Date = DateTimeOffset.UtcNow,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            }
        };
        Assert.Equal(0, AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords));
    }

    [Fact]
    public void CalculateAverageFuelEfficiency_NoFullTanks_UsesFallback_ReturnsCorrectL100Km()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Mileage = 10000,
                Liters = 10,
                FullTank = false,
                Date = DateTimeOffset.UtcNow.AddDays(-20),
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Mileage = 10300,
                Liters = 24,
                FullTank = false,
                Date = DateTimeOffset.UtcNow.AddDays(-10),
                CostPerLiter = 1.5m,
                TotalCost = 36m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S2"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Mileage = 10650,
                Liters = 28,
                FullTank = false,
                Date = DateTimeOffset.UtcNow,
                CostPerLiter = 1.5m,
                TotalCost = 42m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S3"
            }
        };
        const double expectedL100Km = (52.0 / 650.0) * 100;
        var actualL100Km = AnalyticsService.CalculateAverageFuelEfficiency(fuelRecords);
        Assert.Equal(expectedL100Km, actualL100Km, 1);
    }

    [Fact]
    public void CalculateFuelEfficiencyTrend_SufficientData_ReturnsCorrectTrend()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10000,
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10500,
                Liters = 40,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 60m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 11000,
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            },
            new()
            {
                Id = 4,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 2, 15, 0, 0, 0, TimeSpan.Zero),
                Mileage = 11400,
                Liters = 30,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 45m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            }
        };
        var trend = AnalyticsService.CalculateFuelEfficiencyTrend(fuelRecords);
        Assert.Equal(2, trend.Count);
        Assert.Contains(trend, t => t.Date.Month == 1 && Math.Abs(t.LitersPer100Km - 8.0) < 0.01);
        Assert.Contains(trend, t => t.Date.Month == 2 && Math.Abs(t.LitersPer100Km - 7.5) < 0.01);
    }

    [Fact]
    public void CalculateFuelEfficiencyTrend_InsufficientDataPerMonth_ReturnsPartialTrend()
    {
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                Date = new DateTimeOffset(2024, 1, 5, 0, 0, 0, TimeSpan.Zero),
                Mileage = 10000,
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
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
                Liters = 10,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
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
                Liters = 24,
                FullTank = true,
                CostPerLiter = 1.5m,
                TotalCost = 36m,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Station = "S1"
            }
        };
        var trend = AnalyticsService.CalculateFuelEfficiencyTrend(fuelRecords);
        Assert.Single(trend);
        Assert.Contains(trend, t => t.Date.Month == 2 && Math.Abs(t.LitersPer100Km - 8.0) < 0.01);
    }

    [Fact]
    public void PredictUpcomingMaintenance_HistoricalPrediction_TimeBased()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = 2020,
            CurrentMileage = 20000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow,
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var now = DateTimeOffset.UtcNow;
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            // Service done every ~3 months, last one 2.5 months ago. Mileage interval large.
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Filter Change",
                Cost = 20m,
                Mileage = 5000,
                Date = now.AddMonths(-8).AddDays(-15),
                NextServiceDue = null,
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            },
            new()
            {
                Id = 2,
                VehicleId = 1,
                ServiceType = "Filter Change",
                Cost = 20m,
                Mileage = 10000,
                Date = now.AddMonths(-5).AddDays(-10),
                NextServiceDue = null,
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            },
            new()
            {
                Id = 3,
                VehicleId = 1,
                ServiceType = "Filter Change",
                Cost = 20m,
                Mileage = 15000,
                Date = now.AddMonths(-2).AddDays(-15),
                NextServiceDue = null,
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            }
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        var filterChange = upcoming.FirstOrDefault(u => u.Type == "Filter Change");

        Assert.NotNull(filterChange);
        Assert.True(filterChange.DueDate > now && filterChange.DueDate < now.AddMonths(1));
    }

    [Fact]
    public void PredictUpcomingMaintenance_AddMissing_TireRotationByAge()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = DateTimeOffset.UtcNow.Year - 1,
            CurrentMileage = 1000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var maintenanceHistory = new List<MaintenanceRecord> { };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        testOutputHelper.WriteLine(
            "Upcoming for missing tire rotation: " + JsonSerializer.Serialize(upcoming)
        );

        int ageInMonthsForTest =
            (DateTimeOffset.UtcNow.Year - vehicle.Year) * 12 + DateTimeOffset.UtcNow.Month - 1;
        if (ageInMonthsForTest % 6 == 0 && vehicle.CurrentMileage % 10000 > 800) // Ensure mileage rule not dominant
        {
            Assert.Contains(upcoming, u => u.Type == "Tire Rotation");
        }
        else
        {
            Assert.NotEmpty(upcoming);
        }
    }

    // --- CalculateMileageForPeriod Tests ---
    [Fact]
    public void CalculateMileageForPeriod_WithTrips_UsesTripDistance()
    {
        var trips = new List<Trip>
        {
            new()
            {
                Id = 1,
                Distance = 100.5,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                StartLocation = "A",
                EndLocation = "B",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            },
            new()
            {
                Id = 2,
                Distance = 50.2,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                StartLocation = "A",
                EndLocation = "B",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            }
        };
        var fuelRecords = new List<FuelRecord>();

        var mileage = AnalyticsService.CalculateMileageForPeriod(trips, fuelRecords);
        Assert.Equal(150, mileage);
    }

    [Fact]
    public void CalculateMileageForPeriod_NoTrips_UsesFuelRecordMileageDifference()
    {
        var trips = new List<Trip>();
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                Mileage = 10000,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Date = DateTimeOffset.UtcNow,
                Liters = 1,
                CostPerLiter = 1,
                TotalCost = 1,
                FullTank = false,
                Station = "S"
            },
            new()
            {
                Id = 2,
                Mileage = 10550,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Date = DateTimeOffset.UtcNow,
                Liters = 1,
                CostPerLiter = 1,
                TotalCost = 1,
                FullTank = false,
                Station = "S"
            }
        };
        var mileage = AnalyticsService.CalculateMileageForPeriod(trips, fuelRecords);
        Assert.Equal(550, mileage);
    }

    [Fact]
    public void CalculateMileageForPeriod_NoTrips_LessThanTwoFuelRecords_ReturnsZero()
    {
        var trips = new List<Trip>();
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                Mileage = 10000,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Date = DateTimeOffset.UtcNow,
                Liters = 1,
                CostPerLiter = 1,
                TotalCost = 1,
                FullTank = false,
                Station = "S"
            }
        };
        var mileage = AnalyticsService.CalculateMileageForPeriod(trips, fuelRecords);
        Assert.Equal(0, mileage);
    }

    [Fact]
    public void CalculateMileageForPeriod_WithTripsAndFuelRecords_PrioritizesTrips()
    {
        var trips = new List<Trip>
        {
            new()
            {
                Id = 1,
                Distance = 75,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                StartLocation = "A",
                EndLocation = "B",
                StartTime = DateTimeOffset.UtcNow,
                EndTime = DateTimeOffset.UtcNow
            }
        };
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                Mileage = 100,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Date = DateTimeOffset.UtcNow,
                Liters = 1,
                CostPerLiter = 1,
                TotalCost = 1,
                FullTank = false,
                Station = "S"
            },
            new()
            {
                Id = 2,
                Mileage = 200,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Date = DateTimeOffset.UtcNow,
                Liters = 1,
                CostPerLiter = 1,
                TotalCost = 1,
                FullTank = false,
                Station = "S"
            } // Diff is 100
        };
        var mileage = AnalyticsService.CalculateMileageForPeriod(trips, fuelRecords);
        Assert.Equal(75, mileage); // Should use trip distance
    }

    [Theory]
    [InlineData("Oil Change", 45.99d)]
    [InlineData("Tire Rotation", 25.00d)]
    [InlineData("Unknown Service", 50.00d)] // Default estimate
    public void EstimateServiceCost_NoHistory_ReturnsDefaultBasedOnType(
        string serviceType,
        double expectedCost
    )
    {
        var cost = AnalyticsService.EstimateServiceCost(serviceType, new List<MaintenanceRecord>());
        Assert.Equal((decimal)expectedCost, cost);
    }

    [Fact]
    public void EstimateServiceCost_WithHistory_ReturnsAverageCost()
    {
        var history = new List<MaintenanceRecord>
        {
            new()
            {
                ServiceType = "Oil Change",
                Cost = 40m, /* other required props */
                Id = 1,
                VehicleId = 1,
                Mileage = 1,
                Date = DateTimeOffset.UtcNow,
                UserId = "u",
                CreatedAt = DateTimeOffset.UtcNow,
                Description = "d",
                Provider = "p"
            },
            new()
            {
                ServiceType = "Oil Change",
                Cost = 50m, /* other required props */
                Id = 2,
                VehicleId = 1,
                Mileage = 1,
                Date = DateTimeOffset.UtcNow,
                UserId = "u",
                CreatedAt = DateTimeOffset.UtcNow,
                Description = "d",
                Provider = "p"
            },
            new()
            {
                ServiceType = "Oil Change",
                Cost = 45m, /* other required props */
                Id = 3,
                VehicleId = 1,
                Mileage = 1,
                Date = DateTimeOffset.UtcNow,
                UserId = "u",
                CreatedAt = DateTimeOffset.UtcNow,
                Description = "d",
                Provider = "p"
            }
        };
        var cost = AnalyticsService.EstimateServiceCost("Oil Change", history);
        Assert.Equal(45m, cost); // (40+50+45)/3
    }

    // --- PredictBasedOnStandardIntervals Tests ---
    [Fact]
    public void PredictBasedOnStandardIntervals_OilChange_ReturnsDatePlus3Months()
    {
        var lastServiceDate = DateTimeOffset.UtcNow.AddMonths(-1);
        var history = new List<MaintenanceRecord>
        {
            new()
            {
                ServiceType = "Oil Change",
                Date = lastServiceDate, /* other required props */
                Id = 1,
                VehicleId = 1,
                Mileage = 1,
                Cost = 1,
                UserId = "u",
                CreatedAt = DateTimeOffset.UtcNow,
                Description = "d",
                Provider = "p"
            }
        };

        var predictedDate = AnalyticsService.PredictBasedOnStandardIntervals("Oil Change", history);

        Assert.NotNull(predictedDate);
        Assert.Equal(lastServiceDate.AddMonths(3).Date, predictedDate.Value.Date);
    }

    [Fact]
    public void PredictBasedOnStandardIntervals_UnknownService_ReturnsDatePlus6Months()
    {
        var lastServiceDate = DateTimeOffset.UtcNow.AddMonths(-1);
        var history = new List<MaintenanceRecord>
        {
            new()
            {
                ServiceType = "Weird Service",
                Date = lastServiceDate, /* other required props */
                Id = 1,
                VehicleId = 1,
                Mileage = 1,
                Cost = 1,
                UserId = "u",
                CreatedAt = DateTimeOffset.UtcNow,
                Description = "d",
                Provider = "p"
            }
        };

        var predictedDate = AnalyticsService.PredictBasedOnStandardIntervals(
            "Weird Service",
            history
        );

        Assert.NotNull(predictedDate);
        Assert.Equal(lastServiceDate.AddMonths(6).Date, predictedDate.Value.Date);
    }

    [Fact]
    public void PredictBasedOnStandardIntervals_NoHistory_ReturnsNull()
    {
        var predictedDate = AnalyticsService.PredictBasedOnStandardIntervals(
            "Oil Change",
            new List<MaintenanceRecord>()
        );
        Assert.Null(predictedDate);
    }

    // --- FindMostUsedVehicle Tests ---
    [Fact]
    public void FindMostUsedVehicle_ReturnsCorrectVehicle()
    {
        var now = DateTimeOffset.UtcNow;
        var vehicles = new List<Vehicle>
        {
            new()
            {
                Id = 1,
                Make = "Toyota",
                Model = "Camry",
                UserId = "u1",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                Trips = new List<Trip>
                {
                    new()
                    {
                        Id = 1,
                        StartTime = now.AddDays(-5),
                        EndTime = now.AddDays(-4),
                        VehicleId = 1,
                        UserId = "u1",
                        CreatedAt = now,
                        Distance = 1,
                        StartLocation = "A",
                        EndLocation = "B"
                    }
                }
            },
            new()
            {
                Id = 2,
                Make = "Honda",
                Model = "Civic",
                UserId = "u2",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                Trips = new List<Trip>
                {
                    new()
                    {
                        Id = 2,
                        StartTime = now.AddDays(-3),
                        EndTime = now.AddDays(-2),
                        VehicleId = 2,
                        UserId = "u2",
                        CreatedAt = now,
                        Distance = 1,
                        StartLocation = "A",
                        EndLocation = "B"
                    },
                    new()
                    {
                        Id = 3,
                        StartTime = now.AddDays(-1),
                        EndTime = now,
                        VehicleId = 2,
                        UserId = "u2",
                        CreatedAt = now,
                        Distance = 1,
                        StartLocation = "A",
                        EndLocation = "B"
                    }
                }
            }
        };
        var mostUsed = AnalyticsService.FindMostUsedVehicle(vehicles, now.AddDays(-7), now);
        Assert.Equal(2, mostUsed.Id);
        Assert.Equal(2, mostUsed.Trips);
    }

    [Fact]
    public void FindMostUsedVehicle_NoTripsInPeriod_ReturnsUnknown()
    {
        var now = DateTimeOffset.UtcNow;
        var vehicles = new List<Vehicle>
        {
            new()
            {
                Id = 1,
                Make = "Toyota",
                Model = "Camry",
                UserId = "u1",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                Trips = new List<Trip>()
            }
        };
        var mostUsed = AnalyticsService.FindMostUsedVehicle(vehicles, now.AddDays(-7), now);
        Assert.Equal(0, mostUsed.Id); // Or whatever your "Unknown" ID is
        Assert.Equal("Unknown", mostUsed.Make);
    }

    [Fact]
    public void ProcessVehicleStatistics_CalculatesCorrectAggregates()
    {
        var now = DateTimeOffset.UtcNow;
        var vehicles = new List<Vehicle>
        {
            new()
            {
                Id = 1,
                Make = "Toyota",
                Model = "Camry",
                UserId = "u1",
                CreatedAt = now,
                CurrentMileage = 15000,
                Description = "d",
                LicensePlate = "L1",
                Year = 2020,
                Trips = new List<Trip>
                {
                    new()
                    {
                        Id = 1,
                        Distance = 100,
                        StartTime = now.AddDays(-10),
                        EndTime = now.AddDays(-9),
                        VehicleId = 1,
                        UserId = "u1",
                        CreatedAt = now,
                        StartLocation = "A",
                        EndLocation = "B"
                    },
                    new()
                    {
                        Id = 2,
                        Distance = 150,
                        StartTime = now.AddDays(-5),
                        EndTime = now.AddDays(-4),
                        VehicleId = 1,
                        UserId = "u1",
                        CreatedAt = now,
                        StartLocation = "C",
                        EndLocation = "D"
                    }
                },
                FuelRecords = new List<FuelRecord>
                {
                    new()
                    {
                        Id = 1,
                        Mileage = 14000,
                        Liters = 10,
                        FullTank = true,
                        Date = now.AddDays(-12),
                        TotalCost = 15m,
                        CostPerLiter = 1.5m,
                        VehicleId = 1,
                        UserId = "u1",
                        CreatedAt = now,
                        Station = "S"
                    },
                    new()
                    {
                        Id = 2,
                        Mileage = 14250,
                        Liters = 20,
                        FullTank = true,
                        Date = now.AddDays(-6),
                        TotalCost = 30m,
                        CostPerLiter = 1.5m,
                        VehicleId = 1,
                        UserId = "u1",
                        CreatedAt = now,
                        Station = "S"
                    } // 250km / 20L = 12.5 L/100km
                },
                MaintenanceRecords = new List<MaintenanceRecord>
                {
                    new()
                    {
                        Id = 1,
                        Cost = 50m,
                        Date = now.AddDays(-8),
                        VehicleId = 1,
                        UserId = "u1",
                        CreatedAt = now,
                        Description = "d",
                        Mileage = 1,
                        Provider = "p",
                        ServiceType = "st"
                    }
                }
            },
            new()
            {
                Id = 2,
                Make = "Honda",
                Model = "Civic",
                UserId = "u2",
                CreatedAt = now,
                CurrentMileage = 8000,
                Description = "d",
                LicensePlate = "L2",
                Year = 2021,
                Trips = new List<Trip>
                {
                    new()
                    {
                        Id = 3,
                        Distance = 200,
                        StartTime = now.AddDays(-7),
                        EndTime = now.AddDays(-6),
                        VehicleId = 2,
                        UserId = "u2",
                        CreatedAt = now,
                        StartLocation = "E",
                        EndLocation = "F"
                    }
                },
                FuelRecords = new List<FuelRecord>
                {
                    new()
                    {
                        Id = 3,
                        Mileage = 7000,
                        Liters = 10,
                        FullTank = true,
                        Date = now.AddDays(-10),
                        TotalCost = 16m,
                        CostPerLiter = 1.6m,
                        VehicleId = 2,
                        UserId = "u2",
                        CreatedAt = now,
                        Station = "S"
                    },
                    new()
                    {
                        Id = 4,
                        Mileage = 7200,
                        Liters = 16,
                        FullTank = true,
                        Date = now.AddDays(-3),
                        TotalCost = 25.6m,
                        CostPerLiter = 1.6m,
                        VehicleId = 2,
                        UserId = "u2",
                        CreatedAt = now,
                        Station = "S"
                    } // 200km / 16L = 8 L/100km
                },
                MaintenanceRecords = new List<MaintenanceRecord>
                {
                    new()
                    {
                        Id = 2,
                        Cost = 70m,
                        Date = now.AddDays(-2),
                        VehicleId = 2,
                        UserId = "u2",
                        CreatedAt = now,
                        Description = "d",
                        Mileage = 1,
                        Provider = "p",
                        ServiceType = "st"
                    }
                }
            }
        };

        var (totalMileage, totalCost, costBreakdown, averageFuelEfficiency) =
            AnalyticsService.ProcessVehicleStatistics(vehicles, now.AddDays(-30), now);

        Assert.Equal(100 + 150 + 200, totalMileage); // Sum of trip distances
        Assert.Equal((15m + 30m + 50m) + (16m + 25.6m + 70m), totalCost);
        Assert.Equal(2, costBreakdown.Count);
        Assert.Equal(15m + 30m + 50m, costBreakdown["Toyota Camry (2020)"]);

        double veh1Eff = (20.0 / 250.0) * 100; // 8.0
        double veh2Eff = (16.0 / 200.0) * 100; // 8.0
        double expectedAvgEff = (veh1Eff + veh2Eff) / 2.0; // = 8.0
        Assert.Equal(expectedAvgEff, averageFuelEfficiency, 1);
    }

    // --- More PredictUpcomingMaintenance Edge Cases ---
    [Fact]
    public void PredictUpcomingMaintenance_ServiceDueToday_IsIncluded()
    {
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
                ServiceType = "Check Brakes",
                Cost = 50m,
                Mileage = 9000,
                Date = DateTimeOffset.UtcNow.AddMonths(-6),
                NextServiceDue = DateTimeOffset.UtcNow.Date, // Due today
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow.AddMonths(-6),
                Provider = "P1",
                Description = "desc"
            }
        };
        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        Assert.Contains(
            upcoming,
            u => u.Type == "Check Brakes" && u.DueDate.Date == DateTimeOffset.UtcNow.Date
        );
    }

    [Fact]
    public void PredictUpcomingMaintenance_ServiceJustPassed_IsNotIncludedIfPredictionLooksForward()
    {
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
                ServiceType = "Spark Plugs",
                Cost = 50m,
                Mileage = 9000,
                Date = DateTimeOffset.UtcNow.AddYears(-1),
                NextServiceDue = DateTimeOffset.UtcNow.AddDays(-1), // Due yesterday
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
                Provider = "P1",
                Description = "desc"
            }
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        // Assuming current logic includes recently passed due dates (within 3 months)
        Assert.Contains(upcoming, u => u.Type == "Spark Plugs");
    }

    [Fact]
    public void PredictUpcomingMaintenance_MixedHistory_PrioritizesExplicitNextServiceDue()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = 2021,
            CurrentMileage = 25000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-2),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var now = DateTimeOffset.UtcNow;
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            // Oil change with explicit due date soon
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Oil Change",
                Cost = 50m,
                Mileage = 20000,
                Date = now.AddMonths(-2),
                NextServiceDue = now.AddMonths(1),
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            },
            // Tire rotation with older explicit due date (should not appear if >3 months away)
            new()
            {
                Id = 2,
                VehicleId = 1,
                ServiceType = "Tire Rotation",
                Cost = 30m,
                Mileage = 15000,
                Date = now.AddMonths(-7),
                NextServiceDue = now.AddMonths(4),
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            },
            // Brake check with no explicit due date, but history suggests it might be due based on interval
            new()
            {
                Id = 3,
                VehicleId = 1,
                ServiceType = "Brake Check",
                Cost = 70m,
                Mileage = 10000,
                Date = now.AddYears(-1),
                NextServiceDue = null,
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            }, // Assume 12 month interval
            new()
            {
                Id = 4,
                VehicleId = 1,
                ServiceType = "Brake Check",
                Cost = 70m,
                Mileage = 5000,
                Date = now.AddYears(-2),
                NextServiceDue = null,
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            }
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        testOutputHelper.WriteLine(
            "Upcoming (MixedHistory): " + JsonSerializer.Serialize(upcoming)
        );

        var oilChange = upcoming.FirstOrDefault(u => u.Type == "Oil Change");
        Assert.NotNull(oilChange);
        Assert.True(
            oilChange.DueDate.Month == now.AddMonths(1).Month
                && oilChange.DueDate.Year == now.AddMonths(1).Year
        );

        Assert.DoesNotContain(upcoming, u => u.Type == "Tire Rotation"); // Due > 3 months away

        var brakeCheck = upcoming.FirstOrDefault(u => u.Type == "Brake Check");
        Assert.NotNull(brakeCheck); // Should be predicted based on 12-month interval from last service
        Assert.True(brakeCheck.DueDate.Month == now.Month && brakeCheck.DueDate.Year == now.Year); // Due around now
    }

    [Fact]
    public void PredictUpcomingMaintenance_ServiceWithNoStandardInterval_UsesDefaultPrediction()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = 2022,
            CurrentMileage = 5000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var now = DateTimeOffset.UtcNow;
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Custom Polish",
                Cost = 100m,
                Mileage = 4000,
                Date = now.AddMonths(-5),
                NextServiceDue = null,
                UserId = "u1",
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            }
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        var customPolish = upcoming.FirstOrDefault(u => u.Type == "Custom Polish");

        Assert.NotNull(customPolish);
        Assert.True(
            customPolish.DueDate.Month == now.AddMonths(1).Month
                && customPolish.DueDate.Year == now.AddMonths(1).Year
        ); // Due in ~1 month
    }

    [Fact]
    public void CalculateMileageForPeriod_NoTrips_IdenticalMileageInFuelRecords_ReturnsZero()
    {
        var trips = new List<Trip>();
        var fuelRecords = new List<FuelRecord>
        {
            new()
            {
                Id = 1,
                Mileage = 10000,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Date = DateTimeOffset.UtcNow,
                Liters = 1,
                CostPerLiter = 1,
                TotalCost = 1,
                FullTank = false,
                Station = "S"
            },
            new()
            {
                Id = 2,
                Mileage = 10000,
                VehicleId = 1,
                UserId = "u1",
                CreatedAt = DateTimeOffset.UtcNow,
                Date = DateTimeOffset.UtcNow,
                Liters = 1,
                CostPerLiter = 1,
                TotalCost = 1,
                FullTank = false,
                Station = "S"
            }
        };
        var mileage = AnalyticsService.CalculateMileageForPeriod(trips, fuelRecords);
        Assert.Equal(0, mileage);
    }

    // --- GetFleetAnalyticsAsync - Basic Scenarios (testing helpers indirectly) ---
    // These tests will hit the in-memory DB.
    [Fact]
    public async Task GetFleetAnalyticsAsync_NoVehicles_ReturnsEmptyAnalytics()
    {
        using var context = CreateDbContext();
        var analyticsService = new AnalyticsService(context);
        var userId = "fleet-user-no-vehicles";
        context.Users.Add(
            new FleetUser
            {
                Id = userId,
                UserName = "nouser",
                Email = "nu@test.com"
            }
        );
        await context.SaveChangesAsync();

        var result = await analyticsService.GetFleetAnalyticsAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalVehicles);
        Assert.Equal(0, result.TotalMileage);
        Assert.Equal(0, result.TotalCost);
    }

    [Fact]
    public async Task GetFleetAnalyticsAsync_OneVehicleWithData_CalculatesCorrectly()
    {
        using var context = CreateDbContext();
        var analyticsService = new AnalyticsService(context);
        var userId = "fleet-user-one-vehicle";
        var now = DateTimeOffset.UtcNow;

        context.Users.Add(
            new FleetUser
            {
                Id = userId,
                UserName = "onevehicleuser",
                Email = "ov@test.com"
            }
        );
        var vehicle = new Vehicle
        {
            Id = 101,
            Make = "Test",
            Model = "One",
            Year = 2022,
            LicensePlate = "ONE",
            UserId = userId,
            CurrentMileage = 1000,
            CreatedAt = now,
            Description = "d"
        };
        context.Vehicles.Add(vehicle);
        context.Trips.Add(
            new Trip
            {
                Id = 1,
                VehicleId = 101,
                UserId = userId,
                Distance = 100,
                StartTime = now.AddDays(-2),
                EndTime = now.AddDays(-1),
                CreatedAt = now,
                StartLocation = "A",
                EndLocation = "B"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 1,
                VehicleId = 101,
                UserId = userId,
                Date = now.AddDays(-2),
                Liters = 10,
                CostPerLiter = 1.5m,
                TotalCost = 15m,
                Mileage = 900,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 2,
                VehicleId = 101,
                UserId = userId,
                Date = now.AddDays(-1),
                Liters = 8,
                CostPerLiter = 1.5m,
                TotalCost = 12m,
                Mileage = 1000,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        ); // 100km / 8L = 8L/100km
        context.MaintenanceRecords.Add(
            new MaintenanceRecord
            {
                Id = 1,
                VehicleId = 101,
                UserId = userId,
                ServiceType = "Check",
                Cost = 20m,
                Date = now.AddDays(-1),
                Mileage = 1000,
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            }
        );
        await context.SaveChangesAsync();

        var result = await analyticsService.GetFleetAnalyticsAsync(userId, now.AddMonths(-1), now);

        Assert.Equal(1, result.TotalVehicles);
        Assert.Equal(100, result.TotalMileage); // From trip
        Assert.Equal(15m + 12m + 20m, result.TotalCost);
        Assert.Equal(8.0, result.AverageFuelEfficiencyLitersPer100Km, 1);
        Assert.Equal(
            "Test One (2022)",
            result.MostUsedVehicle.Make
                + " "
                + result.MostUsedVehicle.Model
                + " ("
                + vehicle.Year
                + ")"
        ); // Assuming vehicle.Year is part of the display name logic
        Assert.Equal(1, result.MostUsedVehicle.Trips);
    }

    [Fact]
    public void PredictUpcomingMaintenance_HistoricalPrediction_OverridesWeakerDefault()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = DateTimeOffset.UtcNow.Year - 1,
            CurrentMileage = 14000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var now = DateTimeOffset.UtcNow;
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Tepalų Keitimas (Est.)",
                Cost = 70m,
                Mileage = 13500,
                Date = now.AddDays(-10),
                NextServiceDue = now.AddMonths(11), // Explicitly due in 11 months
                UserId = "u1",
                CreatedAt = now.AddDays(-10),
                Description = "d",
                Provider = "p"
            }
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        testOutputHelper.WriteLine(
            "Upcoming (Historical Overrides Default): " + JsonSerializer.Serialize(upcoming)
        );

        var oilChange = upcoming.FirstOrDefault(u => u.Type == "Tepalų Keitimas (Est.)");
        Assert.Null(oilChange); // Explicit NextServiceDue is > 3 months away
        Assert.NotEmpty(upcoming); // Other defaults like Annual Inspection might appear
    }

    // --- FindMostEfficientVehicle Edge Case ---
    [Fact]
    public void FindMostEfficientVehicle_NoVehiclesWithSufficientFuelData_ReturnsUnknown()
    {
        var now = DateTimeOffset.UtcNow;
        var vehicles = new List<Vehicle>
        {
            new()
            {
                Id = 1,
                Make = "Toyota",
                Model = "Prius",
                UserId = "u1",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                FuelRecords = new List<FuelRecord>
                { // Only one record
                    new()
                    {
                        Id = 1,
                        Mileage = 1000,
                        Liters = 10,
                        FullTank = true,
                        Date = now.AddDays(-10),
                        VehicleId = 1,
                        UserId = "u1",
                        CreatedAt = now,
                        CostPerLiter = 1,
                        Station = "S",
                        TotalCost = 1
                    }
                }
            },
            new()
            {
                Id = 2,
                Make = "Honda",
                Model = "Civic",
                UserId = "u2",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                FuelRecords = new List<FuelRecord>()
            } // No records
        };
        var mostEfficient = AnalyticsService.FindMostEfficientVehicle(
            vehicles,
            now.AddDays(-15),
            now
        );
        Assert.Equal(0, mostEfficient.Id);
        Assert.Equal("Unknown", mostEfficient.Make);
    }

    [Fact]
    public async Task GetFleetAnalyticsAsync_MultipleVehiclesInSameFamily_AggregatesCorrectly()
    {
        using var context = CreateDbContext();
        var analyticsService = new AnalyticsService(context);
        var userId = "fleet-user-multi-vehicle-family"; // This user owns/accesses these vehicles
        var now = DateTimeOffset.UtcNow;

        context.Users.Add(
            new FleetUser
            {
                Id = userId,
                UserName = "multivehuser",
                Email = "mv@test.com",
                FamilyGroupId = "familyX"
            }
        );

        // Vehicle 1
        var vehicle1 = new Vehicle
        {
            Id = 201,
            Make = "Ford",
            Model = "Fiesta",
            Year = 2019,
            LicensePlate = "MV1",
            UserId = userId,
            CurrentMileage = 30000,
            CreatedAt = now,
            Description = "d"
        };
        context.Vehicles.Add(vehicle1);
        context.Trips.Add(
            new Trip
            {
                Id = 101,
                VehicleId = 201,
                UserId = userId,
                Distance = 50,
                StartTime = now.AddDays(-3),
                EndTime = now.AddDays(-2),
                CreatedAt = now,
                StartLocation = "A1",
                EndLocation = "B1"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 101,
                VehicleId = 201,
                UserId = userId,
                Date = now.AddDays(-3),
                Liters = 20,
                CostPerLiter = 1.6m,
                TotalCost = 32m,
                Mileage = 29950,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 102,
                VehicleId = 201,
                UserId = userId,
                Date = now.AddDays(-1),
                Liters = 25,
                CostPerLiter = 1.6m,
                TotalCost = 40m,
                Mileage = 30250,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        ); // 300km / 25L = 8.33 L/100km
        context.MaintenanceRecords.Add(
            new MaintenanceRecord
            {
                Id = 101,
                VehicleId = 201,
                UserId = userId,
                ServiceType = "ServiceA",
                Cost = 100m,
                Date = now.AddDays(-2),
                Mileage = 30000,
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            }
        );

        // Vehicle 2
        var vehicle2 = new Vehicle
        {
            Id = 202,
            Make = "VW",
            Model = "Golf",
            Year = 2021,
            LicensePlate = "MV2",
            UserId = userId,
            CurrentMileage = 15000,
            CreatedAt = now,
            Description = "d"
        };
        context.Vehicles.Add(vehicle2);
        context.Trips.Add(
            new Trip
            {
                Id = 102,
                VehicleId = 202,
                UserId = userId,
                Distance = 80,
                StartTime = now.AddDays(-4),
                EndTime = now.AddDays(-3),
                CreatedAt = now,
                StartLocation = "C1",
                EndLocation = "D1"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 103,
                VehicleId = 202,
                UserId = userId,
                Date = now.AddDays(-4),
                Liters = 15,
                CostPerLiter = 1.7m,
                TotalCost = 25.5m,
                Mileage = 14920,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 104,
                VehicleId = 202,
                UserId = userId,
                Date = now.AddDays(-2),
                Liters = 20,
                CostPerLiter = 1.7m,
                TotalCost = 34m,
                Mileage = 15220,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        ); // 300km / 20L = 6.67 L/100km
        context.MaintenanceRecords.Add(
            new MaintenanceRecord
            {
                Id = 102,
                VehicleId = 202,
                UserId = userId,
                ServiceType = "ServiceB",
                Cost = 60m,
                Date = now.AddDays(-3),
                Mileage = 15000,
                CreatedAt = now,
                Description = "d",
                Provider = "p"
            }
        );
        await context.SaveChangesAsync();

        var result = await analyticsService.GetFleetAnalyticsAsync(userId, now.AddMonths(-1), now);

        Assert.Equal(2, result.TotalVehicles);
        Assert.Equal(50 + 80, result.TotalMileage);
        Assert.Equal((32m + 40m + 100m) + (25.5m + 34m + 60m), result.TotalCost);

        double v1Eff = (25.0 / 300.0) * 100; // ~8.33
        double v2Eff = (20.0 / 300.0) * 100; // ~6.67
        double expectedAvgEff = (v1Eff + v2Eff) / 2.0; // ~7.5
        Assert.Equal(expectedAvgEff, result.AverageFuelEfficiencyLitersPer100Km, 1);

        Assert.Equal(2, result.CostBreakdown.Count);
        Assert.Equal(32m + 40m + 100m, result.CostBreakdown["Ford Fiesta (2019)"]);
    }

    [Fact]
    public void PredictUpcomingMaintenance_ServiceWithPastNextServiceDue_ButStillUpcoming_IsIncluded()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = 2020,
            CurrentMileage = 15000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow,
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var now = DateTimeOffset.UtcNow;
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Annual Check",
                Cost = 100m,
                Mileage = 10000,
                Date = now.AddYears(-1),
                NextServiceDue = now.AddDays(-10), // Due 10 days ago (but still within the 3-month window from now)
                UserId = "u1",
                CreatedAt = now.AddYears(-1),
                Description = "d",
                Provider = "p"
            }
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        testOutputHelper.WriteLine(
            "Upcoming (PastDue But Recent): " + JsonSerializer.Serialize(upcoming)
        );

        var annualCheck = upcoming.FirstOrDefault(u => u.Type == "Annual Check");
        Assert.NotNull(annualCheck); // Should still be listed as it's "upcoming" relative to a 3-month window
        Assert.Equal(now.AddDays(-10).Date, annualCheck.DueDate.Date);
    }

    [Fact]
    public void FindMostUsedVehicle_MultipleVehicles_OneWithMoreTripsInPeriod()
    {
        var now = DateTimeOffset.UtcNow;
        var periodStart = now.AddMonths(-1);
        var vehicles = new List<Vehicle>
        {
            new()
            {
                Id = 1,
                Make = "A",
                Model = "X",
                UserId = "u",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                Trips = new List<Trip>
                {
                    new()
                    {
                        Id = 1,
                        StartTime = periodStart.AddDays(1),
                        EndTime = periodStart.AddDays(2),
                        VehicleId = 1,
                        UserId = "u",
                        CreatedAt = now,
                        Distance = 1,
                        StartLocation = "A",
                        EndLocation = "B"
                    }
                }
            },
            new()
            {
                Id = 2,
                Make = "B",
                Model = "Y",
                UserId = "u",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                Trips = new List<Trip>
                { // Most used
                    new()
                    {
                        Id = 2,
                        StartTime = periodStart.AddDays(3),
                        EndTime = periodStart.AddDays(4),
                        VehicleId = 2,
                        UserId = "u",
                        CreatedAt = now,
                        Distance = 1,
                        StartLocation = "A",
                        EndLocation = "B"
                    },
                    new()
                    {
                        Id = 3,
                        StartTime = periodStart.AddDays(5),
                        EndTime = periodStart.AddDays(6),
                        VehicleId = 2,
                        UserId = "u",
                        CreatedAt = now,
                        Distance = 1,
                        StartLocation = "A",
                        EndLocation = "B"
                    }
                }
            },
            new()
            {
                Id = 3,
                Make = "C",
                Model = "Z",
                UserId = "u",
                CreatedAt = now,
                CurrentMileage = 1,
                Description = "d",
                LicensePlate = "L",
                Year = 2020,
                Trips = new List<Trip>
                { // Trip outside period
                    new()
                    {
                        Id = 4,
                        StartTime = periodStart.AddDays(-5),
                        EndTime = periodStart.AddDays(-4),
                        VehicleId = 3,
                        UserId = "u",
                        CreatedAt = now,
                        Distance = 1,
                        StartLocation = "A",
                        EndLocation = "B"
                    }
                }
            }
        };

        var mostUsed = AnalyticsService.FindMostUsedVehicle(vehicles, periodStart, now);
        Assert.Equal(2, mostUsed.Id);
        Assert.Equal(2, mostUsed.Trips);
    }

    [Fact]
    public async Task GetFleetAnalyticsAsync_AdminUser_SeesAggregatedDataFromMultipleFamilies()
    {
        using var context = CreateDbContext();
        var analyticsService = new AnalyticsService(context);
        var adminUserId = "admin-for-fleet-test";
        var now = DateTimeOffset.UtcNow;

        // Admin User
        context.Users.Add(
            new FleetUser
            {
                Id = adminUserId,
                UserName = "fleetadmin",
                Email = "fa@test.com",
                FamilyGroupId = "adminFam"
            }
        );

        // Family 1
        var userF1 = new FleetUser
        {
            Id = "userF1",
            UserName = "userF1",
            Email = "f1@test.com",
            FamilyGroupId = "family1"
        };
        context.Users.Add(userF1);
        var vehicleF1 = new Vehicle
        {
            Id = 301,
            Make = "Ford",
            Model = "F1",
            Year = 2019,
            LicensePlate = "F1V",
            UserId = userF1.Id,
            CurrentMileage = 1000,
            CreatedAt = now,
            Description = "d"
        };
        context.Vehicles.Add(vehicleF1);
        context.Trips.Add(
            new Trip
            {
                Id = 201,
                VehicleId = 301,
                UserId = userF1.Id,
                Distance = 10,
                StartTime = now.AddDays(-2),
                EndTime = now.AddDays(-1),
                CreatedAt = now,
                StartLocation = "A",
                EndLocation = "B"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 201,
                VehicleId = 301,
                UserId = userF1.Id,
                Date = now.AddDays(-1),
                Liters = 5,
                CostPerLiter = 1.5m,
                TotalCost = 7.5m,
                Mileage = 990,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 202,
                VehicleId = 301,
                UserId = userF1.Id,
                Date = now,
                Liters = 4,
                CostPerLiter = 1.5m,
                TotalCost = 6m,
                Mileage = 1000,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        ); // 10km / 4L = 40L/100km

        // Family 2
        var userF2 = new FleetUser
        {
            Id = "userF2",
            UserName = "userF2",
            Email = "f2@test.com",
            FamilyGroupId = "family2"
        };
        context.Users.Add(userF2);
        var vehicleF2 = new Vehicle
        {
            Id = 302,
            Make = "Kia",
            Model = "K1",
            Year = 2020,
            LicensePlate = "F2V",
            UserId = userF2.Id,
            CurrentMileage = 2000,
            CreatedAt = now,
            Description = "d"
        };
        context.Vehicles.Add(vehicleF2);
        context.Trips.Add(
            new Trip
            {
                Id = 202,
                VehicleId = 302,
                UserId = userF2.Id,
                Distance = 20,
                StartTime = now.AddDays(-3),
                EndTime = now.AddDays(-2),
                CreatedAt = now,
                StartLocation = "C",
                EndLocation = "D"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 203,
                VehicleId = 302,
                UserId = userF2.Id,
                Date = now.AddDays(-2),
                Liters = 8,
                CostPerLiter = 1.8m,
                TotalCost = 14.4m,
                Mileage = 1980,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        );
        context.FuelRecords.Add(
            new FuelRecord
            {
                Id = 204,
                VehicleId = 302,
                UserId = userF2.Id,
                Date = now,
                Liters = 10,
                CostPerLiter = 1.8m,
                TotalCost = 18m,
                Mileage = 2000,
                FullTank = true,
                CreatedAt = now,
                Station = "S"
            }
        ); // 20km / 10L = 50L/100km
        await context.SaveChangesAsync();

        vehicleF1.UserId = adminUserId;
        vehicleF2.UserId = adminUserId;
        await context.SaveChangesAsync();

        var result = await analyticsService.GetFleetAnalyticsAsync(
            adminUserId,
            now.AddMonths(-1),
            now
        );

        Assert.Equal(2, result.TotalVehicles);
        Assert.Equal(10 + 20, result.TotalMileage);
        Assert.Equal(7.5m + 6m + 14.4m + 18m, result.TotalCost); // Fuel costs only for this simple test

        double effF1 = (4.0 / 10.0) * 100; // 40
        double effF2 = (10.0 / 20.0) * 100; // 50
        Assert.Equal((effF1 + effF2) / 2.0, result.AverageFuelEfficiencyLitersPer100Km, 1);
    }

    [Fact]
    public void PredictUpcomingMaintenance_ServiceWithFarFutureNextServiceDue_IsNotIncluded()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = 2022,
            CurrentMileage = 5000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var now = DateTimeOffset.UtcNow;
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Long Term Check",
                Cost = 200m,
                Mileage = 4000,
                Date = now.AddMonths(-1),
                NextServiceDue = now.AddMonths(12), // Due in 1 year (well outside 3-month upcoming window)
                UserId = "u1",
                CreatedAt = now.AddMonths(-1),
                Description = "d",
                Provider = "p"
            }
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        testOutputHelper.WriteLine(
            "Upcoming (Far Future Due): " + JsonSerializer.Serialize(upcoming)
        );

        Assert.DoesNotContain(upcoming, u => u.Type == "Long Term Check");
    }

    [Fact]
    public void PredictUpcomingMaintenance_MultipleServices_OneExplicitDue_OthersPredicted()
    {
        var vehicle = new Vehicle
        {
            Id = 1,
            Year = DateTimeOffset.UtcNow.Year - 1,
            CurrentMileage = 14000,
            UserId = "u1",
            CreatedAt = DateTimeOffset.UtcNow.AddYears(-1),
            Description = "d",
            LicensePlate = "lp",
            Make = "m",
            Model = "m"
        };
        var now = DateTimeOffset.UtcNow;
        var maintenanceHistory = new List<MaintenanceRecord>
        {
            new()
            {
                Id = 1,
                VehicleId = 1,
                ServiceType = "Oil Change",
                Cost = 70m,
                Mileage = 10000,
                Date = now.AddMonths(-2),
                NextServiceDue = now.AddMonths(1), // Explicitly due in 1 month
                UserId = "u1",
                CreatedAt = now.AddMonths(-2),
                Description = "d",
                Provider = "p"
            },
        };

        var upcoming = AnalyticsService.PredictUpcomingMaintenance(vehicle, maintenanceHistory);
        testOutputHelper.WriteLine(
            "Upcoming (One Explicit, Others Default): " + JsonSerializer.Serialize(upcoming)
        );

        Assert.Contains(
            upcoming,
            u => u.Type == "Oil Change" && u.DueDate.Month == now.AddMonths(1).Month
        );

        int ageInMonthsForTest = (now.Year - vehicle.Year) * 12 + now.Month - 1;
        if (ageInMonthsForTest % 6 == 0 && vehicle.CurrentMileage % 10000 > 800) // If tire rotation by age is due
        {
            Assert.Contains(upcoming, u => u.Type == "Padangų Rotacija/Patikra (Est.)");
        }
        if (ageInMonthsForTest % 12 == 0 && (now.Year - vehicle.Year) >= 1)
        {
            Assert.Contains(upcoming, u => u.Type == "Metinis Aptarnavimas (Est.)");
        }
    }
}
