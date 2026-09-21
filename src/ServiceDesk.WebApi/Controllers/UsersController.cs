using Microsoft.AspNetCore.Mvc;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.WebApi.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(CreateUserShell createUserShell) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserHttpRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateUserCommand(request.FirstName, request.LastName, request.Email, request.Role);
        try
        {
            var user = await createUserShell.ExecuteAsync(command, cancellationToken);
            return Created($"/api/users/{user.Id}", ToResponse(user));
        }
        catch (CreateUserException exception) when (exception.Failure == CreateUserFailureKind.EmailUnavailable)
        {
            return Conflict(CreateProblemDetails("Email unavailable"));
        }
        catch (CreateUserException exception)
        {
            return BadRequest(CreateProblemDetails(exception.Failure.ToString()));
        }
    }

    private static UserResponse ToResponse(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role,
        user.IsActive,
        user.CreatedAt,
        user.UpdatedAt);

    private static ProblemDetails CreateProblemDetails(string detail) => new()
    {
        Title = "User creation was rejected.",
        Detail = detail
    };
}

public sealed record CreateUserHttpRequest(
    string? FirstName,
    string? LastName,
    string? Email,
    UserRole Role);

public sealed record UserResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
