namespace ServiceDesk.Core.Tickets;

public sealed class ConfirmResolutionException(ConfirmResolutionFailureKind failure) : Exception
{
    public ConfirmResolutionFailureKind Failure { get; } = failure;
}
