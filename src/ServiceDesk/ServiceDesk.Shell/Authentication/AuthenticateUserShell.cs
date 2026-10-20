using Microsoft.AspNetCore.Identity;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Authentication;

public sealed class AuthenticateUserShell(
    IAuthenticationRepository authenticationRepository,
    IPasswordHasher<User> passwordHasher)
{
    public async Task<User> ExecuteAsync(
        AuthenticateUserInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        string normalizedPassword;
        try
        {
            normalizedPassword = PasswordPolicy.NormalizeForVerification(input.Password);
        }
        catch (PasswordPolicyException)
        {
            AuthenticateUserCore.Execute(new AuthenticateUserFacts(false, false));
            throw new InvalidOperationException("Authentication failure was expected.");
        }

        var canonicalEmail = UserRules.CanonicalizeEmail(input.Email);
        var authenticationUser = await authenticationRepository
            .FindByCanonicalEmailAsync(canonicalEmail, cancellationToken);
        var credential = authenticationUser?.Credential;
        var verificationResult = credential is null || authenticationUser is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(
                authenticationUser.User,
                credential.PasswordHash,
                normalizedPassword);
        var credentialsValid = verificationResult == PasswordVerificationResult.Success
            || verificationResult == PasswordVerificationResult.SuccessRehashNeeded;
        var facts = new AuthenticateUserFacts(
            authenticationUser?.User.IsActive == true,
            credentialsValid);

        AuthenticateUserCore.Execute(facts);
        return authenticationUser!.User;
    }
}
