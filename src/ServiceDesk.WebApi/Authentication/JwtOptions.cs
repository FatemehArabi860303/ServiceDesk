using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ServiceDesk.WebApi.Authentication;

public sealed record JwtOptions(string Issuer, string Audience, string SigningKey)
{
    public const int AccessTokenLifetimeMinutes = 30;

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new JwtOptions(
            configuration["Jwt:Issuer"] ?? string.Empty,
            configuration["Jwt:Audience"] ?? string.Empty,
            configuration["Jwt:SigningKey"] ?? string.Empty);

        if (string.IsNullOrWhiteSpace(options.Issuer)
            || string.IsNullOrWhiteSpace(options.Audience)
            || string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException("JWT issuer, audience, and signing key are required.");
        }

        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException("JWT signing key must contain at least 32 UTF-8 bytes.");
        }

        return options;
    }

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}
