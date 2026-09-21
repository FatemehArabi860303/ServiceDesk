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
        var outcome = await createUserShell.ExecuteAsync(command, cancellationToken);

        return outcome switch
        {
            UserCreated created => Created($"/api/users/{created.User.Id}", ToResponse(created.User)),
            UserCreationRejected { Failure: CreateUserFailureKind.EmailUnavailable } => Conflict(CreateProblemDetails("Email unavailable")),
            UserCreationRejected rejected => BadRequest(CreateProblemDetails(rejected.Failure.ToString())),
            _ => throw new InvalidOperationException("Unknown user creation outcome.")
        };
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
