using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Authentication;

public interface IAdministratorBootstrapRepository
{
    Task<bool> IsInstallationEmptyAsync(CancellationToken cancellationToken = default);

    Task<bool> TryAddAsync(
        User administrator,
        UserCredential credential,
        CancellationToken cancellationToken = default);
}
