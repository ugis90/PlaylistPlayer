using System.Text.Json;
using PlaylistPlayer;
using PlaylistPlayer.Data;
using PlaylistPlayer.Data.Entities;
using PlaylistPlayer.Helpers;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Results;
using SharpGrip.FluentValidation.AutoValidation.Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddDbContext<MusicDbContext>();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddFluentValidationAutoValidation(configuration =>
{
    configuration.OverrideDefaultResultFactoryWith<ProblemDetailsResultFactory>();
});
builder.Services.AddResponseCaching();

var app = builder.Build();

app.AddCategoryApi();
app.AddPlaylistApi();
app.AddSongApi();

app.MapGet(
        "api",
        (HttpContext httpContext, LinkGenerator linkGenerator) =>
            Results.Ok(
                new List<LinkDto>
                {
                    new(
                        linkGenerator.GetUriByName(httpContext, "GetCategories"),
                        "categories",
                        "GET"
                    ),
                    new(
                        linkGenerator.GetUriByName(httpContext, "CreateCategory"),
                        "createCategory",
                        "POST"
                    ),
                    new(linkGenerator.GetUriByName(httpContext, "GetRoot"), "self", "GET"),
                }
            )
    )
    .WithName("GetRoot");

app.MapControllers();
app.UseResponseCaching();
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

// DTOs
public record UpdateSongDto(string Title, string Artist, int Duration);

public record CreateSongDto(string Title, string Artist, int Duration);

public record UpdatePlaylistDto(string Name, string Description);

public record CreatePlaylistDto(string Name, string Description);

public record CategoryDto(int Id, string Name, string Description, DateTimeOffset CreatedOn);

public record PlaylistDto(
    int Id,
    string Name,
    string Description,
    DateTimeOffset CreatedOn,
    int CategoryId
);

public record SongDto(
    int Id,
    string Title,
    string Artist,
    int Duration,
    DateTimeOffset CreatedOn,
    int PlaylistId
);

// DTO Validators
public record CreateCategoryDto(string Name, string Description)
{
    public class CreateCategoryDtoValidator : AbstractValidator<CreateCategoryDto>
    {
        public CreateCategoryDtoValidator()
        {
            RuleFor(x => x.Name).NotEmpty().Length(min: 2, max: 100);
            RuleFor(x => x.Description).NotEmpty().Length(min: 5, max: 300);
        }
    }
};

public record UpdateCategoryDto(string Description)
{
    public class UpdateCategoryDtoValidator : AbstractValidator<UpdateCategoryDto>
    {
        public UpdateCategoryDtoValidator()
        {
            RuleFor(x => x.Description).NotEmpty().Length(min: 5, max: 300);
        }
    }
};

public class CreatePlaylistDtoValidator : AbstractValidator<CreatePlaylistDto>
{
    public CreatePlaylistDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).NotEmpty().Length(5, 500);
    }
}

public class UpdatePlaylistDtoValidator : AbstractValidator<UpdatePlaylistDto>
{
    public UpdatePlaylistDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().Length(2, 100);
        RuleFor(x => x.Description).NotEmpty().Length(5, 500);
    }
}

public class CreateSongDtoValidator : AbstractValidator<CreateSongDto>
{
    public CreateSongDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(1, 200);
        RuleFor(x => x.Artist).NotEmpty().Length(1, 100);
        RuleFor(x => x.Duration).GreaterThan(0);
    }
}

public class UpdateSongDtoValidator : AbstractValidator<UpdateSongDto>
{
    public UpdateSongDtoValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(1, 200);
        RuleFor(x => x.Artist).NotEmpty().Length(1, 100);
        RuleFor(x => x.Duration).GreaterThan(0);
    }
}