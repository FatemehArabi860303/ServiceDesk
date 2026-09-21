namespace ServiceDesk.Core.Users;

public abstract record CreateUserOutcome;

public sealed record UserCreated(User User) : CreateUserOutcome;

public sealed record UserCreationRejected(CreateUserFailureKind Failure) : CreateUserOutcome;
