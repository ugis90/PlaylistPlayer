// FleetManager/Auth/AuthEndpoints.cs
using System.Security.Claims;
using FleetManager.Auth.Model;
using FleetManager.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using FluentValidation;
using SharpGrip.FluentValidation.AutoValidation.Endpoints.Extensions;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System;
using System.Collections.Generic; // For List

namespace FleetManager.Auth;

public static class AuthEndpoints
{
    public record RegisterUserDto(string UserName, string Email, string Password, string Role);

    public record LoginDto(string UserName, string Password);

    public record UserInfo(string username, string email, string role);

    public record SuccessfulLoginDto(string accessToken, UserInfo userInfo);

    public class RegisterUserDtoValidator : AbstractValidator<RegisterUserDto>
    {
        public RegisterUserDtoValidator()
        {
            RuleFor(x => x.UserName).NotEmpty().MinimumLength(3);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .Matches("[A-Z]")
                .WithMessage("Password must contain an uppercase letter.")
                .Matches("[a-z]")
                .WithMessage("Password must contain a lowercase letter.")
                .Matches("[0-9]")
                .WithMessage("Password must contain a digit.")
                .Matches("[^a-zA-Z0-9]")
                .WithMessage("Password must contain a special character.");
            RuleFor(x => x.Role)
                .NotEmpty()
                .Must(role => FleetRoles.All.Contains(role.ToUpperInvariant())) // Check uppercase
                .WithMessage($"Role must be one of: {string.Join(", ", FleetRoles.All)}");
        }
    }

