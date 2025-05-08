using System.Text;
using System.Text.Json;
using FleetManager;
using FleetManager.Data;
using FleetManager.Data.Entities;
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
            .SetIsOriginAllowed(origin => true) // For development only
            .AllowCredentials(); // Keep if you rely on cookies (like RefreshToken)
    });
});

builder.Services.AddControllers();
builder.Services.AddDbContext<FleetDbContext>();
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

app.UseCors();
using var scope = app.Services.CreateScope();

var dbContext = scope.ServiceProvider.GetRequiredService<FleetDbContext>();
dbContext.Database.Migrate();

var dbSeeder = scope.ServiceProvider.GetRequiredService<AuthSeeder>();
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
app.UseAuthentication();
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
