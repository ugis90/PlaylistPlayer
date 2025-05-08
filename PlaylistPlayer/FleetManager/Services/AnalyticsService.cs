// FleetManager/Services/AnalyticsService.cs
using FleetManager.Data;
using FleetManager.Data.DTOs;
using FleetManager.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System; // Add this for DateTimeOffset, Math, etc.
using System.Collections.Generic; // Add this for List, Dictionary
using System.Linq; // Add this for LINQ methods like Sum, Average, GroupBy, SelectMany etc.
using System.Threading.Tasks; // Add this for Task

namespace FleetManager.Services;

public class AnalyticsService(FleetDbContext dbContext)
{
    private const string UnknownVehicleLabel = "Unknown";

    public async Task<VehicleAnalyticsDto> GetVehicleAnalyticsAsync(
        int vehicleId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null
    )
    {
        var start = startDate ?? DateTimeOffset.UtcNow.AddYears(-1);
        var end = endDate ?? DateTimeOffset.UtcNow;

        // *** FIX: Include MaintenanceRecords directly from Vehicle ***
        var vehicle = await dbContext.Vehicles
            .Include(v => v.Trips) // Still need trips for trip count and mileage
            .Include(v => v.MaintenanceRecords) // Include maintenance records directly
            .Include(v => v.FuelRecords)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            throw new KeyNotFoundException($"Vehicle with ID {vehicleId} not found");
        }

        // Filter trips by date range
        var tripsInPeriod = vehicle.Trips
            .Where(t => t.StartTime >= start && t.EndTime <= end)
            .ToList();

        // *** FIX: Filter MaintenanceRecords directly from the vehicle based on date ***
        var maintenanceRecordsInPeriod = vehicle.MaintenanceRecords
            .Where(m => m.Date >= start && m.Date <= end)
            .ToList();

        // Filter fuel records by date range
        var fuelRecordsInPeriod = vehicle.FuelRecords
            .Where(f => f.Date >= start && f.Date <= end)
            .ToList();

        // Calculate total cost
        var maintenanceCosts = maintenanceRecordsInPeriod.Sum(m => m.Cost);
        var fuelCosts = fuelRecordsInPeriod.Sum(f => f.TotalCost);
        var totalCost = maintenanceCosts + fuelCosts;

        // Calculate total mileage for the period (using trips or fuel records)
        var mileage = CalculateMileageForPeriod(tripsInPeriod, fuelRecordsInPeriod);

        // Calculate cost per mile
        decimal costPerMile = mileage > 0 ? totalCost / (decimal)mileage : 0; // Cast mileage to decimal

        // Calculate fuel efficiency (MPG)
        double fuelEfficiency = CalculateAverageFuelEfficiency(fuelRecordsInPeriod);

        // Analyze upcoming maintenance (using all maintenance history for the vehicle)
        var upcomingMaintenance = PredictUpcomingMaintenance(
            vehicle,
            vehicle.MaintenanceRecords.ToList()
        ); // Use all records for prediction

        // Generate fuel efficiency trend
        var fuelEfficiencyTrend = CalculateFuelEfficiencyTrend(fuelRecordsInPeriod);

        // Calculate cost by category
        var costByCategory = new CostByCategoryDto(fuelCosts, maintenanceCosts, 0);

        // Calculate cost by month
        var costByMonth = CalculateCostByMonth(maintenanceRecordsInPeriod, fuelRecordsInPeriod);

