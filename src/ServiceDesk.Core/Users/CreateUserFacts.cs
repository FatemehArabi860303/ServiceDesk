namespace ServiceDesk.Core.Users;

public sealed record CreateUserFacts(bool CallerPermitted, bool IsEmailAvailable);
