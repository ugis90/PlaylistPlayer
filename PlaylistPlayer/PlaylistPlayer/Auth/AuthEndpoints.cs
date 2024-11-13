using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PlaylistPlayer.Auth.Model;
using PlaylistPlayer.Data;

namespace PlaylistPlayer.Auth;

public static class AuthEndpoints
{
    public static void AddAuthApi(this WebApplication app)
    {
        // register
        //app.MapPost(
        //    "api/accounts",
        //    async (UserManager<MusicUser> userManager, RegisterUserDto dto) =>
        //    {
        //        // check user exists
        //        var user = await userManager.FindByNameAsync(dto.UserName);
        //        if (user is not null)
        //            return Results.UnprocessableEntity("Username already taken");

        //        // create user
        //        // TODO: wrap in a transaction
        //        user = new MusicUser { UserName = dto.UserName, Email = dto.Email };

        //        var result = await userManager.CreateAsync(user, dto.Password);
        //        if (!result.Succeeded)
        //            return Results.UnprocessableEntity(result.Errors);

        //        await userManager.AddToRoleAsync(user, MusicRoles.MusicUser);

        //        return Results.Created();
        //    }
        //);

        // register
        app.MapPost(
            "api/accounts",
            async (
                UserManager<MusicUser> userManager,
                MusicDbContext dbContext,
                RegisterUserDto dto
            ) =>
            {
                // check user exists
                var user = await userManager.FindByNameAsync(dto.UserName);
                if (user is not null)
                    return Results.UnprocessableEntity("Username already taken");

                // create user
                await using var transaction = await dbContext.Database.BeginTransactionAsync();
                try
                {
                    user = new MusicUser { UserName = dto.UserName, Email = dto.Email };

                    var result = await userManager.CreateAsync(user, dto.Password);
                    if (!result.Succeeded)
                        return Results.UnprocessableEntity(result.Errors);

                    var roleResult = await userManager.AddToRoleAsync(user, MusicRoles.MusicUser);
                    if (!roleResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return Results.UnprocessableEntity(roleResult.Errors);
                    }

                    await transaction.CommitAsync();
                    return Results.Created();
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        );

        // login
        app.MapPost(
            "api/login",
            async (
                UserManager<MusicUser> userManager,
                JwtTokenService jwtTokenService,
                SessionService sessionService,
                HttpContext httpContext,
                LoginDto dto
            ) =>
            {
                // check user exists
                var user = await userManager.FindByNameAsync(dto.UserName);
                if (user is null)
                    return Results.UnprocessableEntity("User does not exist");

                var isPasswordValid = await userManager.CheckPasswordAsync(user, dto.Password);
                if (!isPasswordValid)
                    return Results.UnprocessableEntity("Invalid password or username");

                var roles = await userManager.GetRolesAsync(user);

                var sessionId = Guid.NewGuid();
                var expiresAt = DateTime.UtcNow.AddDays(3);
                var accessToken = jwtTokenService.CreateAccessToken(user.UserName, user.Id, roles);
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
                    Secure = false, // should be true in production
                };

                httpContext.Response.Cookies.Append("RefreshToken", refreshToken, cookieOptions);

                return Results.Ok(new SuccessfulLoginDto(accessToken));
            }
        );

        app.MapPost(
            "api/accessToken",
            async (
                UserManager<MusicUser> userManager,
                JwtTokenService jwtTokenService,
                SessionService sessionService,
                HttpContext httpContext
            ) =>
            {
                if (!httpContext.Request.Cookies.TryGetValue("RefreshToken", out var refreshToken))
                    return Results.UnprocessableEntity();

                if (!jwtTokenService.TryParseRefreshToken(refreshToken, out var claims))
                    return Results.UnprocessableEntity();

                var sessionId = claims.FindFirstValue("SessionId");
                if (string.IsNullOrWhiteSpace(sessionId))
                    return Results.UnprocessableEntity();

                var sessionIdAsGuid = Guid.Parse(sessionId);
                if (!await sessionService.IsSessionValidAsync(sessionIdAsGuid, refreshToken))
                    return Results.UnprocessableEntity();

                var userId = claims.FindFirstValue(JwtRegisteredClaimNames.Sub);
                var user = await userManager.FindByIdAsync(userId);
                if (user is null)
                    return Results.UnprocessableEntity();

                var roles = await userManager.GetRolesAsync(user);

                var expiresAt = DateTime.UtcNow.AddDays(3);
                var accessToken = jwtTokenService.CreateAccessToken(user.UserName, user.Id, roles);
                var newRefreshToken = jwtTokenService.CreateRefreshToken(
                    sessionIdAsGuid,
                    user.Id,
                    expiresAt
                );

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Expires = expiresAt,
                    SameSite = SameSiteMode.Lax,
                    Secure = false, // should be true in production
                };

                httpContext.Response.Cookies.Append("RefreshToken", newRefreshToken, cookieOptions);

                await sessionService.ExtendSessionAsync(
                    sessionIdAsGuid,
                    newRefreshToken,
                    expiresAt
                );

                return Results.Ok(new SuccessfulLoginDto(accessToken));
            }
        );

        app.MapPost(
            "api/logout",
            async (
                UserManager<MusicUser> userManager,
                JwtTokenService jwtTokenService,
                SessionService sessionService,
                HttpContext httpContext
            ) =>
            {
                if (!httpContext.Request.Cookies.TryGetValue("RefreshToken", out var refreshToken))
                    return Results.UnprocessableEntity();

                if (!jwtTokenService.TryParseRefreshToken(refreshToken, out var claims))
                    return Results.UnprocessableEntity();

                var sessionId = claims.FindFirstValue("SessionId");
                if (string.IsNullOrWhiteSpace(sessionId))
                    return Results.UnprocessableEntity();

                await sessionService.InvalidateSessionAsync(Guid.Parse(sessionId));
                httpContext.Response.Cookies.Delete("RefreshToken");

                return Results.Ok();
            }
        );
    }

    public record RegisterUserDto(string UserName, string Email, string Password);

    public record LoginDto(string UserName, string Password);

    public record SuccessfulLoginDto(string accessToken);
}
