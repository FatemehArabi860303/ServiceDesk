using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ServiceDesk.WebApi.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int AccessTokenLifetimeMinutes = 30;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public static JwtOptions BindAndValidate(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<JwtOptions>() ?? new JwtOptions();

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
