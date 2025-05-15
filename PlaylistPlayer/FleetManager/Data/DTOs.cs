using FluentValidation;

namespace FleetManager.Data;

// Vehicle DTOs
public record VehicleDto(
    int Id,
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Description,
    int CurrentMileage,
    DateTimeOffset CreatedOn
);

public record CreateVehicleDto(
    string Make,
    string Model,
    int Year,
    string LicensePlate,
    string Description,
    int? CurrentMileage
);

public record UpdateVehicleDto(
    string? Make,
    string? Model,
    int? Year,
    string? LicensePlate,
    string? Description,
    int? CurrentMileage
);

public record LocationUpdateDto(
    double Latitude,
    double Longitude,
    double? Speed,
    double? Heading,
    string? Timestamp
);

// Trip DTOs
public record TripDto(
    int Id,
    string StartLocation,
    string EndLocation,
    double Distance,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Purpose,
    double? FuelUsed,
    DateTimeOffset CreatedAt,
    int VehicleId,
    string DriverId
);

public record CreateTripDto(
    string StartLocation,
    string EndLocation,
    double Distance,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string? Purpose,
    double? FuelUsed
);

public record UpdateTripDto(double? Distance, string? Purpose, double? FuelUsed);

// MaintenanceRecord DTOs
public record MaintenanceRecordDto(
    int Id,
    string ServiceType,
    string Description,
    decimal Cost,
    int Mileage,
    DateTimeOffset Date,
    string Provider,
    DateTimeOffset? NextServiceDue,
    DateTimeOffset CreatedAt,
    int VehicleId
);

public record CreateMaintenanceRecordDto(
    string ServiceType,
    string Description,
    decimal Cost,
    int Mileage,
    DateTimeOffset Date,
    string? Provider,
    DateTimeOffset? NextServiceDue
);

public record UpdateMaintenanceRecordDto(
    string? ServiceType,
    string? Description,
    decimal? Cost,
    int? Mileage,
    DateTimeOffset? Date,
    string? Provider,
    DateTimeOffset? NextServiceDue
);

// FuelRecord DTOs
public record FuelRecordDto(
    int Id,
    DateTimeOffset Date,
    double Liters,
    decimal CostPerLiter,
    decimal TotalCost,
    int Mileage,
    string Station,
    bool FullTank,
    DateTimeOffset CreatedOn,
    int VehicleId
);

public record CreateFuelRecordDto(
    DateTimeOffset Date,
    double Liters,
    decimal CostPerLiter,
    decimal TotalCost,
    int Mileage,
    string? Station,
    bool FullTank
);

public record UpdateFuelRecordDto(
    double? Liters,
    decimal? TotalCost,
    string? Station,
    bool? FullTank
);

// Analytics DTOs
public record VehicleAnalyticsDto(
    decimal TotalCost,
    int Mileage,
    decimal CostPerKm,
    int TotalTrips,
    double FuelEfficiencyLitersPer100Km,
    decimal MaintenanceCosts,
    decimal FuelCosts,
    IEnumerable<UpcomingMaintenanceDto> UpcomingMaintenance,
    IEnumerable<FuelEfficiencyTrendDto> FuelEfficiencyTrend,
    CostByCategoryDto CostByCategory,
    IEnumerable<CostByMonthDto> CostByMonth
);

public record UpcomingMaintenanceDto(string Type, DateTimeOffset DueDate, decimal EstimatedCost);

public record FuelEfficiencyTrendDto(DateTimeOffset Date, double LitersPer100Km);

public record CostByCategoryDto(decimal Fuel, decimal Maintenance, decimal Repairs);

public record CostByMonthDto(string Month, decimal Cost);

public record FleetAnalyticsDto(
    int TotalVehicles,
    int TotalMileage,
    decimal TotalCost,
    Dictionary<string, decimal> CostBreakdown,
    decimal AverageCostPerKm,
    double AverageFuelEfficiencyLitersPer100Km,
    MostUsedVehicleDto MostUsedVehicle,
    MostEfficientVehicleDto MostEfficientVehicle,
    IEnumerable<CostTrendDto> CostTrend,
    IEnumerable<FleetUpcomingMaintenanceDto> UpcomingMaintenance
);

public record MostUsedVehicleDto(int Id, string Make, string Model, int Trips);

public record MostEfficientVehicleDto(int Id, string Make, string Model, double LitersPer100Km);

public record CostTrendDto(string Month, decimal Cost);

public record FleetUpcomingMaintenanceDto(int VehicleId, string Type, DateTimeOffset DueDate);

// Vehicle Validators
public class CreateVehicleDtoValidator : AbstractValidator<CreateVehicleDto>
{
    public CreateVehicleDtoValidator()
    {
        RuleFor(x => x.Make).NotEmpty().Length(2, 50);
        RuleFor(x => x.Model).NotEmpty().Length(2, 50);
        RuleFor(x => x.Year).InclusiveBetween(1900, 2100);
        RuleFor(x => x.LicensePlate).NotEmpty().Length(1, 20);
        RuleFor(x => x.Description).NotEmpty().Length(5, 300);
        RuleFor(x => x.CurrentMileage).GreaterThanOrEqualTo(0).When(x => x.CurrentMileage.HasValue);
    }
}

