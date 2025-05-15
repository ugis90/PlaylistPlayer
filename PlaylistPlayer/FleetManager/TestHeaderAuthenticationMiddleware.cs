// FleetManager/TestHeaderAuthenticationMiddleware.cs (or a TestUtils project)
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens; // For JwtRegisteredClaimNames
using System.Text.Json;

namespace FleetManager.Middleware;

public class TestHeaderAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    public const string TestUserIdHeader = "X-Test-User-Id";
    public const string TestUserRolesHeader = "X-Test-User-Roles"; // Comma-separated
    public const string TestUserFamilyHeader = "X-Test-User-FamilyGroupId";
    public const string TestUserNameHeader = "X-Test-User-Name";

    public TestHeaderAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    // FleetManager/Middleware/TestHeaderAuthenticationMiddleware.cs
    public async Task InvokeAsync(HttpContext context)
    {
        Console.WriteLine($"[TestHeaderAuth] Request Path: {context.Request.Path}");
        bool userSetByHeader = false;

        if (
            context.Request.Headers.TryGetValue(TestUserIdHeader, out var userIdValues)
            && userIdValues.FirstOrDefault() is string userId
        )
        {
            var userName = context.Request.Headers.TryGetValue(
                TestUserNameHeader,
                out var userNameValues
            )
                ? userNameValues.FirstOrDefault() ?? $"test_{userId}"
                : $"test_{userId}";

            var familyGroupId = context.Request.Headers.TryGetValue(
                TestUserFamilyHeader,
                out var familyValues
            )
                ? familyValues.FirstOrDefault()
                : null;

            var roles = new List<string>();
            if (
                context.Request.Headers.TryGetValue(TestUserRolesHeader, out var rolesValues)
                && rolesValues.FirstOrDefault() is string rolesString
            )
            {
                roles.AddRange(
                    rolesString.Split(
                        ',',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
                    )
                );
            }

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(ClaimTypes.Name, userName)
            };

            if (!string.IsNullOrEmpty(familyGroupId))
            {
                claims.Add(new Claim("FamilyGroupId", familyGroupId));
            }

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToUpperInvariant()));
            }

            var identity = new ClaimsIdentity(claims, "TestHeaderAuthScheme"); // Give it a scheme name
            context.User = new ClaimsPrincipal(identity);
            userSetByHeader = true;
            Console.WriteLine(
                $"[TestHeaderAuth] User '{userId}' SET from headers. Authenticated: {context.User.Identity?.IsAuthenticated}. Roles: {string.Join(",", roles)}"
            );
        }
        else
        {
            Console.WriteLine("[TestHeaderAuth] Test user headers NOT FOUND.");
            // Optionally, set an anonymous user or let it fall through if no headers
            // var anonymousIdentity = new ClaimsIdentity();
            // context.User = new ClaimsPrincipal(anonymousIdentity);
        }

        await _next(context);

        // Log after next middleware, useful for the 201 vs 403 issue
        if (userSetByHeader)
        {
            Console.WriteLine(
                $"[TestHeaderAuth] AFTER next for {context.Request.Path}. Response Status: {context.Response.StatusCode}."
            );
        }
    }
}

public static class TestHeaderAuthenticationExtensions
{
    public static IApplicationBuilder UseTestHeaderAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TestHeaderAuthenticationMiddleware>();
    }
}
