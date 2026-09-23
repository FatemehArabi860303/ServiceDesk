namespace ServiceDesk.Core.Authentication;

public sealed record ProvisionUserAccessFacts(
    bool CallerPermitted,
    bool TargetExists,
    bool TargetActive,
    bool TargetAlreadyCredentialed);
