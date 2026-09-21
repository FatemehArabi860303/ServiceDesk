using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Users;

public sealed class CreateUserShell(IUserRepository userRepository)
{
    public async Task<User> ExecuteAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = CreateUserCore.CanonicalizeEmail(command.Email);
        var isEmailAvailable = await userRepository.IsEmailAvailableAsync(email, cancellationToken);
        var facts = new CreateUserFacts(isEmailAvailable);
        var user = CreateUserCore.Execute(command, facts, Guid.NewGuid(), DateTimeOffset.UtcNow);

        await userRepository.AddAsync(user, cancellationToken);

        return user;
    }
}
