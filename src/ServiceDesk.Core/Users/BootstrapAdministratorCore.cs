namespace ServiceDesk.Core.Users;

public static class BootstrapAdministratorCore
{
    public static User Execute(
        BootstrapAdministratorCommand command,
        BootstrapAdministratorFacts facts,
        Guid userId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(facts);

        if (!facts.IsInstallationEmpty)
        {
            throw new BootstrapAdministratorException(BootstrapAdministratorFailureKind.InstallationNotEmpty);
        }

        if (!UserRules.IsValidName(command.FirstName, UserRules.FirstNameMaxLength))
        {
            throw new BootstrapAdministratorException(BootstrapAdministratorFailureKind.InvalidFirstName);
        }

        if (!UserRules.IsValidName(command.LastName, UserRules.LastNameMaxLength))
        {
            throw new BootstrapAdministratorException(BootstrapAdministratorFailureKind.InvalidLastName);
        }

        var email = UserRules.CanonicalizeEmail(command.Email);
        if (!UserRules.IsValidEmail(email))
        {
            throw new BootstrapAdministratorException(BootstrapAdministratorFailureKind.InvalidEmail);
        }

        if (!facts.IsEmailAvailable)
        {
            throw new BootstrapAdministratorException(BootstrapAdministratorFailureKind.EmailUnavailable);
        }

        return new User(
            userId,
            command.FirstName!.Trim(),
            command.LastName!.Trim(),
            email,
            UserRole.Administrator,
            IsActive: true,
            CreatedAt: now,
            UpdatedAt: now);
    }
}
