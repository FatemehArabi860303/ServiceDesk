namespace ServiceDesk.Core.Authentication;

public static class ProvisionUserAccessCore
{
    public static void Execute(ProvisionUserAccessFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (!facts.CallerPermitted)
        {
            throw new ProvisionUserAccessException(ProvisionUserAccessFailureKind.CallerNotPermitted);
        }

        if (!facts.TargetExists)
        {
            throw new ProvisionUserAccessException(ProvisionUserAccessFailureKind.TargetNotFound);
        }

        if (!facts.TargetActive)
        {
            throw new ProvisionUserAccessException(ProvisionUserAccessFailureKind.TargetInactive);
        }

        if (facts.TargetAlreadyCredentialed)
        {
            throw new ProvisionUserAccessException(ProvisionUserAccessFailureKind.TargetAlreadyCredentialed);
        }
    }
}
