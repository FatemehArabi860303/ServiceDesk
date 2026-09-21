namespace ServiceDesk.Core.Users;

public sealed record CreateUserCommand(
    string? FirstName,
    string? LastName,
    string? Email,
    UserRole Role);
