namespace ServiceDesk.Core.Authentication;

public static class AuthenticateUserCore
{
    public static void Execute(AuthenticateUserFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (!facts.UserPermitted || !facts.CredentialsValid)
        {
            throw new AuthenticateUserException(AuthenticateUserFailureKind.AuthenticationFailed);
        }
    }
}
