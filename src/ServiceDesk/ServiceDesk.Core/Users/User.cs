namespace ServiceDesk.Core.Users;

public sealed record User(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
