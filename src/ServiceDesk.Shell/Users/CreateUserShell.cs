using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Users;

public sealed class CreateUserShell(IUserRepository userRepository)
{
    public async Task<User> ExecuteAsync(
        CreateUserCommand command,
        Guid authenticatedCallerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = await userRepository.GetByIdAsync(authenticatedCallerUserId, cancellationToken);
        var callerPermitted = caller is { IsActive: true, Role: UserRole.Administrator };
        var email = CreateUserCore.CanonicalizeEmail(command.Email);
        var isEmailAvailable = await userRepository.IsEmailAvailableAsync(email, cancellationToken);
        var facts = new CreateUserFacts(callerPermitted, isEmailAvailable);
        var user = CreateUserCore.Execute(command, facts, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await userRepository.AddAsync(user, cancellationToken);

        return user;
    }
}
