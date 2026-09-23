using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Authentication;

public sealed class ActivateUserAccountShell(
    IUserRepository userRepository,
    IUserAccessProvisionRepository userAccessProvisionRepository,
    IPasswordHasher<User> passwordHasher)
{
    private const int ActivationTokenByteLength = 32;

    public async Task ExecuteAsync(
        ActivateUserAccountInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!TryDecodeActivationToken(input.ActivationToken, out var tokenBytes))
        {
            ThrowActivationNotPermitted();
        }

        var activationTokenHash = SHA256.HashData(tokenBytes!);
        var provision = await userAccessProvisionRepository
            .FindByActivationTokenHashAsync(activationTokenHash, cancellationToken);
        var user = provision is null
            ? null
            : await userRepository.GetByIdAsync(provision.UserId, cancellationToken);
        var userAlreadyCredentialed = user is not null
            && await userAccessProvisionRepository.HasCredentialAsync(user.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var facts = new ActivateUserAccountFacts(
            provision is not null,
            provision?.ExpiresAt > now,
            user is not null,
            user?.IsActive == true,
            userAlreadyCredentialed);

        ActivateUserAccountCore.Execute(facts);

        var normalizedPassword = PasswordPolicy.NormalizeAndValidate(input.Password);
        var credential = new UserCredential(
            user!.Id,
            passwordHasher.HashPassword(user, normalizedPassword));
        var activated = await userAccessProvisionRepository
            .TryActivateAsync(credential, activationTokenHash, now, cancellationToken);

        if (!activated)
        {
            ThrowActivationNotPermitted();
        }
    }

    private static bool TryDecodeActivationToken(string? activationToken, out byte[]? tokenBytes)
    {
        tokenBytes = null;
        if (string.IsNullOrWhiteSpace(activationToken)
            || activationToken.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            return false;
        }

        try
        {
            var base64 = activationToken
                .Replace('-', '+')
                .Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var decoded = Convert.FromBase64String(base64);
            if (decoded.Length != ActivationTokenByteLength)
            {
                return false;
            }

            tokenBytes = decoded;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static void ThrowActivationNotPermitted() =>
        throw new ActivateUserAccountException(ActivateUserAccountFailureKind.ActivationNotPermitted);
}
