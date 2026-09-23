using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Shell.Authentication;
using ServiceDesk.WebApi.Authentication;

namespace ServiceDesk.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/users/{userId:guid}/access-provisioning")]
public sealed class UserAccessProvisioningController(ProvisionUserAccessShell provisionUserAccessShell) : ControllerBase
{
    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<ProvisionUserAccessResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProvisionUserAccessResponse>> Provision(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUserId.TryGet(User, out var callerUserId))
        {
            return Forbid();
        }

        try
        {
            var result = await provisionUserAccessShell.ExecuteAsync(userId, callerUserId, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, new ProvisionUserAccessResponse(result.ActivationToken, result.ExpiresAt));
        }
        catch (ProvisionUserAccessException exception) when (exception.Failure == ProvisionUserAccessFailureKind.CallerNotPermitted)
        {
            return Forbid();
        }
        catch (ProvisionUserAccessException exception) when (exception.Failure == ProvisionUserAccessFailureKind.TargetNotFound)
        {
            return NotFound(CreateProblemDetails("Target User was not found."));
        }
        catch (ProvisionUserAccessException exception)
        {
            return Conflict(CreateProblemDetails(exception.Failure.ToString()));
        }
    }

    private static ProblemDetails CreateProblemDetails(string detail) => new()
    {
        Title = "User access provisioning was rejected.",
        Detail = detail
    };
}

public sealed record ProvisionUserAccessResponse(string ActivationToken, DateTimeOffset ExpiresAt);
