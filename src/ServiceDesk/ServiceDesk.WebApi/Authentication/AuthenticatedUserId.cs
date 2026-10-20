using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ServiceDesk.WebApi.Authentication;

public static class AuthenticatedUserId
{
    public static bool TryGet(ClaimsPrincipal principal, out Guid userId)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return Guid.TryParse(subject, out userId);
    }
}