        return new VehicleAnalyticsDto(
            totalCost,
            mileage,
            costPerMile,
            tripsInPeriod.Count, // Use Count() method
            fuelEfficiency,
            maintenanceCosts,
            fuelCosts,
            upcomingMaintenance,
            fuelEfficiencyTrend,
            costByCategory,
            costByMonth
        );
    }

    public async Task<FleetAnalyticsDto> GetFleetAnalyticsAsync(
        string userId,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null
    )
    {
        var start = startDate ?? DateTimeOffset.UtcNow.AddYears(-1);
        var end = endDate ?? DateTimeOffset.UtcNow;

        // *** FIX: Include MaintenanceRecords directly from Vehicle ***
        var vehicles = await dbContext.Vehicles
            .Where(v => v.UserId == userId)
            .Include(v => v.Trips)
            .Include(v => v.MaintenanceRecords) // Include maintenance records
            .Include(v => v.FuelRecords)
            .ToListAsync();

        if (vehicles.Count == 0)
        {
            // Return a default/empty DTO instead of throwing an exception
            // This might be better UX if a user has no vehicles yet.
            return new FleetAnalyticsDto(
                0,
                0,
                0,
                new Dictionary<string, decimal>(),
                0,
                0,
                new MostUsedVehicleDto(0, "N/A", "N/A", 0),
                new MostEfficientVehicleDto(0, "N/A", "N/A", 0),
                new List<CostTrendDto>(),
                new List<FleetUpcomingMaintenanceDto>()
            );
            // throw new InvalidOperationException("No vehicles found for the user");
        }

        // Process vehicle data
        var (totalMileage, totalCost, costBreakdown, averageFuelEfficiency) =
            ProcessVehicleStatistics(vehicles, start, end);

        // Calculate fleet statistics
        int totalVehicles = vehicles.Count;
        decimal averageCostPerMile = totalMileage > 0 ? totalCost / (decimal)totalMileage : 0; // Cast mileage

        // Find most used and efficient vehicles
        var mostUsedVehicle = FindMostUsedVehicle(vehicles, start, end);
        var mostEfficientVehicle = FindMostEfficientVehicle(vehicles, start, end);

        // Calculate cost trend across all vehicles
        var costTrend = ConvertToTrendData(CalculateFleetCostTrend(vehicles, start, end));

        // Get upcoming maintenance across all vehicles
        var upcomingMaintenance = GetFleetUpcomingMaintenance(vehicles); // Uses all history

        return new FleetAnalyticsDto(
            totalVehicles,
            totalMileage,
            totalCost,
            costBreakdown,
            averageCostPerMile,
            averageFuelEfficiency,
            mostUsedVehicle,
            mostEfficientVehicle,
            costTrend,
            upcomingMaintenance
        );
    }

    private static List<CostTrendDto> ConvertToTrendData(List<CostByMonthDto> costByMonth)
    {
        return costByMonth.Select(c => new CostTrendDto(c.Month, c.Cost)).ToList();
    }

    private static (
        int totalMileage,
        decimal totalCost,
        Dictionary<string, decimal> costBreakdown,
        double averageFuelEfficiency
    ) ProcessVehicleStatistics(List<Vehicle> vehicles, DateTimeOffset start, DateTimeOffset end)
    {
        int totalMileage = 0;
        decimal totalCost = 0;
        var costBreakdown = new Dictionary<string, decimal>();
        double totalFuelEfficiencySum = 0; // Renamed for clarity
        int vehiclesWithFuelData = 0;

        foreach (var vehicle in vehicles)
        {
            var tripsInPeriod = vehicle.Trips
                .Where(t => t.StartTime >= start && t.EndTime <= end)
                .ToList();
            // *** FIX: Filter MaintenanceRecords directly from vehicle ***
            var maintenanceRecordsInPeriod = vehicle.MaintenanceRecords
                .Where(m => m.Date >= start && m.Date <= end)
                .ToList();
            var fuelRecordsInPeriod = vehicle.FuelRecords
                .Where(f => f.Date >= start && f.Date <= end)
                .ToList();

            var vehicleMileage = CalculateMileageForPeriod(tripsInPeriod, fuelRecordsInPeriod);
            totalMileage += vehicleMileage;

            var maintenanceCost = maintenanceRecordsInPeriod.Sum(m => m.Cost);
            var fuelCost = fuelRecordsInPeriod.Sum(f => f.TotalCost);
            var vehicleTotalCost = maintenanceCost + fuelCost;
            totalCost += vehicleTotalCost;

            costBreakdown.Add($"{vehicle.Make} {vehicle.Model} ({vehicle.Year})", vehicleTotalCost);

            if (fuelRecordsInPeriod.Count >= 2) // Need at least 2 records for MPG
            {
                var vehicleEfficiency = CalculateAverageFuelEfficiency(fuelRecordsInPeriod);
                if (vehicleEfficiency > 0) // Only include valid MPG calculations
                {
                    totalFuelEfficiencySum += vehicleEfficiency;
                    vehiclesWithFuelData++;
                }
            }
        }

        double averageFuelEfficiency =
            vehiclesWithFuelData > 0 ? totalFuelEfficiencySum / vehiclesWithFuelData : 0;

        return (totalMileage, totalCost, costBreakdown, averageFuelEfficiency);
    }

    private static MostUsedVehicleDto FindMostUsedVehicle(
        List<Vehicle> vehicles,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        var mostUsed = vehicles
            .Select(
                v =>
                    new
                    {
                        Vehicle = v,
                        TripCount = v.Trips.Count(t => t.StartTime >= start && t.EndTime <= end)
                    }
            ) // Calculate count first
            .Where(x => x.TripCount > 0) // Only consider vehicles with trips in the period
            .OrderByDescending(x => x.TripCount)
            .FirstOrDefault();

        if (mostUsed == null)
        {
            return new MostUsedVehicleDto(0, UnknownVehicleLabel, UnknownVehicleLabel, 0);
        }
        // *** FIX: Use TripCount directly ***
        return new MostUsedVehicleDto(
            mostUsed.Vehicle.Id,
            mostUsed.Vehicle.Make,
            mostUsed.Vehicle.Model,
            mostUsed.TripCount
        );
    }

    private static MostEfficientVehicleDto FindMostEfficientVehicle(
        List<Vehicle> vehicles,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        var vehiclesWithEfficiency = vehicles
            .Select(
                v =>
                    new
                    {
                        Vehicle = v,
                        FuelRecordsInPeriod = v.FuelRecords
                            .Where(f => f.Date >= start && f.Date <= end)
                            .ToList()
                    }
            )
            .Where(x => x.FuelRecordsInPeriod.Count >= 2) // Ensure enough records for calculation
            .Select(
                x =>
                    new
                    {
                        x.Vehicle,
                        Efficiency = CalculateAverageFuelEfficiency(x.FuelRecordsInPeriod)
                    }
            )
            .Where(x => x.Efficiency > 0) // Only consider valid efficiencies
            .OrderByDescending(x => x.Efficiency)
            .FirstOrDefault();

        if (vehiclesWithEfficiency == null)
        {
            return new MostEfficientVehicleDto(0, UnknownVehicleLabel, UnknownVehicleLabel, 0);
        }
        return new MostEfficientVehicleDto(
            vehiclesWithEfficiency.Vehicle.Id,
            vehiclesWithEfficiency.Vehicle.Make,
            vehiclesWithEfficiency.Vehicle.Model,
            vehiclesWithEfficiency.Efficiency
        );
    }

    private static List<CostByMonthDto> CalculateFleetCostTrend(
        List<Vehicle> vehicles,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        // *** FIX: Select MaintenanceRecords directly from vehicles ***
        var allMaintenanceRecords = vehicles
            .SelectMany(v => v.MaintenanceRecords) // Select from vehicle's collection
            .Where(m => m.Date >= start && m.Date <= end) // Filter by date
            .ToList();

        var allFuelRecords = vehicles
            .SelectMany(v => v.FuelRecords)
            .Where(f => f.Date >= start && f.Date <= end)
            .ToList();

        return CalculateCostByMonth(allMaintenanceRecords, allFuelRecords);
    }

    private static List<FleetUpcomingMaintenanceDto> GetFleetUpcomingMaintenance(
        List<Vehicle> vehicles
    )
    {
        var upcomingMaintenance = new List<FleetUpcomingMaintenanceDto>();
        foreach (var vehicle in vehicles)
        {
            // *** FIX: Use MaintenanceRecords directly from vehicle ***
            // Pass *all* records for the vehicle to PredictUpcomingMaintenance
            var vehicleUpcoming = PredictUpcomingMaintenance(
                vehicle,
                vehicle.MaintenanceRecords.ToList()
            );
            upcomingMaintenance.AddRange(
                vehicleUpcoming.Select(
                    m => new FleetUpcomingMaintenanceDto(vehicle.Id, m.Type, m.DueDate)
                )
            );
        }
        return upcomingMaintenance.OrderBy(m => m.DueDate).ToList();
    }

    // CalculateMileageForPeriod remains the same (uses Trips or Fuel)
    public static int CalculateMileageForPeriod(List<Trip> trips, List<FuelRecord> fuelRecords)
    {
        if (trips.Any())
            return (int)trips.Sum(t => t.Distance);
        if (fuelRecords.Count < 2)
            return 0;
        var minMileage = fuelRecords.Min(f => f.Mileage);
        var maxMileage = fuelRecords.Max(f => f.Mileage);
        return maxMileage - minMileage;
    }

    // CalculateAverageFuelEfficiency remains the same
    public static double CalculateAverageFuelEfficiency(List<FuelRecord> fuelRecords)
    {
        if (fuelRecords.Count < 2)
            return 0;
        var sortedRecords = fuelRecords.OrderBy(f => f.Mileage).ToList();
        var fullTankFillups = sortedRecords.Where(f => f.FullTank).ToList();
        if (fullTankFillups.Count < 2)
            fullTankFillups = sortedRecords; // Fallback
        double totalDistance = 0;
        double totalGallons = 0;
        for (int i = 1; i < fullTankFillups.Count; i++)
        {
            var current = fullTankFillups[i];
            var previous = fullTankFillups[i - 1];
            var distance = current.Mileage - previous.Mileage;
            if (distance <= 0 || distance >= 1000)
                continue; // Sanity check
            totalDistance += distance;
            totalGallons += current.Gallons;
        }
        return totalGallons > 0 ? Math.Round(totalDistance / totalGallons, 1) : 0;
    }

    private static List<UpcomingMaintenanceDto> PredictUpcomingMaintenance(
        Vehicle vehicle,
        List<MaintenanceRecord> maintenanceHistory
    )
    {
        var currentDate = DateTimeOffset.UtcNow;
        var alerts = new List<UpcomingMaintenanceDto>();

        // Only process if we have maintenance history
        if (!maintenanceHistory.Any())
        {
            return GetDefaultMaintenanceSchedule(vehicle);
        }

        // Group by service type
        var serviceGroups = maintenanceHistory
            .GroupBy(m => m.ServiceType)
            .ToDictionary(g => g.Key, g => g.ToList());

        ProcessExistingMaintenanceRecords(serviceGroups, vehicle, currentDate, alerts);

        // Add default maintenance if some common types aren't in history
        AddMissingMaintenanceTypes(alerts, serviceGroups.Keys.ToList(), vehicle);

        return alerts.OrderBy(a => a.DueDate).ToList();
    }

    // Breaking down the PredictUpcomingMaintenance method to reduce complexity
    private static void ProcessExistingMaintenanceRecords(
        Dictionary<string, List<MaintenanceRecord>> serviceGroups,
        Vehicle vehicle,
        DateTimeOffset currentDate,
        List<UpcomingMaintenanceDto> alerts
    )
    {
        // Check each service type for upcoming maintenance
        foreach (var serviceGroup in serviceGroups)
        {
            var serviceType = serviceGroup.Key;
            var records = serviceGroup.Value.OrderByDescending(r => r.Date).ToList();

            if (records.Count == 0)
                continue;

            var latestRecord = records[0]; // Use indexing instead of First()

            // Check if the record has a next service due date
            if (latestRecord.NextServiceDue.HasValue)
            {
                if (latestRecord.NextServiceDue.Value <= currentDate.AddMonths(3))
                {
                    alerts.Add(
                        new UpcomingMaintenanceDto(
                            serviceType,
                            latestRecord.NextServiceDue.Value,
                            EstimateServiceCost(serviceType, records)
                        )
                    );
                }
            }
            else
            {
                // Predict based on service type and historical pattern
                var predictedDate = PredictNextServiceDate(
                    serviceType,
                    records,
                    vehicle.CurrentMileage
                );
                if (predictedDate.HasValue && predictedDate.Value <= currentDate.AddMonths(3))
                {
                    alerts.Add(
                        new UpcomingMaintenanceDto(
                            serviceType,
                            predictedDate.Value,
                            EstimateServiceCost(serviceType, records)
                        )
                    );
                }
            }
        }
    }

    public static List<UpcomingMaintenanceDto> GetDefaultMaintenanceSchedule(Vehicle vehicle)
    {
        var currentDate = DateTimeOffset.UtcNow;
        var alerts = new List<UpcomingMaintenanceDto>();

        // No maintenance history - recommend based on vehicle age and mileage
        int ageInMonths = (currentDate.Year - vehicle.Year) * 12 + currentDate.Month - 1;

        // Oil change (every 3 months or 3000 miles)
        if (ageInMonths % 3 == 0 || vehicle.CurrentMileage % 3000 <= 500)
        {
            alerts.Add(new UpcomingMaintenanceDto("Oil Change", currentDate.AddDays(15), 45.99m));
        }

        // Tire rotation (every 6 months or 6000-8000 miles)
        if (ageInMonths % 6 == 0 || vehicle.CurrentMileage % 6000 <= 500)
        {
            alerts.Add(
                new UpcomingMaintenanceDto("Tire Rotation", currentDate.AddDays(30), 25.00m)
            );
        }

        // Yearly maintenance check
        if (ageInMonths % 12 == 0)
        {
            alerts.Add(
                new UpcomingMaintenanceDto("Annual Inspection", currentDate.AddDays(30), 89.99m)
            );
        }

        return alerts;
    }

    private static DateTimeOffset? PredictNextServiceDate(
        string serviceType,
        List<MaintenanceRecord> serviceHistory,
        int currentMileage
    )
    {
        if (serviceHistory.Count < 2)
        {
            // Not enough history to predict interval
            return PredictBasedOnStandardIntervals(serviceType, serviceHistory);
        }

        // Calculate average time between services
        var intervals = new List<TimeSpan>();
        var mileageIntervals = new List<int>();

        for (int i = 0; i < serviceHistory.Count - 1; i++)
        {
            var current = serviceHistory[i];
            var next = serviceHistory[i + 1];

            intervals.Add(current.Date - next.Date);
            mileageIntervals.Add(current.Mileage - next.Mileage);
        }

        // Calculate average interval
        var avgTimeInterval = new TimeSpan((long)intervals.Average(i => i.Ticks));
        var avgMileageInterval = (int)mileageIntervals.Average();

        // Get latest service record
        var latestService = serviceHistory[0]; // Use indexing instead of First()

        // Predict based on time
        var predictedTimeDate = latestService.Date + avgTimeInterval;

        // Predict based on mileage
        var mileageDiff = currentMileage - latestService.Mileage;
        var remainingMileage = avgMileageInterval - mileageDiff;

        // Estimate time to reach mileage threshold (assuming avg 1000 miles/month)
        var monthsToMileageThreshold = Math.Max(0, (double)remainingMileage / 1000);
        var predictedMileageDate = DateTimeOffset.UtcNow.AddMonths((int)monthsToMileageThreshold);

        // Return the earlier of the two predictions
        return predictedTimeDate < predictedMileageDate ? predictedTimeDate : predictedMileageDate;
    }

    public static DateTimeOffset? PredictBasedOnStandardIntervals(
        string serviceType,
        List<MaintenanceRecord> serviceHistory
    )
    {
        // If we have at least one record
        if (serviceHistory.Count <= 0)
            return null;
        var latestService = serviceHistory[0]; // Use indexing instead of First()

        // Use switch expression instead of switch statement
        return serviceType.ToLower() switch
        {
            "oil change" => latestService.Date.AddMonths(3),
            "tire rotation" => latestService.Date.AddMonths(6),
            "brake inspection" => latestService.Date.AddMonths(12),
            "brake service" => latestService.Date.AddMonths(12),
            "air filter" => latestService.Date.AddMonths(12),
            _ => latestService.Date.AddMonths(6) // Default: 6 months
        };

        // No service history
    }

    public static decimal EstimateServiceCost(
        string serviceType,
        List<MaintenanceRecord> serviceHistory
    )
    {
        // If we have cost history, use the average
        if (serviceHistory.Count != 0)
        {
            return serviceHistory.Average(s => s.Cost);
        }

        // Otherwise, estimate based on service type
        return serviceType.ToLower() switch
        {
            "oil change" => 45.99m,
            "tire rotation" => 25.00m,
            "brake inspection" or "brake service" => 150.00m,
            "air filter" => 20.00m,
            "annual inspection" or "inspection" => 89.99m,
            _ => 50.00m // Generic estimate
        };
    }

    public static void AddMissingMaintenanceTypes(
        List<UpcomingMaintenanceDto> alerts,
        List<string> existingTypes,
        Vehicle vehicle
    )
    {
        var currentDate = DateTimeOffset.UtcNow;
        int ageInMonths = (currentDate.Year - vehicle.Year) * 12 + currentDate.Month - 1;

        CheckForOilChange(alerts, existingTypes, vehicle, currentDate);
        CheckForTireRotation(alerts, existingTypes, vehicle, currentDate);
        CheckForAnnualInspection(alerts, existingTypes, vehicle, currentDate, ageInMonths);
    }

    // Breaking down AddMissingMaintenanceTypes to reduce complexity
    private static void CheckForOilChange(
        List<UpcomingMaintenanceDto> alerts,
        List<string> existingTypes,
        Vehicle vehicle,
        DateTimeOffset currentDate
    )
    {
        // Check for oil change
        if (existingTypes.Any(t => t.Contains("oil", StringComparison.CurrentCultureIgnoreCase)))
            return;
        // Recommend based on mileage
        var mileageSinceLast = vehicle.CurrentMileage % 3000;
        if (mileageSinceLast is > 2500 or < 500)
        {
            alerts.Add(new UpcomingMaintenanceDto("Oil Change", currentDate.AddDays(15), 45.99m));
        }
    }

    private static void CheckForTireRotation(
        List<UpcomingMaintenanceDto> alerts,
        List<string> existingTypes,
        Vehicle vehicle,
        DateTimeOffset currentDate
    )
    {
        // Check for tire rotation
        if (
            existingTypes.Any(
                t =>
                    t.Contains("tire", StringComparison.CurrentCultureIgnoreCase)
                    && t.Contains("rotat", StringComparison.CurrentCultureIgnoreCase)
            )
        )
            return;
        var mileageSinceLast = vehicle.CurrentMileage % 6000;
        if (mileageSinceLast is > 5500 or < 500)
        {
            alerts.Add(
                new UpcomingMaintenanceDto("Tire Rotation", currentDate.AddDays(30), 25.00m)
            );
        }
    }

    private static void CheckForAnnualInspection(
        List<UpcomingMaintenanceDto> alerts,
        List<string> existingTypes,
        Vehicle vehicle,
        DateTimeOffset currentDate,
        int ageInMonths
    )
    {
        // Check for annual inspection
        bool hasInspection = existingTypes.Any(
            t =>
                t.Contains("inspection", StringComparison.CurrentCultureIgnoreCase)
                || t.Contains("annual", StringComparison.CurrentCultureIgnoreCase)
        );

        if (!hasInspection && ageInMonths % 12 >= 11)
        {
            alerts.Add(
                new UpcomingMaintenanceDto("Annual Inspection", currentDate.AddDays(30), 89.99m)
            );
        }
    }

    public static List<FuelEfficiencyTrendDto> CalculateFuelEfficiencyTrend(
        List<FuelRecord> fuelRecords
    )
    {
        if (fuelRecords.Count < 2)
        {
            return new List<FuelEfficiencyTrendDto>();
        }

        // Group by month
        var monthlyRecords = fuelRecords
            .GroupBy(f => new DateTime(f.Date.Year, f.Date.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .ToList();

        return (
            from monthGroup in monthlyRecords
            where monthGroup.Count() >= 2
            let mpg = CalculateMonthlyMpg(monthGroup.ToList())
            where mpg > 0
            select new FuelEfficiencyTrendDto(new DateTimeOffset(monthGroup.Key), mpg)
        ).ToList();
    }

    private static double CalculateMonthlyMpg(List<FuelRecord> recordsInMonth)
    {
        // Sort by mileage within the month
        var sortedRecords = recordsInMonth.OrderBy(f => f.Mileage).ToList();

        // Calculate mpg for this month
        double totalDistance = 0;
        double totalGallons = 0;

        for (int i = 1; i < sortedRecords.Count; i++)
        {
            var distance = sortedRecords[i].Mileage - sortedRecords[i - 1].Mileage;
            if (distance is <= 0 or >= 1000)
                continue; // Sanity check
            totalDistance += distance;
            totalGallons += sortedRecords[i].Gallons;
        }

        return totalGallons > 0 ? Math.Round(totalDistance / totalGallons, 1) : 0;
    }

    private static List<CostByMonthDto> CalculateCostByMonth(
        List<MaintenanceRecord> maintenanceRecords,
        List<FuelRecord> fuelRecords
    )
    {
        // Combine all costs and group by month
        var maintenanceCostsByMonth = maintenanceRecords
            .GroupBy(m => new { m.Date.Year, m.Date.Month })
            .Select(
                g => new { YearMonth = $"{g.Key.Year}-{g.Key.Month:D2}", Cost = g.Sum(m => m.Cost) }
            );

        var fuelCostsByMonth = fuelRecords
            .GroupBy(f => new { f.Date.Year, f.Date.Month })
            .Select(
                g =>
                    new
                    {
                        YearMonth = $"{g.Key.Year}-{g.Key.Month:D2}",
                        Cost = g.Sum(f => f.TotalCost)
                    }
            );

        // Combine and sum
        var allCosts = maintenanceCostsByMonth
            .Concat(fuelCostsByMonth)
            .GroupBy(c => c.YearMonth)
            .Select(g => new CostByMonthDto(g.Key, g.Sum(c => c.Cost)))
            .OrderBy(c => c.Month)
            .ToList();

        return allCosts;
    }
}
