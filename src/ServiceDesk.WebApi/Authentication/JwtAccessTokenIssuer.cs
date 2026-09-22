using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Core.Users;

namespace ServiceDesk.WebApi.Authentication;

public sealed class JwtAccessTokenIssuer(JwtOptions options)
{
    public AccessToken Issue(User user, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(user);

        var expiresAt = now.AddMinutes(JwtOptions.AccessTokenLifetimeMinutes);
        var token = new JwtSecurityToken(
            options.Issuer,
            options.Audience,
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ],
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            new SigningCredentials(options.CreateSigningKey(), SecurityAlgorithms.HmacSha256));

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
