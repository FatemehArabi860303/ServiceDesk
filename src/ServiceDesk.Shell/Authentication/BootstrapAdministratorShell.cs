using Microsoft.AspNetCore.Identity;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Authentication;

public sealed class BootstrapAdministratorShell(
    IUserRepository userRepository,
    IAdministratorBootstrapRepository administratorBootstrapRepository,
    IPasswordHasher<User> passwordHasher)
{
    public async Task<User> ExecuteAsync(
        BootstrapAdministratorInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var normalizedPassword = PasswordPolicy.NormalizeAndValidate(input.Password);
        var email = UserRules.CanonicalizeEmail(input.Email);
        var isInstallationEmpty = await administratorBootstrapRepository.IsInstallationEmptyAsync(cancellationToken);
        var isEmailAvailable = await userRepository.IsEmailAvailableAsync(email, cancellationToken);
        var facts = new BootstrapAdministratorFacts(isInstallationEmpty, isEmailAvailable);
        var command = new BootstrapAdministratorCommand(input.FirstName, input.LastName, input.Email);
        var administrator = BootstrapAdministratorCore.Execute(command, facts, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var passwordHash = passwordHasher.HashPassword(administrator, normalizedPassword);
        var credential = new UserCredential(administrator.Id, passwordHash);

        if (!await administratorBootstrapRepository.TryAddAsync(administrator, credential, cancellationToken))
        {
            throw new BootstrapAdministratorException(BootstrapAdministratorFailureKind.InstallationNotEmpty);
        }

        return administrator;
    }
}
