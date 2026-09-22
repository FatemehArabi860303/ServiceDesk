namespace ServiceDesk.Core.Authentication;

public sealed class AuthenticateUserException(AuthenticateUserFailureKind failure) : Exception
{
    public AuthenticateUserFailureKind Failure { get; } = failure;
}
