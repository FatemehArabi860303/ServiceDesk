namespace ServiceDesk.Core.Authentication;

public sealed class PasswordPolicyException(PasswordPolicyFailureKind failure) : Exception
{
    public PasswordPolicyFailureKind Failure { get; } = failure;
}
