namespace ServiceDesk.Shell.Authentication;

public sealed record UserCredential(Guid UserId, string PasswordHash);
