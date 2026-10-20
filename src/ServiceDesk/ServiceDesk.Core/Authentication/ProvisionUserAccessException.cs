namespace ServiceDesk.Core.Authentication;

public sealed class ProvisionUserAccessException(ProvisionUserAccessFailureKind failure) : Exception
{
    public ProvisionUserAccessFailureKind Failure { get; } = failure;
}
