namespace ServiceDesk.Core.Authentication;

public sealed class ActivateUserAccountException(ActivateUserAccountFailureKind failure) : Exception
{
    public ActivateUserAccountFailureKind Failure { get; } = failure;
}
