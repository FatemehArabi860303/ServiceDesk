using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Users;

public sealed class GetAllUsersShell(IUserRepository userRepository)
{
    public Task<IReadOnlyList<User>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return userRepository.GetAllAsync(cancellationToken);
    }
}
