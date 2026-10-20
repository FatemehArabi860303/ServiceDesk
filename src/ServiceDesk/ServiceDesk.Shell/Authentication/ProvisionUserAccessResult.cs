namespace ServiceDesk.Shell.Authentication;

public sealed record ProvisionUserAccessResult(string ActivationToken, DateTimeOffset ExpiresAt);
