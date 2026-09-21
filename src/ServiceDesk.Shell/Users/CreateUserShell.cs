using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Users;

public sealed class CreateUserShell(IUserRepository userRepository)
{
    public async Task<CreateUserOutcome> ExecuteAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var email = CreateUserCore.CanonicalizeEmail(command.Email);
        var isEmailAvailable = await userRepository.IsEmailAvailableAsync(email, cancellationToken);
        var facts = new CreateUserFacts(isEmailAvailable);
        var outcome = CreateUserCore.Execute(command, facts, Guid.NewGuid(), DateTimeOffset.UtcNow);

        if (outcome is not UserCreated created)
        {
            return outcome;
        }

        var user = created.User;

        try
        {
            await userRepository.AddAsync(user, cancellationToken);
            return outcome;
        }
        catch (UserEmailAlreadyExistsException)
        {
            return new UserCreationRejected(CreateUserFailureKind.EmailUnavailable);
        }
    }
}
