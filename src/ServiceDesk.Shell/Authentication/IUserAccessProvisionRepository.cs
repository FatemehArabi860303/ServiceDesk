namespace ServiceDesk.Shell.Authentication;

public interface IUserAccessProvisionRepository
{
    Task<bool> HasCredentialAsync(Guid userId, CancellationToken cancellationToken = default);

    Task ReplaceAsync(UserAccessProvision provision, CancellationToken cancellationToken = default);
}
