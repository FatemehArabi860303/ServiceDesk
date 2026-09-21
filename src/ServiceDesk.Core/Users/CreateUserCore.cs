namespace ServiceDesk.Core.Users;

public static class CreateUserCore
{
    public const int FirstNameMaxLength = 100;
    public const int LastNameMaxLength = 100;
    public const int EmailMaxLength = 254;

    public static User Execute(
        CreateUserCommand command,
        CreateUserFacts facts,
        Guid userId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(facts);

        if (!IsValidName(command.FirstName, FirstNameMaxLength))
        {
            throw new CreateUserException(CreateUserFailureKind.InvalidFirstName);
        }

        if (!IsValidName(command.LastName, LastNameMaxLength))
        {
            throw new CreateUserException(CreateUserFailureKind.InvalidLastName);
        }

        var email = CanonicalizeEmail(command.Email);
        if (!IsValidEmail(email))
        {
            throw new CreateUserException(CreateUserFailureKind.InvalidEmail);
        }

        if (!Enum.IsDefined(command.Role))
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

    public static string CanonicalizeEmail(string? email) => email?.Trim().ToUpperInvariant() ?? string.Empty;

    private static bool IsValidName(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;

    private static bool IsValidEmail(string email)
    {
        if (email.Length is 0 or > EmailMaxLength || email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex != email.LastIndexOf('@') || atIndex == email.Length - 1)
        {
            return false;
        }

        var domain = email[(atIndex + 1)..];
        return domain.Length > 2 && domain[0] != '.' && domain[^1] != '.' && domain.Contains('.');
    }
}
