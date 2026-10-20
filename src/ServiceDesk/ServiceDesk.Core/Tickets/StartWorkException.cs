namespace ServiceDesk.Core.Tickets;

public sealed class StartWorkException(StartWorkFailureKind failure) : Exception
{
    public StartWorkFailureKind Failure { get; } = failure;
}
