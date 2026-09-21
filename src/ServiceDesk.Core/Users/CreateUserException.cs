namespace ServiceDesk.Core.Users;

public sealed class CreateUserException(CreateUserFailureKind failure) : Exception
{
    public CreateUserFailureKind Failure { get; } = failure;
}
