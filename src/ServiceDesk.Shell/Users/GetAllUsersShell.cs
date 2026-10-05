using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Users;

public sealed class GetUsersShell(IUserRepository userRepository)
{
    public Task<IReadOnlyList<User>> ExecuteAsync(UserFilter? filter = null, CancellationToken cancellationToken = default)
    {
        return userRepository.GetUsersAsync(filter, cancellationToken);
    }
}