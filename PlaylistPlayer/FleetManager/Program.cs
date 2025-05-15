using System.Text;
using FleetManager;
using FleetManager.Data;
using FleetManager.Helpers;
using FleetManager.Services;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FleetManager.Auth;
using FleetManager.Auth.Model;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Results;
using SharpGrip.FluentValidation.AutoValidation.Shared.Extensions;
using FleetManager.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .WithOrigins(
                "http://localhost:3000",
                "https://localhost:3000",
                "https://chic-jalebi-b61d0b.netlify.app"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("Pagination", "Content-Disposition")
            .SetIsOriginAllowed(origin => true)
            .AllowCredentials();
    });
});

builder.Services.AddControllers();
builder.Services.AddDbContext<FleetDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgreSQL");
    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException(
            "PostgreSQL connection string is not configured in Program.cs."
        );
    }
    options.UseNpgsql(connectionString);
});
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation(
    configuration => configuration.OverrideDefaultResultFactoryWith<ProblemDetailsResultFactory>()
);
builder.Services.AddResponseCaching();
builder.Services.AddTransient<JwtTokenService>();
builder.Services.AddTransient<SessionService>();
builder.Services.AddTransient<AnalyticsService>();
builder.Services.AddScoped<AuthSeeder>();

builder.Services
    .AddIdentity<FleetUser, IdentityRole>()
    .AddEntityFrameworkStores<FleetDbContext>()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidAudience = builder.Configuration[
            "Jwt:ValidAudience"
        ];
        options.TokenValidationParameters.ValidIssuer = builder.Configuration["Jwt:ValidIssuer"];
        options.TokenValidationParameters.IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"])
        );
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsEnvironment("Testing"))
{
    app.UseTestHeaderAuthentication();
    Console.WriteLine(
        "INFO: TestHeaderAuthenticationMiddleware registered for Testing environment."
    );
    // In "Testing" environment, we rely SOLELY on TestHeaderAuthentication.
    // The standard app.UseAuthentication() might interfere or expect a JWT.
    // We still need app.UseAuthorization() for [Authorize] attributes to work.
}
else
{
    app.UseAuthentication(); // Standard JWT authentication for Dev/Prod
}

if (
    !app.Environment.IsEnvironment("Testing")
    || app.Services.GetRequiredService<FleetDbContext>().Database.IsRelational()
)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
}

app.UseCors();
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
    if (dbContext.Database.IsRelational())
    {
        dbContext.Database.Migrate();
    }
}

var dbSeeder = app.Services.CreateScope().ServiceProvider.GetRequiredService<AuthSeeder>();
await dbSeeder.SeedAsync();

app.AddVehicleApi();
app.AddTripApi();
app.AddMaintenanceRecordApi();
app.AddFuelRecordApi();
app.AddAnalyticsApi();
app.AddAuthApi();

app.MapGet(
        "api",
        (HttpContext httpContext, LinkGenerator linkGenerator) =>
            Results.Ok(
                new List<LinkDto>
                {
                    new(linkGenerator.GetUriByName(httpContext, "GetVehicles"), "vehicles", "GET"),
                    new(
                        linkGenerator.GetUriByName(httpContext, "CreateVehicle"),
                        "createVehicle",
                        "POST"
                    ),
                    new(
                        linkGenerator.GetUriByName(httpContext, "GetFleetAnalytics"),
                        "fleetAnalytics",
                        "GET"
                    ),
                    new(linkGenerator.GetUriByName(httpContext, "GetRoot"), "self", "GET"),
                }
            )
    )
    .WithName("GetRoot");

app.MapControllers();
app.UseResponseCaching();

//app.UseAuthentication();
app.UseAuthorization();
app.Run();

public class ProblemDetailsResultFactory : IFluentValidationAutoValidationResultFactory
{
    public IResult CreateResult(
        EndpointFilterInvocationContext context,
        ValidationResult validationResult
    )
    {
        var problemDetails = new HttpValidationProblemDetails(
            validationResult.ToValidationProblemErrors()
        )
        {
            Type = "https://tools.ietf.org/html/rfc4918#section-11.2",
            Title = "Unprocessable Entity",
            Status = 422
        };

        return TypedResults.Problem(problemDetails);
    }
}

public partial class Program;