public class UpdateVehicleDtoValidator : AbstractValidator<UpdateVehicleDto>
{
    public UpdateVehicleDtoValidator()
    {
        RuleFor(x => x.Description)
            .Length(5, 300)
            .When(x => !string.IsNullOrWhiteSpace(x.Description));
        RuleFor(x => x.CurrentMileage).GreaterThanOrEqualTo(0).When(x => x.CurrentMileage.HasValue);
        RuleFor(x => x.Make).NotEmpty().Length(2, 50).When(x => x.Make != null);
        RuleFor(x => x.Model).NotEmpty().Length(2, 50).When(x => x.Model != null);
        RuleFor(x => x.Year).InclusiveBetween(1900, 2100).When(x => x.Year.HasValue);
        RuleFor(x => x.LicensePlate).NotEmpty().Length(1, 20).When(x => x.LicensePlate != null);
    }
}

// Trip Validators
public class CreateTripDtoValidator : AbstractValidator<CreateTripDto>
{
    public CreateTripDtoValidator()
    {
        RuleFor(x => x.StartLocation).NotEmpty().Length(2, 100);
        RuleFor(x => x.EndLocation).NotEmpty().Length(2, 100);
        RuleFor(x => x.Distance)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Distance must be 0 or greater."); // Allow 0
        RuleFor(x => x.StartTime).NotEmpty();
        RuleFor(x => x.EndTime).NotEmpty().GreaterThan(x => x.StartTime);
        RuleFor(x => x.Purpose).MaximumLength(200);
        RuleFor(x => x.FuelUsed).GreaterThanOrEqualTo(0).When(x => x.FuelUsed.HasValue);
    }
}

public class UpdateTripDtoValidator : AbstractValidator<UpdateTripDto>
{
    public UpdateTripDtoValidator()
    {
        RuleFor(x => x.Distance).GreaterThanOrEqualTo(0).When(x => x.Distance.HasValue);
        RuleFor(x => x.Purpose).MaximumLength(200).When(x => x.Purpose != null);
        RuleFor(x => x.FuelUsed).GreaterThanOrEqualTo(0).When(x => x.FuelUsed.HasValue);
    }
}

// MaintenanceRecord Validators
public class CreateMaintenanceRecordDtoValidator : AbstractValidator<CreateMaintenanceRecordDto>
{
    public CreateMaintenanceRecordDtoValidator()
    {
        RuleFor(x => x.ServiceType).NotEmpty().Length(1, 100);
        RuleFor(x => x.Description).NotEmpty().Length(1, 300);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Mileage).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Provider).MaximumLength(100); // Allow null/empty
        RuleFor(x => x.NextServiceDue)
            .GreaterThan(x => x.Date)
            .When(x => x.NextServiceDue.HasValue);
    }
}

public class UpdateMaintenanceRecordDtoValidator : AbstractValidator<UpdateMaintenanceRecordDto>
{
    public UpdateMaintenanceRecordDtoValidator()
    {
        RuleFor(x => x.ServiceType).NotEmpty().Length(1, 100).When(x => x.ServiceType != null);
        RuleFor(x => x.Description).NotEmpty().Length(1, 300).When(x => x.Description != null);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost.HasValue);
        RuleFor(x => x.Mileage).GreaterThanOrEqualTo(0).When(x => x.Mileage.HasValue);
        RuleFor(x => x.Date).NotEmpty().When(x => x.Date.HasValue);
        RuleFor(x => x.Provider).MaximumLength(100).When(x => x.Provider != null);
        RuleFor(x => x.NextServiceDue)
            .GreaterThan(x => x.Date)
            .When(x => x.NextServiceDue.HasValue && x.Date.HasValue);
    }
}

// FuelRecord Validators
public class CreateFuelRecordDtoValidator : AbstractValidator<CreateFuelRecordDto>
{
    public CreateFuelRecordDtoValidator()
    {
        RuleFor(x => x.Date).NotEmpty();
        RuleFor(x => x.Liters).GreaterThan(0); // CHANGED
        RuleFor(x => x.CostPerLiter).GreaterThan(0); // CHANGED
        RuleFor(x => x.TotalCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Mileage).GreaterThanOrEqualTo(0); // Assumed KM
        RuleFor(x => x.Station).Length(0, 100);
    }
}

public class UpdateFuelRecordDtoValidator : AbstractValidator<UpdateFuelRecordDto>
{
    public UpdateFuelRecordDtoValidator()
    {
        RuleFor(x => x.Liters).GreaterThan(0).When(x => x.Liters.HasValue); // CHANGED
        RuleFor(x => x.TotalCost).GreaterThanOrEqualTo(0).When(x => x.TotalCost.HasValue);
        RuleFor(x => x.Station).Length(0, 100).When(x => x.Station != null);
    }
}
