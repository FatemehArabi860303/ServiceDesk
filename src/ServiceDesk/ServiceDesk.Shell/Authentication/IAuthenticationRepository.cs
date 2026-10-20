namespace ServiceDesk.Shell.Authentication;

public interface IAuthenticationRepository
{
    Task<AuthenticationUser?> FindByCanonicalEmailAsync(
        string canonicalEmail,
        CancellationToken cancellationToken = default);
}
