using FleetManager.Data;
using FleetManager.Data.Entities;
using Microsoft.EntityFrameworkCore;

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

        var vehicle = await dbContext.Vehicles
            .Include(v => v.Trips)
            .Include(v => v.MaintenanceRecords)
            .Include(v => v.FuelRecords)
            .FirstOrDefaultAsync(v => v.Id == vehicleId);

        if (vehicle == null)
        {
            throw new KeyNotFoundException($"Vehicle with ID {vehicleId} not found");
        }

        var tripsInPeriod = vehicle.Trips
            .Where(t => t.StartTime >= start && t.EndTime <= end)
            .ToList();

        var maintenanceRecordsInPeriod = vehicle.MaintenanceRecords
            .Where(m => m.Date >= start && m.Date <= end)
            .ToList();

        var fuelRecordsInPeriod = vehicle.FuelRecords
            .Where(f => f.Date >= start && f.Date <= end)
            .ToList();

        var maintenanceCosts = maintenanceRecordsInPeriod.Sum(m => m.Cost);
        var fuelCosts = fuelRecordsInPeriod.Sum(f => f.TotalCost);
        var totalCost = maintenanceCosts + fuelCosts;

        var mileage = CalculateMileageForPeriod(tripsInPeriod, fuelRecordsInPeriod);

        decimal costPerKm = mileage > 0 ? totalCost / (decimal)mileage : 0; // Cast mileage to decimal

        double fuelEfficiency = CalculateAverageFuelEfficiency(fuelRecordsInPeriod);

        var upcomingMaintenance = PredictUpcomingMaintenance(
            vehicle,
            vehicle.MaintenanceRecords.ToList()
        );

        var fuelEfficiencyTrend = CalculateFuelEfficiencyTrend(fuelRecordsInPeriod);

        var costByCategory = new CostByCategoryDto(fuelCosts, maintenanceCosts, 0);

        var costByMonth = CalculateCostByMonth(maintenanceRecordsInPeriod, fuelRecordsInPeriod);

        return new VehicleAnalyticsDto(
            totalCost,
            mileage,
            costPerKm,
            tripsInPeriod.Count,
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

        var vehicles = await dbContext.Vehicles
            .Where(v => v.UserId == userId)
            .Include(v => v.Trips)
            .Include(v => v.MaintenanceRecords)
            .Include(v => v.FuelRecords)
            .ToListAsync();

        if (vehicles.Count == 0)
        {
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
        }

        var (totalMileage, totalCost, costBreakdown, averageFuelEfficiency) =
            ProcessVehicleStatistics(vehicles, start, end);

        int totalVehicles = vehicles.Count;
        decimal averageCostPerMile = totalMileage > 0 ? totalCost / (decimal)totalMileage : 0;

        var mostUsedVehicle = FindMostUsedVehicle(vehicles, start, end);
        var mostEfficientVehicle = FindMostEfficientVehicle(vehicles, start, end);

        var costTrend = ConvertToTrendData(CalculateFleetCostTrend(vehicles, start, end));

        var upcomingMaintenance = GetFleetUpcomingMaintenance(vehicles);

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

    public static (
        int totalMileage,
        decimal totalCost,
        Dictionary<string, decimal> costBreakdown,
        double averageFuelEfficiency
    ) ProcessVehicleStatistics(List<Vehicle> vehicles, DateTimeOffset start, DateTimeOffset end)
    {
        int totalMileage = 0;
        decimal totalCost = 0;
        var costBreakdown = new Dictionary<string, decimal>();
        double totalFuelEfficiencySum = 0;
        int vehiclesWithFuelData = 0;

        foreach (var vehicle in vehicles)
        {
            var tripsInPeriod = vehicle.Trips
                .Where(t => t.StartTime >= start && t.EndTime <= end)
                .ToList();
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

            if (fuelRecordsInPeriod.Count < 2) continue;
            var vehicleEfficiency = CalculateAverageFuelEfficiency(fuelRecordsInPeriod);
            if (vehicleEfficiency <= 0) continue;
            totalFuelEfficiencySum += vehicleEfficiency;
            vehiclesWithFuelData++;
        }

        double averageFuelEfficiency =
            vehiclesWithFuelData > 0 ? totalFuelEfficiencySum / vehiclesWithFuelData : 0;

        return (totalMileage, totalCost, costBreakdown, averageFuelEfficiency);
    }

    public static MostUsedVehicleDto FindMostUsedVehicle(
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
            )
            .Where(x => x.TripCount > 0)
            .OrderByDescending(x => x.TripCount)
            .FirstOrDefault();

        if (mostUsed == null)
        {
            return new MostUsedVehicleDto(0, UnknownVehicleLabel, UnknownVehicleLabel, 0);
        }
        return new MostUsedVehicleDto(
            mostUsed.Vehicle.Id,
            mostUsed.Vehicle.Make,
            mostUsed.Vehicle.Model,
            mostUsed.TripCount
        );
    }

    public static MostEfficientVehicleDto FindMostEfficientVehicle(
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
            .Where(x => x.FuelRecordsInPeriod.Count >= 2)
            .Select(
                x =>
                    new
                    {
                        x.Vehicle,
                        Efficiency = CalculateAverageFuelEfficiency(x.FuelRecordsInPeriod)
                    }
            )
            .Where(x => x.Efficiency > 0)
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
        var allMaintenanceRecords = vehicles
            .SelectMany(v => v.MaintenanceRecords)
            .Where(m => m.Date >= start && m.Date <= end)
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

    public static int CalculateMileageForPeriod(List<Trip> trips, List<FuelRecord> fuelRecords)
    {
        if (trips.Count != 0)
            return (int)trips.Sum(t => t.Distance);
        if (fuelRecords.Count < 2)
            return 0;
        var minMileage = fuelRecords.Min(f => f.Mileage);
        var maxMileage = fuelRecords.Max(f => f.Mileage);
        return maxMileage - minMileage;
    }

    public static double CalculateAverageFuelEfficiency(List<FuelRecord> fuelRecords)
    {
        if (fuelRecords.Count < 2)
            return 0;

        var sortedRecordsByMileage = fuelRecords.OrderBy(f => f.Mileage).ToList();
        var fullTankFillups = sortedRecordsByMileage.Where(f => f.FullTank).ToList();

        if (fullTankFillups.Count < 2)
        {
            fullTankFillups = sortedRecordsByMileage;
            if (fullTankFillups.Count < 2)
                return 0;
        }

        double totalDistanceKm = 0;
        double totalLiters = 0;

        for (int i = 1; i < fullTankFillups.Count; i++)
        {
            var current = fullTankFillups[i];
            var previous = fullTankFillups[i - 1];
            var distance = current.Mileage - previous.Mileage;

            if (distance is <= 0 or >= 2000)
                continue;
            totalDistanceKm += distance;
            totalLiters += current.Liters;
        }

        if (totalLiters > 0 && totalDistanceKm > 0)
        {
            return Math.Round((totalLiters / totalDistanceKm) * 100, 1);
        }
        return 0;
    }

    public static List<UpcomingMaintenanceDto> PredictUpcomingMaintenance(
        Vehicle vehicle,
        List<MaintenanceRecord> maintenanceHistory
    )
    {
        var currentDate = DateTimeOffset.UtcNow;
        var alerts = new List<UpcomingMaintenanceDto>();

        if (maintenanceHistory.Count == 0)
        {
            return GetDefaultMaintenanceSchedule(vehicle);
        }

        var serviceGroups = maintenanceHistory
            .GroupBy(m => m.ServiceType)
            .ToDictionary(g => g.Key, g => g.ToList());

        ProcessExistingMaintenanceRecords(serviceGroups, vehicle, currentDate, alerts);

        AddMissingMaintenanceTypes(alerts, serviceGroups.Keys.ToList(), vehicle);

        return alerts.OrderBy(a => a.DueDate).ToList();
    }

    private static void ProcessExistingMaintenanceRecords(
        Dictionary<string, List<MaintenanceRecord>> serviceGroups,
        Vehicle vehicle,
        DateTimeOffset currentDate,
        List<UpcomingMaintenanceDto> alerts
    )
    {
        foreach (var (serviceType, value) in serviceGroups)
        {
            var records = value.OrderByDescending(r => r.Date).ToList();

            if (records.Count == 0)
                continue;

            var latestRecord = records[0];

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
        
        int ageInServiceYears = currentDate.Year - vehicle.Year;
        int ageInMonthsRough = (currentDate.Year - vehicle.Year) * 12 + currentDate.Month - 1;


        // Oil Change (Tepalų keitimas): every 12 months or 15,000 km
        if (ageInMonthsRough % 12 == 0 || vehicle.CurrentMileage % 15000 <= 1000)
        {
            alerts.Add(new UpcomingMaintenanceDto("Oil Change", currentDate.AddDays(30), 70.00m));
        }

        // Tire Rotation/Check (Padangų rotacija/patikra): every 6-12 months or 10,000 km
        if (ageInMonthsRough % 6 == 0 || vehicle.CurrentMileage % 10000 <= 800)
        {
            alerts.Add(
                new UpcomingMaintenanceDto("Tire Rotation", currentDate.AddDays(45), 30.00m)
            );
        }

        // Annual Service / Check-up (Metinis Aptarnavimas): every 12 months
        if (ageInMonthsRough % 12 == 0 && ageInServiceYears >= 1)
        {
            alerts.Add(
                new UpcomingMaintenanceDto("Annual Service", currentDate.AddDays(60), 120.00m)
            );
        }

        // Mandatory Technical Inspection (Techninė Apžiūra - TA)
        if (ageInServiceYears >= 3 && ageInMonthsRough % 24 == 0) {
             alerts.Add(
                new UpcomingMaintenanceDto("Mandatory Technical Inspection", currentDate.AddDays(90), 50.00m)
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

        var intervals = new List<TimeSpan>();
        var mileageIntervals = new List<int>();

        for (int i = 0; i < serviceHistory.Count - 1; i++)
        {
            var current = serviceHistory[i];
            var next = serviceHistory[i + 1];

            intervals.Add(current.Date - next.Date);
            mileageIntervals.Add(current.Mileage - next.Mileage);
        }

        var avgTimeInterval = new TimeSpan((long)intervals.Average(i => i.Ticks));
        var avgMileageInterval = (int)mileageIntervals.Average();

        var latestService = serviceHistory[0];

        var predictedTimeDate = latestService.Date + avgTimeInterval;

        var mileageDiff = currentMileage - latestService.Mileage;
        var remainingMileage = avgMileageInterval - mileageDiff;

        var monthsToMileageThreshold = Math.Max(0, (double)remainingMileage / 1600);
        var predictedMileageDate = DateTimeOffset.UtcNow.AddMonths((int)monthsToMileageThreshold);

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
        var latestService = serviceHistory[0];

        return serviceType.ToLower() switch
        {
            "oil change" => latestService.Date.AddMonths(3),
            "tire rotation" => latestService.Date.AddMonths(6),
            "brake inspection" => latestService.Date.AddMonths(12),
            "brake service" => latestService.Date.AddMonths(12),
            "air filter" => latestService.Date.AddMonths(12),
            _ => latestService.Date.AddMonths(6)
        };
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
            _ => 50.00m
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

    private static void CheckForOilChange(
        List<UpcomingMaintenanceDto> alerts,
        List<string> existingTypes,
        Vehicle vehicle,
        DateTimeOffset currentDate
    )
    {
        if (existingTypes.Any(t => t.Contains("oil", StringComparison.CurrentCultureIgnoreCase)))
            return;
        var mileageSinceLast = vehicle.CurrentMileage % 5000;
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
        if (
            existingTypes.Any(
                t =>
                    t.Contains("tire", StringComparison.CurrentCultureIgnoreCase)
                    && t.Contains("rotat", StringComparison.CurrentCultureIgnoreCase)
            )
        )
            return;
        var mileageSinceLast = vehicle.CurrentMileage % 10000;
        if (mileageSinceLast is > 8800 or < 850)
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

    public static List<FuelEfficiencyTrendDto> CalculateFuelEfficiencyTrend(List<FuelRecord> fuelRecords)
    {
        if (fuelRecords == null || fuelRecords.Count < 2)
            return [];

        var trendData = fuelRecords
            .GroupBy(fr => new { fr.VehicleId, fr.Date.Year, fr.Date.Month })
            .Select(group => new
            {
                group.Key.VehicleId,
                Year = group.Key.Year,
                Month = group.Key.Month,
                Records = group.ToList()
            })
            .Select(vehicleMonthData => new
            {
                vehicleMonthData.Year,
                vehicleMonthData.Month,
                LitersPer100Km = CalculateMonthlyLitersPer100Km(vehicleMonthData.Records)
            })
            .Where(monthlyResult => monthlyResult.LitersPer100Km > 0)
            .GroupBy(r => new { r.Year, r.Month })
            .Select(finalGroup => new FuelEfficiencyTrendDto(
                new DateTimeOffset(finalGroup.Key.Year, finalGroup.Key.Month, 1, 0, 0, 0, TimeSpan.Zero),
                Math.Round(finalGroup.Average(g => g.LitersPer100Km), 1)
            ))
            .OrderBy(dto => dto.Date)
            .ToList();

        return trendData;
    }

    public static double CalculateMonthlyLitersPer100Km(List<FuelRecord> recordsInMonth)
    {
        if (recordsInMonth == null || recordsInMonth.Count < 2) return 0;

        var sortedRecords = recordsInMonth.OrderBy(f => f.Mileage).ToList();
        double totalDistanceKmInMonth = 0;
        double totalLitersInMonth = 0;

        for (int i = 1; i < sortedRecords.Count; i++)
        {
            var currentFill = sortedRecords[i];
            var previousFill = sortedRecords[i - 1];

            if (!currentFill.FullTank || !previousFill.FullTank) continue;
            var distanceSegment = currentFill.Mileage - previousFill.Mileage;
            var litersSegment = currentFill.Liters;

            if (distanceSegment <= 0 || distanceSegment >= 2000 || litersSegment <= 0) continue;
            totalDistanceKmInMonth += distanceSegment;
            totalLitersInMonth += litersSegment;
        }

        if (totalLitersInMonth > 0 && totalDistanceKmInMonth > 0)
        {
            return Math.Round((totalLitersInMonth / totalDistanceKmInMonth) * 100, 1);
        }
        return 0;
    }

    public static List<CostByMonthDto> CalculateCostByMonth(
        List<MaintenanceRecord> maintenanceRecords,
        List<FuelRecord> fuelRecords
    )
    {
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

        var allCosts = maintenanceCostsByMonth
            .Concat(fuelCostsByMonth)
            .GroupBy(c => c.YearMonth)
            .Select(g => new CostByMonthDto(g.Key, g.Sum(c => c.Cost)))
            .OrderBy(c => c.Month)
            .ToList();

        return allCosts;
    }
}
