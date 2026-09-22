using System.Text;

namespace ServiceDesk.Core.Authentication;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;

    public static string NormalizeAndValidate(string? password)
    {
        var normalizedPassword = NormalizeForVerification(password);
        var length = normalizedPassword.EnumerateRunes().Count();

        if (length is < MinimumLength or > MaximumLength)
        {
            throw new PasswordPolicyException(PasswordPolicyFailureKind.InvalidLength);
        }

        return normalizedPassword;
    }

    public static string NormalizeForVerification(string? password)
    {
        if (password is null)
        {
            throw new PasswordPolicyException(PasswordPolicyFailureKind.InvalidLength);
        }

        try
        {
            return password.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            throw new PasswordPolicyException(PasswordPolicyFailureKind.InvalidLength);
        }
    }
}
