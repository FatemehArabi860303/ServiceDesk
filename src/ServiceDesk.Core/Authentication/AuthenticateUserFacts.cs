namespace ServiceDesk.Core.Authentication;

public sealed record AuthenticateUserFacts(bool UserPermitted, bool CredentialsValid);
