namespace ServiceDesk.Core.Users;

public sealed class BootstrapAdministratorException(BootstrapAdministratorFailureKind failure) : Exception
{
    public BootstrapAdministratorFailureKind Failure { get; } = failure;
}
