using System.Text;

namespace ServiceDesk.Core.Authentication;

public static class PasswordPolicy
{
    public const int MinimumLength = 15;
    public const int MaximumLength = 128;

    public static string NormalizeAndValidate(string? password)
    {
        if (password is null)
        {
            throw new PasswordPolicyException(PasswordPolicyFailureKind.InvalidLength);
        }

        var normalizedPassword = password.Normalize(NormalizationForm.FormC);
        var length = normalizedPassword.EnumerateRunes().Count();

        if (length is < MinimumLength or > MaximumLength)
        {
            throw new PasswordPolicyException(PasswordPolicyFailureKind.InvalidLength);
        }

        return normalizedPassword;
    }
}
