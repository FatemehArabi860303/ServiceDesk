using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Users;

public interface IUserRepository
{
    Task<bool> IsEmailAvailableAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}
