namespace ServiceDesk.Shell.Authentication;

public interface IUserAccessProvisionRepository
{
    Task<UserAccessProvision?> FindByActivationTokenHashAsync(
        byte[] activationTokenHash,
        CancellationToken cancellationToken = default);

    Task<bool> HasCredentialAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ReplaceAsync(UserAccessProvision provision, CancellationToken cancellationToken = default);

    Task<bool> TryActivateAsync(
        UserCredential credential,
        byte[] activationTokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
