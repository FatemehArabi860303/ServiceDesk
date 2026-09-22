namespace ServiceDesk.Core.Users;

public static class UserRules
{
    public const int FirstNameMaxLength = 100;
    public const int LastNameMaxLength = 100;
    public const int EmailMaxLength = 254;

    public static string CanonicalizeEmail(string? email) => email?.Trim().ToUpperInvariant() ?? string.Empty;

    public static bool IsValidName(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximumLength;

    public static bool IsValidEmail(string email)
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

    public static bool IsSupportedRole(UserRole role) => Enum.IsDefined(role);
}
