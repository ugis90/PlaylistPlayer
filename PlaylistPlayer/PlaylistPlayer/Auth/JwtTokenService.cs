using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PlaylistPlayer.Auth;

public class JwtTokenService(IConfiguration configuration)
{
    private readonly SymmetricSecurityKey _authSigningKey =
        new(Encoding.UTF8.GetBytes(configuration["JWT:Secret"]));
    private readonly string? _issuer = configuration["JWT:ValidIssuer"];
    private readonly string? _audience = configuration["JWT:ValidAudience"];

    public string CreateAccessToken(string userName, string userId, IEnumerable<string> roles)
    {
        var authClaims = new List<Claim>
        {
            new(ClaimTypes.Name, userName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, userId),
        };

        authClaims.AddRange(roles.Select(o => new Claim(ClaimTypes.Role, o)));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: authClaims,
            expires: DateTime.Now.AddMinutes(20),
            signingCredentials: new SigningCredentials(
                _authSigningKey,
                SecurityAlgorithms.HmacSha256
            )
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken(Guid sessionId, string userId, DateTime expires)
    {
        var authClaims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Sub, userId),
            new("SessionId", sessionId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: authClaims,
            expires: expires,
            signingCredentials: new SigningCredentials(
                _authSigningKey,
                SecurityAlgorithms.HmacSha256
            )
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool TryParseRefreshToken(string refreshToken, out ClaimsPrincipal? claims)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var validationParameters = new TokenValidationParameters
            {
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = _authSigningKey,
                ValidateLifetime = true,
            };

            claims = tokenHandler.ValidateToken(refreshToken, validationParameters, out _);

            return true;
        }
        catch
        {
            claims = null;
            return false;
        }
    }
}