    public static void AddAuthApi(this WebApplication app)
    {
        app.MapPost(
                "api/accounts",
                async (
                    UserManager<FleetUser> userManager,
                    FleetDbContext dbContext,
                    RegisterUserDto dto,
                    IValidator<RegisterUserDto> validator
                ) =>
                {
                    var validationResult = await validator.ValidateAsync(dto);
                    if (!validationResult.IsValid)
                        return Results.ValidationProblem(validationResult.ToDictionary());

                    var user = await userManager.FindByNameAsync(dto.UserName);
                    if (user is not null)
                        return Results.UnprocessableEntity(
                            new { errors = new { UserName = new[] { "Username already taken" } } }
                        );
                    user = await userManager.FindByEmailAsync(dto.Email);
                    if (user is not null)
                        return Results.UnprocessableEntity(
                            new { errors = new { Email = new[] { "Email already taken" } } }
                        );

                    await using var transaction = await dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        user = new FleetUser
                        {
                            UserName = dto.UserName,
                            Email = dto.Email,
                            FamilyGroupId = Guid.NewGuid().ToString()
                        };
                        var result = await userManager.CreateAsync(user, dto.Password);
                        if (!result.Succeeded)
                        {
                            var identityErrors = result.Errors.ToDictionary(
                                e => e.Code,
                                e => new[] { e.Description }
                            );
                            await transaction.RollbackAsync(); // Rollback before returning
                            return Results.ValidationProblem(identityErrors);
                        }

                        var roleToAdd = dto.Role.ToUpperInvariant(); // Ensure uppercase
                        if (!FleetRoles.All.Contains(roleToAdd))
                        {
                            await transaction.RollbackAsync();
                            return Results.BadRequest(
                                new { errors = new { Role = new[] { "Invalid role specified." } } }
                            );
                        }

                        var roleResult = await userManager.AddToRoleAsync(user, roleToAdd); // Add uppercase role
                        if (!roleResult.Succeeded)
                        {
                            await transaction.RollbackAsync();
                            var roleErrors = roleResult.Errors.ToDictionary(
                                e => e.Code,
                                e => new[] { e.Description }
                            );
                            return Results.UnprocessableEntity(new { errors = roleErrors });
                        }

                        await transaction.CommitAsync();
                        return Results.Created(
                            $"/api/users/{user.Id}",
                            new
                            {
                                userId = user.Id,
                                userName = user.UserName,
                                role = roleToAdd
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        Console.WriteLine($"Registration Error: {ex}");
                        return Results.Problem(
                            "An unexpected error occurred during registration.",
                            statusCode: 500
                        );
                    }
                }
            )
            .AddFluentValidationAutoValidation();

        app.MapPost(
            "api/login",
            async (
                UserManager<FleetUser> userManager,
                JwtTokenService jwtTokenService,
                SessionService sessionService,
                HttpContext httpContext,
                LoginDto dto
            ) =>
            {
                var user = await userManager.FindByNameAsync(dto.UserName);
                if (user is null || !await userManager.CheckPasswordAsync(user, dto.Password))
                    return Results.UnprocessableEntity(
                        new { detail = "Invalid username or password" }
                    );

                var rolesFromDb = await userManager.GetRolesAsync(user);
                // *** FIX: Force roles to uppercase before putting in token ***
                var rolesForToken = rolesFromDb.Select(r => r.ToUpperInvariant()).ToList();
                Console.WriteLine(
                    $"User {user.UserName} roles (DB): {string.Join(", ", rolesFromDb)} -> For Token: {string.Join(", ", rolesForToken)}"
                );

                var sessionId = Guid.NewGuid();
                var expiresAt = DateTime.UtcNow.AddDays(3);
                var accessToken = jwtTokenService.CreateAccessToken(
                    user.UserName!,
                    user.Id,
                    rolesForToken
                ); // Pass uppercase roles
                var refreshToken = jwtTokenService.CreateRefreshToken(
                    sessionId,
                    user.Id,
                    expiresAt
                );

                await sessionService.CreateSessionAsync(
                    sessionId,
                    user.Id,
                    refreshToken,
                    expiresAt
                );

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Expires = expiresAt,
                    SameSite = SameSiteMode.Lax,
                    Secure = app.Environment.IsProduction(),
                };
                httpContext.Response.Cookies.Append("RefreshToken", refreshToken, cookieOptions);

                var primaryRole = rolesForToken.FirstOrDefault() ?? FleetRoles.FleetUser; // Use uppercase constant
                var userInfo = new UserInfo(
                    username: user.UserName!,
                    email: user.Email!,
                    role: primaryRole
                );

                return Results.Ok(new SuccessfulLoginDto(accessToken, userInfo));
            }
        );

        app.MapPost(
            "api/accessToken",
            async (
                UserManager<FleetUser> userManager,
                JwtTokenService jwtTokenService,
                SessionService sessionService,
                HttpContext httpContext
            ) =>
            {
                if (
                    !httpContext.Request.Cookies.TryGetValue("RefreshToken", out var refreshToken)
                    || string.IsNullOrEmpty(refreshToken)
                    || !jwtTokenService.TryParseRefreshToken(refreshToken, out var claims)
                )
                    return Results.UnprocessableEntity(
                        new { detail = "Invalid or missing refresh token." }
                    );

                var sessionId = claims?.FindFirstValue("SessionId");
                var userId = claims?.FindFirstValue(JwtRegisteredClaimNames.Sub);

                if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrEmpty(userId))
                    return Results.UnprocessableEntity(
                        new { detail = "Invalid refresh token payload." }
                    );

                if (!await sessionService.IsSessionValidAsync(Guid.Parse(sessionId), refreshToken))
                    return Results.UnprocessableEntity(
                        new { detail = "Session is invalid or expired." }
                    );

                var user = await userManager.FindByIdAsync(userId);
                if (user is null)
                    return Results.UnprocessableEntity(new { detail = "User not found." });

                var rolesFromDb = await userManager.GetRolesAsync(user);
                // *** FIX: Force roles to uppercase before putting in token ***
                var rolesForToken = rolesFromDb.Select(r => r.ToUpperInvariant()).ToList();
                Console.WriteLine(
                    $"User {user.UserName} roles (refresh DB): {string.Join(", ", rolesFromDb)} -> For Token: {string.Join(", ", rolesForToken)}"
                );

                var expiresAt = DateTime.UtcNow.AddDays(3);
                var newAccessToken = jwtTokenService.CreateAccessToken(
                    user.UserName!,
                    user.Id,
                    rolesForToken
                ); // Pass uppercase roles
                var newRefreshToken = jwtTokenService.CreateRefreshToken(
                    Guid.Parse(sessionId),
                    user.Id,
                    expiresAt
                );

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Expires = expiresAt,
                    SameSite = SameSiteMode.Lax,
                    Secure = app.Environment.IsProduction(),
                };
                httpContext.Response.Cookies.Append("RefreshToken", newRefreshToken, cookieOptions);

                await sessionService.ExtendSessionAsync(
                    Guid.Parse(sessionId),
                    newRefreshToken,
                    expiresAt
                );

                var primaryRole = rolesForToken.FirstOrDefault() ?? FleetRoles.FleetUser; // Use uppercase constant
                var userInfo = new UserInfo(
                    username: user.UserName!,
                    email: user.Email!,
                    role: primaryRole
                );

                return Results.Ok(new SuccessfulLoginDto(newAccessToken, userInfo));
            }
        );

        app.MapPost(
            "api/logout",
            async (
                JwtTokenService jwtTokenService,
                SessionService sessionService,
                HttpContext httpContext
            ) =>
            {
                httpContext.Request.Cookies.TryGetValue("RefreshToken", out var refreshToken);
                if (
                    !string.IsNullOrEmpty(refreshToken)
                    && jwtTokenService.TryParseRefreshToken(refreshToken, out var claims)
                )
                {
                    var sessionId = claims?.FindFirstValue("SessionId");
                    if (!string.IsNullOrWhiteSpace(sessionId))
                    {
                        try
                        {
                            await sessionService.InvalidateSessionAsync(Guid.Parse(sessionId));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(
                                $"Error invalidating session {sessionId}: {ex.Message}"
                            );
                        }
                    }
                }
                httpContext.Response.Cookies.Delete("RefreshToken");
                return Results.Ok();
            }
        );
    }
}
