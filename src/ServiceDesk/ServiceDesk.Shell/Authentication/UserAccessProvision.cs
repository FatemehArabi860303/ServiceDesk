namespace ServiceDesk.Shell.Authentication;

public sealed record UserAccessProvision(
    Guid UserId,
    byte[] ActivationTokenHash,
    DateTimeOffset ExpiresAt);
