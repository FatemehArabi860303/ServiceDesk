using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.WebApi.Authentication;

namespace ServiceDesk.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthenticationController(
    AuthenticateUserShell authenticateUserShell,
    JwtAccessTokenIssuer jwtAccessTokenIssuer) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthenticateUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthenticationFailureResponse>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthenticateUserResponse>> Login(
        AuthenticateUserHttpRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await authenticateUserShell.ExecuteAsync(
                new AuthenticateUserInput(request.Email, request.Password),
                cancellationToken);
            var token = jwtAccessTokenIssuer.Issue(user, DateTimeOffset.UtcNow);
            return Ok(new AuthenticateUserResponse(token.Value, token.ExpiresAt));
        }
        catch (AuthenticateUserException)
        {
            return Unauthorized(new AuthenticationFailureResponse("Authentication failed."));
        }
    }
}

public sealed record AuthenticateUserHttpRequest(
    [Required] string? Email,
    [Required] string? Password);

public sealed record AuthenticateUserResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed record AuthenticationFailureResponse(string Message);
