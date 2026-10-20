using ServiceDesk.Core.Users;

namespace ServiceDesk.Shell.Authentication;

public sealed record AuthenticationUser(User User, UserCredential? Credential);
