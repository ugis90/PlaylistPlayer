using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Threading;

namespace FleetManager.Tests;

public class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public string DefaultUserId { get; set; } = "DefaultTestUserId";
    public string DefaultUserName { get; set; } = "DefaultTestUser";
    public string DefaultFamilyGroupId { get; set; } = "DefaultTestFamilyId";
    public IEnumerable<string> DefaultRoles { get; set; } = new List<string> { "FLEETUSER" };
}

public class TestAuthHandler(
    IOptionsMonitor<TestAuthHandlerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<TestAuthHandlerOptions>(options, logger, encoder)
{
    public const string AuthenticationScheme = "TestScheme";

    private static readonly AsyncLocal<ClaimsPrincipal?> CurrentTestUserClaimsPrincipal = new();

    public static ClaimsPrincipal? TestUserClaimsPrincipal
    {
        get => CurrentTestUserClaimsPrincipal.Value;
        set => CurrentTestUserClaimsPrincipal.Value = value;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        ClaimsPrincipal? principalToUse = TestUserClaimsPrincipal;

        if (principalToUse == null)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, Options.DefaultUserId),
                new Claim(ClaimTypes.Name, Options.DefaultUserName),
            };
            if (!string.IsNullOrEmpty(Options.DefaultFamilyGroupId))
            {
                claims.Add(new Claim("FamilyGroupId", Options.DefaultFamilyGroupId));
            }

            claims.AddRange(Options.DefaultRoles.Select(role => new Claim(ClaimTypes.Role, role)));
            var identity = new ClaimsIdentity(claims, AuthenticationScheme);
            principalToUse = new ClaimsPrincipal(identity);
        }

        var ticket = new AuthenticationTicket(principalToUse, AuthenticationScheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    public static void SetTestUser(
        string userId,
        string userName,
        string? familyGroupId,
        IEnumerable<string> roles
    )
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(ClaimTypes.Name, userName),
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
