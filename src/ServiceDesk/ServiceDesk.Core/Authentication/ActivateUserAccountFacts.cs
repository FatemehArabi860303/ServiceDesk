namespace ServiceDesk.Core.Authentication;

public sealed record ActivateUserAccountFacts(
    bool ProvisionFound,
    bool ProvisionNotExpired,
    bool UserExists,
    bool UserActive,
    bool UserAlreadyCredentialed);
