using System.Security.Cryptography;
using ServiceDesk.Core.Authentication;
using ServiceDesk.Core.Users;
using ServiceDesk.Shell.Users;

namespace ServiceDesk.Shell.Authentication;

public sealed class ProvisionUserAccessShell(
    IUserRepository userRepository,
    IUserAccessProvisionRepository userAccessProvisionRepository)
{
    private const int ActivationTokenByteLength = 32;
    private static readonly TimeSpan ActivationTokenLifetime = TimeSpan.FromHours(24);

    public async Task<ProvisionUserAccessResult> ExecuteAsync(
        Guid targetUserId,
        Guid callerUserId,
        CancellationToken cancellationToken = default)
    {
        var caller = await userRepository.GetByIdAsync(callerUserId, cancellationToken);
        var target = await userRepository.GetByIdAsync(targetUserId, cancellationToken);
        var targetAlreadyCredentialed = target is not null
            && await userAccessProvisionRepository.HasCredentialAsync(targetUserId, cancellationToken);
        var facts = new ProvisionUserAccessFacts(
            caller is { IsActive: true, Role: UserRole.Administrator },
            target is not null,
            target?.IsActive == true,
            targetAlreadyCredentialed);

        ProvisionUserAccessCore.Execute(facts);

        var tokenBytes = RandomNumberGenerator.GetBytes(ActivationTokenByteLength);
        var activationToken = ToBase64Url(tokenBytes);
        var expiresAt = DateTimeOffset.UtcNow.Add(ActivationTokenLifetime);
        var provision = new UserAccessProvision(
            targetUserId,
            SHA256.HashData(tokenBytes),
            expiresAt);

        await userAccessProvisionRepository.ReplaceAsync(provision, cancellationToken);

        return new ProvisionUserAccessResult(activationToken, expiresAt);
    }

    private static string ToBase64Url(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');
}
