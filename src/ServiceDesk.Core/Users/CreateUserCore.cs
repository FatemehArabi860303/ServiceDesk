namespace ServiceDesk.Core.Users;

public static class CreateUserCore
{
    public const int FirstNameMaxLength = UserRules.FirstNameMaxLength;
    public const int LastNameMaxLength = UserRules.LastNameMaxLength;
    public const int EmailMaxLength = UserRules.EmailMaxLength;

    public static User Execute(
        CreateUserCommand command,
        CreateUserFacts facts,
        Guid userId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(facts);

        if (!UserRules.IsValidName(command.FirstName, FirstNameMaxLength))
        {
            throw new CreateUserException(CreateUserFailureKind.InvalidFirstName);
        }

        if (!UserRules.IsValidName(command.LastName, LastNameMaxLength))
        {
            throw new CreateUserException(CreateUserFailureKind.InvalidLastName);
        }

        var email = UserRules.CanonicalizeEmail(command.Email);
        if (!UserRules.IsValidEmail(email))
        {
            throw new CreateUserException(CreateUserFailureKind.InvalidEmail);
        }

        if (!UserRules.IsSupportedRole(command.Role))
        {
            throw new CreateUserException(CreateUserFailureKind.UnsupportedRole);
        }

        if (!facts.IsEmailAvailable)
        {
            throw new CreateUserException(CreateUserFailureKind.EmailUnavailable);
        }

        return new User(
            userId,
            command.FirstName!.Trim(),
            command.LastName!.Trim(),
            email,
            command.Role,
            IsActive: true,
            CreatedAt: now,
            UpdatedAt: now);
    }

    public static string CanonicalizeEmail(string? email) => UserRules.CanonicalizeEmail(email);
}
