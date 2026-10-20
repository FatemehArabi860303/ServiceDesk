namespace ServiceDesk.Core.Authentication;

public static class ActivateUserAccountCore
{
    public static void Execute(ActivateUserAccountFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (!facts.ProvisionFound
            || !facts.ProvisionNotExpired
            || !facts.UserExists
            || !facts.UserActive
            || facts.UserAlreadyCredentialed)
        {
            throw new ActivateUserAccountException(ActivateUserAccountFailureKind.ActivationNotPermitted);
        }
    }
}
