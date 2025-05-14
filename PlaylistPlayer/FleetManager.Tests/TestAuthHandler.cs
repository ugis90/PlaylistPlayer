using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.IdentityModel.JsonWebTokens;

namespace FleetManager.Tests;

public class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public string DefaultUserId { get; set; } = "DefaultTestUserId";
    public string DefaultUserName { get; set; } = "DefaultTestUser";
    public string DefaultFamilyGroupId { get; set; } = "DefaultTestFamilyId";
    public IEnumerable<string> DefaultRoles { get; set; } = new List<string> { "FLEETUSER" }; // Default to a basic role
}

public class TestAuthHandler(
    IOptionsMonitor<TestAuthHandlerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<TestAuthHandlerOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "TestScheme";

    public static ClaimsPrincipal? TestUserClaimsPrincipal { get; set; }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        ClaimsPrincipal principal;

        if (TestUserClaimsPrincipal != null)
        {
            principal = TestUserClaimsPrincipal;
        }
        else
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, Options.DefaultUserId),
                new(ClaimTypes.Name, Options.DefaultUserName),
            };
            if (!string.IsNullOrEmpty(Options.DefaultFamilyGroupId))
            {
                claims.Add(new Claim("FamilyGroupId", Options.DefaultFamilyGroupId));
            }
            claims.AddRange(Options.DefaultRoles.Select(role => new Claim(ClaimTypes.Role, role)));
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            principal = new ClaimsPrincipal(identity);
        }

        var ticket = new AuthenticationTicket(principal, AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    public static void SetTestUser(
        string userId,
        string userName,
        string familyGroupId,
        IEnumerable<string> roles
    )
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(ClaimTypes.Name, userName),
        };
        if (!string.IsNullOrEmpty(familyGroupId))
        {
            claims.Add(new Claim("FamilyGroupId", familyGroupId));
        }
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role.ToUpperInvariant())));
        var identity = new ClaimsIdentity(claims, AuthenticationScheme);
        TestUserClaimsPrincipal = new ClaimsPrincipal(identity);
    }

    public static void ClearTestUser()
    {
        TestUserClaimsPrincipal = null;
    }
}
